using System.Threading.Tasks;

namespace SilksongRL
{
    /// <summary>
    /// Interface for RL communication clients.
    /// Allows swapping between HTTP API and Socket implementations.
    /// </summary>
    public interface ICommClient
    {
        /// <summary>
        /// Whether the client is currently connected/ready to communicate.
        /// For HTTP, this is always true. For sockets, reflects connection state.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Establish connection to the server (if needed).
        /// HTTP clients can return true immediately.
        /// </summary>
        Task<bool> ConnectAsync();

        /// <summary>
        /// Disconnect from the server (if needed).
        /// HTTP clients can be a no-op.
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Initialize the RL session with boss name and observation size.
        /// </summary>
        Task<InitResponse> InitializeAsync(string bossName, int observationSize);

        /// <summary>
        /// Get an action from the RL agent given current observations.
        /// </summary>
        Task<Action> GetActionAsync(float[] observations);

        /// <summary>
        /// Store a transition (state, action, reward, next_state, done) for training.
        /// </summary>
        Task<bool> StoreTransitionAsync(float[] observations, Action action, float reward, float[] nextObservations, bool done);
    }
}

