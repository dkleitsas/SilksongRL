using UnityEngine;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Text;
using System.IO;

namespace SilksongRL
{
    [Serializable]
    public class SocketConfig
    {
        public string Host = "localhost";
        public int Port = 8000;
        public float Timeout = 10f;
        public int MaxReconnectAttempts = 5;
        public float ReconnectDelay = 1f;
    }

    // Message types for protocol
    public enum MessageType : byte
    {
        Initialize = 0,
        GetAction = 1,
        StoreTransition = 2,
        InitResponse = 10,
        ActionResponse = 11,
        TransitionAck = 12,
        Error = 255
    }

    public class SocketClient : ICommClient
    {
        private SocketConfig config;
        private TcpClient client;
        private NetworkStream stream;
        private bool isConnected = false;

        public bool IsConnected => isConnected && client?.Connected == true;

        public SocketClient(SocketConfig config = null)
        {
            this.config = config ?? new SocketConfig();
        }

        #region Connection Management

        public async Task<bool> ConnectAsync()
        {
            if (IsConnected) return true;

            for (int attempt = 0; attempt < config.MaxReconnectAttempts; attempt++)
            {
                try
                {
                    RLManager.StaticLogger?.LogInfo($"[SocketClient] Connecting to {config.Host}:{config.Port} (attempt {attempt + 1}/{config.MaxReconnectAttempts})");

                    client = new TcpClient();
                    client.NoDelay = true; // Disable Nagle's algorithm for lower latency
                    
                    var connectTask = client.ConnectAsync(config.Host, config.Port);
                    var timeoutTask = Task.Delay((int)(config.Timeout * 1000));

                    if (await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
                    {
                        throw new TimeoutException("Connection timed out");
                    }

                    await connectTask.ConfigureAwait(false); // Propagate any exceptions

                    stream = client.GetStream();
                    isConnected = true;

                    RLManager.StaticLogger?.LogInfo("[SocketClient] Connected successfully");
                    return true;
                }
                catch (Exception e)
                {
                    RLManager.StaticLogger?.LogWarning($"[SocketClient] Connection failed: {e.Message}");
                    Disconnect();

                    if (attempt < config.MaxReconnectAttempts - 1)
                    {
                        await Task.Delay((int)(config.ReconnectDelay * 1000 * Math.Pow(2, attempt))).ConfigureAwait(false);
                    }
                }
            }

            RLManager.StaticLogger?.LogError("[SocketClient] Failed to connect after all attempts");
            return false;
        }

        public void Disconnect()
        {
            try
            {
                stream?.Close();
                client?.Close();
            }
            catch (Exception e)
            {
                RLManager.StaticLogger?.LogWarning($"[SocketClient] Error during disconnect: {e.Message}");
            }
            finally
            {
                stream = null;
                client = null;
                isConnected = false;
            }
        }

        #endregion

        #region Public API (matches APIClient interface)

        public async Task<InitResponse> InitializeAsync(string bossName, int observationSize)
        {
            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false)) return null;

                InitRequest request = new InitRequest
                {
                    boss_name = bossName,
                    observation_size = observationSize
                };

                string json = JsonUtility.ToJson(request);
                await SendMessageAsync(MessageType.Initialize, json).ConfigureAwait(false);

                var (msgType, responseJson) = await ReceiveMessageAsync().ConfigureAwait(false);

                if (msgType == MessageType.InitResponse)
                {
                    InitResponse response = JsonUtility.FromJson<InitResponse>(responseJson);
                    RLManager.StaticLogger?.LogInfo($"[SocketClient] Initialized for boss '{response.boss_name}' with observation size {response.observation_size}");
                    return response;
                }
                else if (msgType == MessageType.Error)
                {
                    RLManager.StaticLogger?.LogError($"[SocketClient] Server error: {responseJson}");
                    return null;
                }

