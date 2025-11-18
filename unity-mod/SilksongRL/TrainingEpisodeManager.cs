using UnityEngine;

namespace SilksongRL
{
    /// <summary>
    /// Manages the lifecycle of training episodes including death detection,
    /// reset sequences, and state transitions.
    /// This class is decoupled from specific boss implementations.
    /// </summary>
    public class TrainingEpisodeManager
    {
        // Episode state machine
        public enum EpisodeState
        {
            Training,        // Normal training mode
            HeroDead,        // Hero died, need to reset
            BossDead         // Boss died, need to reset
        }

        public EpisodeState CurrentState { get; private set; }

        private IBossEncounter encounter;
        
        // Death detection state tracking
        private int previousHeroHealth = -1;
        private bool wasBossPresent = false;
        
        private bool hasTriggeredReset = false;
        private float resetSequenceStartTime = 0f;
        private float resetDelayDuration = 0.5f; // Delay before pressing F5 after death

        public System.Action<KeyCode> OnSimulateKeyPress;
        public System.Action OnResetComplete;

        public TrainingEpisodeManager(IBossEncounter encounter)
        {
            this.encounter = encounter;
            CurrentState = EpisodeState.Training;
        }

        /// <summary>
        /// Updates the episode state based on current game conditions.
        /// Should be called every fixed update.
        /// </summary>
        public void UpdateEpisodeState(HeroController hero, HealthManager boss)
        {
            if (hero == null)
                return;

            int currentHeroHealth = hero.playerData.health;
            bool isBossPresent = (boss != null);
           

            // Only detect death transitions when in training mode
            if (CurrentState == EpisodeState.Training)
            {
                // Detect death when boss becomes null
                if (wasBossPresent && !isBossPresent)
                {
                    // Check if hero health jumped from low to full (hero died and reset)
                    bool heroHealthJumped = (previousHeroHealth <= 3 && currentHeroHealth == 10);
                    
                    if (heroHealthJumped)
                    {
                        CurrentState = EpisodeState.HeroDead;
                        RLManager.StaticLogger?.LogInfo($"[TrainingEpisodeManager] Hero died detected - health jumped from {previousHeroHealth} to {currentHeroHealth}");
                    }
                    else
                    {
                        CurrentState = EpisodeState.BossDead;
                        RLManager.StaticLogger?.LogInfo($"[TrainingEpisodeManager] Boss died detected - no hero health jump (was {previousHeroHealth}, now {currentHeroHealth})");
                    }
                }
            }

            // Update tracking for next frame
            previousHeroHealth = currentHeroHealth;
            wasBossPresent = isBossPresent;
        }

        /// <summary>
        /// Handles the reset sequence. Returns true if reset is in progress (skip normal step processing).
        /// </summary>
        public bool HandleResetSequence(HeroController hero, HealthManager boss)
        {
            switch (CurrentState)
            {
                case EpisodeState.HeroDead:
                    ResetEpisode();
                    return true;

                case EpisodeState.BossDead:
                    return HandleDeathReset(hero, boss, "Boss defeated");

                default:
                    return false; // Normal training
            }
        }

        /// <summary>
        /// Checks if the episode is done (either death or victory).
        /// </summary>
        public bool IsEpisodeDone()
        {
            return CurrentState == EpisodeState.HeroDead || CurrentState == EpisodeState.BossDead;
        }

        /// <summary>
        /// Resets the episode manager state after a successful reset.
        /// </summary>
        public void ResetEpisode()
        {
            CurrentState = EpisodeState.Training;
            hasTriggeredReset = false;
            
            // Reset death detection tracking
            previousHeroHealth = -1;
            wasBossPresent = false;
            
            RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Episode reset complete, resuming training");
            
            OnResetComplete?.Invoke();
        }

        private bool HandleDeathReset(HeroController hero, HealthManager boss, string reason)
        {
            // First time in death state - trigger reset
            if (!hasTriggeredReset)
            {
                hasTriggeredReset = true;
                resetSequenceStartTime = Time.time;
                RLManager.StaticLogger?.LogInfo($"[TrainingEpisodeManager] {reason} - starting automatic reset sequence...");
            }
            
            // Wait for delay, then press reset (F5)
            // Delay is necessary because immediate press would often break the game
            if (hasTriggeredReset && Time.time - resetSequenceStartTime >= resetDelayDuration)
            {
                // Check if boss has respawned (reset complete)
                if (boss != null)
                {
                    RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Boss respawned - reset complete");
                    ResetEpisode();
                    return false; // Resume normal training
                }
                
                // Boss not present yet, press F5 if we haven't recently
                // (We check every frame but only press once per reset delay period)
                if (Time.time - resetSequenceStartTime >= resetDelayDuration && 
                    Time.time - resetSequenceStartTime < resetDelayDuration + 0.1f)
                {
                    OnSimulateKeyPress?.Invoke(KeyCode.F5);
                    RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] F5 pressed");
                }
            }
            
            return true; // Skip normal step processing
        }
    }
}

