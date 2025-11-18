using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Threading.Tasks;
using System.Text;
using System.Collections;

namespace SilksongRL
{
    [Serializable]
    public class APIConfig
    {
        public string BaseUrl = "http://localhost:8000";
        public float Timeout = 10f;
        public int MaxRetries = 3;
        public float RetryDelay = 1f;
    }

    [Serializable]
    public class StateRequest
    {
        public float[] state;
    }

    [Serializable]
    public class ActionResponse
    {
        public int[] action;
    }

    [Serializable]
    public class TransitionRequest
    {
        public float[] state;
        public int[] action;
        public float reward;
        public float[] next_state;
        public bool done;
    }

    [Serializable]
    public class InitRequest
    {
        public string boss_name;
        public int observation_size;
    }

    [Serializable]
    public class InitResponse
    {
        public bool initialized;
        public string boss_name;
        public int observation_size;
        public bool checkpoint_loaded;
    }

    public class APIClient
    {
        private APIConfig config;

        public APIClient(APIConfig config = null)
        {
            this.config = config ?? new APIConfig();
        }

        // Initialize the API with boss name and observation size
        public async Task<InitResponse> InitializeAsync(string bossName, int observationSize)
        {
            try
            {
                string endpoint = "/initialize";
                InitRequest request = new InitRequest 
                { 
                    boss_name = bossName,
                    observation_size = observationSize
                };
                
                InitResponse response = await SendRequestAsync<InitResponse>(endpoint, request);
                
                if (response != null && response.initialized)
                {
                    RLManager.StaticLogger?.LogInfo($"[APIClient] Initialized for boss '{response.boss_name}' with observation size {response.observation_size}");
                    RLManager.StaticLogger?.LogInfo($"[APIClient] Checkpoint loaded: {response.checkpoint_loaded}");
                }
                
                return response;
            }
            catch (Exception e)
            {
                RLManager.StaticLogger?.LogError($"[APIClient] Initialize failed: {e.Message}");
                return null;
            }
        }

        // Get obs, call API, return action
        public async Task<Action> GetActionAsync(float[] observations)
        {
            try
            {
                string endpoint = "/get_action";
                StateRequest request = new StateRequest { state = observations };
                
                ActionResponse response = await SendRequestAsync<ActionResponse>(endpoint, request);
                
                if (response != null)
                {
                    return ActionManager.ArrayToAction(response);
                }
                
                return null;
            }
            catch (Exception e)
            {
                RLManager.StaticLogger?.LogError($"[APIClient] GetAction failed: {e.Message}");
                return null;
            }
        }

        // Send transition to API
        public async Task<bool> StoreTransitionAsync(float[] observations, Action action, float reward, float[] nextObservations, bool done)
        {
            try
            {
                string endpoint = "/store_transition";
                TransitionRequest request = new TransitionRequest
                {
                    state = observations,
                    action = ActionManager.ActionToArray(action),
                    reward = reward,
                    next_state = nextObservations,
                    done = done
                };

                bool success = await SendRequestAsync<bool>(endpoint, request);
                return success;
            }
            catch (Exception e)
            {
                RLManager.StaticLogger?.LogError($"[APIClient] StoreTransition failed: {e.Message}");
                return false;
            }
        }


        private async Task<T> SendRequestAsync<T>(string endpoint, object payload)
        {
            string url = config.BaseUrl + endpoint;
            string jsonPayload = JsonUtility.ToJson(payload);

            for (int attempt = 0; attempt < config.MaxRetries; attempt++)
            {
                try
                {
                    using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
                    {
                        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
                        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.SetRequestHeader("Content-Type", "application/json");
                        request.timeout = (int)config.Timeout;

                        var operation = request.SendWebRequest();

                        while (!operation.isDone)
                        {
                            await Task.Yield();
                        }

                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            string jsonResponse = request.downloadHandler.text;
                            
                            if (typeof(T) == typeof(bool))
                            {
                                return (T)(object)true;
                            }
                            else
                            {
                                return JsonUtility.FromJson<T>(jsonResponse);
                            }
                        }
                        else
                        {
                            RLManager.StaticLogger?.LogWarning($"[APIClient] Request failed (attempt {attempt + 1}/{config.MaxRetries}): {request.error}");
                            
                            if (attempt < config.MaxRetries - 1)
                            {
                                await Task.Delay((int)(config.RetryDelay * 1000 * Math.Pow(2, attempt)));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    RLManager.StaticLogger?.LogError($"[APIClient] Request exception (attempt {attempt + 1}/{config.MaxRetries}): {e.Message}");
                    
                    if (attempt < config.MaxRetries - 1)
                    {
                        await Task.Delay((int)(config.RetryDelay * 1000 * Math.Pow(2, attempt)));
                    }
                }
            }

            return default(T);
        }

        public void UpdateConfig(APIConfig newConfig)
        {
            config = newConfig;
        }
    }
}