                return null;
            }
            catch (Exception e)
            {
                RLManager.StaticLogger?.LogError($"[SocketClient] Initialize failed: {e.Message}");
                HandleConnectionError();
                return null;
            }
        }

        public async Task<Action> GetActionAsync(float[] observations)
        {
            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false)) return null;

                StateRequest request = new StateRequest { state = observations };
                string json = JsonUtility.ToJson(request);

                await SendMessageAsync(MessageType.GetAction, json).ConfigureAwait(false);

                var (msgType, responseJson) = await ReceiveMessageAsync().ConfigureAwait(false);

                if (msgType == MessageType.ActionResponse)
                {
                    ActionResponse response = JsonUtility.FromJson<ActionResponse>(responseJson);
                    return ActionManager.ArrayToAction(response);
                }
                else if (msgType == MessageType.Error)
                {
                    RLManager.StaticLogger?.LogError($"[SocketClient] Server error: {responseJson}");
                    return null;
                }

                return null;
            }
            catch (Exception e)
            {
                RLManager.StaticLogger?.LogError($"[SocketClient] GetAction failed: {e.Message}");
                HandleConnectionError();
                return null;
            }
        }

        public async Task<bool> StoreTransitionAsync(float[] observations, Action action, float reward, float[] nextObservations, bool done)
        {
            try
            {
                if (!await EnsureConnectedAsync().ConfigureAwait(false)) return false;

                TransitionRequest request = new TransitionRequest
                {
                    state = observations,
                    action = ActionManager.ActionToArray(action),
                    reward = reward,
                    next_state = nextObservations,
                    done = done
                };

                string json = JsonUtility.ToJson(request);
                await SendMessageAsync(MessageType.StoreTransition, json).ConfigureAwait(false);

                var (msgType, responseJson) = await ReceiveMessageAsync().ConfigureAwait(false);

                if (msgType == MessageType.TransitionAck)
                {
                    return true;
                }
                else if (msgType == MessageType.Error)
                {
                    RLManager.StaticLogger?.LogError($"[SocketClient] Server error: {responseJson}");
                    return false;
                }

                return false;
            }
            catch (Exception e)
            {
                RLManager.StaticLogger?.LogError($"[SocketClient] StoreTransition failed: {e.Message}");
                HandleConnectionError();
                return false;
            }
        }

        #endregion

        #region Message Framing (Length-Prefixed Protocol)

        // Message format:
        // [4 bytes: length (big-endian)] [1 byte: message type] [N bytes: JSON payload]

        private async Task SendMessageAsync(MessageType msgType, string payload)
        {
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);
            int totalLength = 1 + payloadBytes.Length; // 1 byte for message type + payload

            byte[] lengthBytes = BitConverter.GetBytes(totalLength);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes); // Convert to big-endian
            }

            // Combine all data into single buffer to avoid multiple TCP packets
            byte[] fullMessage = new byte[4 + 1 + payloadBytes.Length];
            Buffer.BlockCopy(lengthBytes, 0, fullMessage, 0, 4);
            fullMessage[4] = (byte)msgType;
            Buffer.BlockCopy(payloadBytes, 0, fullMessage, 5, payloadBytes.Length);

            // Single write for entire message
            await stream.WriteAsync(fullMessage, 0, fullMessage.Length).ConfigureAwait(false);
        }

        private async Task<(MessageType, string)> ReceiveMessageAsync()
        {
            // Read length prefix (4 bytes)
            byte[] lengthBytes = await ReadExactAsync(4).ConfigureAwait(false);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            int length = BitConverter.ToInt32(lengthBytes, 0);

            if (length <= 0 || length > 1024 * 1024) // Sanity check: max 1MB
            {
                throw new InvalidDataException($"Invalid message length: {length}");
            }

            // Read message type (1 byte)
            byte[] msgTypeBytes = await ReadExactAsync(1).ConfigureAwait(false);
            MessageType msgType = (MessageType)msgTypeBytes[0];

            // Read payload
            int payloadLength = length - 1;
            string payload = "";
            
            if (payloadLength > 0)
            {
                byte[] payloadBytes = await ReadExactAsync(payloadLength).ConfigureAwait(false);
                payload = Encoding.UTF8.GetString(payloadBytes);
            }

            return (msgType, payload);
        }

        // Helper to read exact number of bytes (TCP can deliver partial data)
        private async Task<byte[]> ReadExactAsync(int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;

            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead).ConfigureAwait(false);
                
                if (bytesRead == 0)
                {
                    throw new IOException("Connection closed by server");
                }
                
                totalRead += bytesRead;
            }

            return buffer;
        }

        #endregion

        #region Helper Methods

        private async Task<bool> EnsureConnectedAsync()
        {
            if (IsConnected) return true;
            return await ConnectAsync().ConfigureAwait(false);
        }

        private void HandleConnectionError()
        {
            // Mark as disconnected so next call attempts reconnection
            isConnected = false;
        }

        public void UpdateConfig(SocketConfig newConfig)
        {
            config = newConfig;
        }

        #endregion
    }
}

