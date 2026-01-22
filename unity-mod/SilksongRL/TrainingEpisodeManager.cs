using UnityEngine;

namespace SilksongRL
{
    /// <summary>
    /// Manages the lifecycle of training episodes including death detection,
    /// reset sequences, and state transitions.
    /// </summary>
    public class TrainingEpisodeManager
    {
        // Episode state machine
        public enum EpisodeState
        {
            Training,        // Normal training mode
            HeroDead,        // Hero died, need to reset
            BossDead,        // Boss died, need to reset
            HeroStuck        // Hero stuck (e.g., below ground), need to force reset
        }

        public EpisodeState CurrentState { get; private set; }

        private IBossEncounter encounter;
        
        // Death detection state tracking
        private int previousHeroHealth = -1;
        private bool wasBossPresent = false;
        
        // Stuck detection
        // Hornet was sometimes getting stuck below the ground
        // due to issues with the Debug mod resetting
        // Resetting straight into a fight can be finnicky
        // The specific conditions of getting stuck should
        // be defined in the individual encounter implementations

        // TO DO 
        // MAKE STUCK STEP THRESHOLD CONFIGURABLE BY EACH ENCOUNTER
        private const int STUCK_STEP_THRESHOLD = 5000;
        private int consecutiveStuckSteps = 0;
        
        private bool hasTriggeredReset = false;
        private bool hasPressedF5 = false;
        private float resetSequenceStartTime = 0f;
        private float resetDelayDuration = 0.5f; // Delay before pressing F5

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
                if (encounter.IsHeroStuck(hero))
                {
                    consecutiveStuckSteps++;
                    if (consecutiveStuckSteps >= STUCK_STEP_THRESHOLD)
                    {
                        CurrentState = EpisodeState.HeroStuck;
                        RLManager.StaticLogger?.LogInfo($"[TrainingEpisodeManager] Hero stuck for {consecutiveStuckSteps} steps - triggering reset");
                        consecutiveStuckSteps = 0;
                    }
                }
                else
                {
                    consecutiveStuckSteps = 0;
                }
                
                // Detect death when boss becomes null
                if (wasBossPresent && !isBossPresent)
                {
                    // Check if hero health jumped from low to full (hero died and reset)
                    // This is needed because hero or boss health does not go to 0, instead
                    // they just disappear.
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
                    
                    consecutiveStuckSteps = 0; // Reset stuck counter on death
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

                case EpisodeState.HeroStuck:
                    return HandleStuckReset(hero, boss);

                default:
                    return false; // Normal training
            }
        }

        /// <summary>
        /// Resets the episode manager state after a successful reset.
        /// </summary>
        public void ResetEpisode()
        {
            CurrentState = EpisodeState.Training;
            hasTriggeredReset = false;
            hasPressedF5 = false;
            
            // Reset death detection tracking
            previousHeroHealth = -1;
            wasBossPresent = false;
            
            // Reset stuck detection
            consecutiveStuckSteps = 0;
            
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

        private bool HandleStuckReset(HeroController hero, HealthManager boss)
        {
            // First time in stuck state - trigger reset
            if (!hasTriggeredReset)
            {
                hasTriggeredReset = true;
                resetSequenceStartTime = Time.time;
                RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Hero stuck - starting automatic reset sequence...");
            }
            
            // Wait for delay, then press F5
            if (!hasPressedF5 && Time.time - resetSequenceStartTime >= resetDelayDuration)
            {
                OnSimulateKeyPress?.Invoke(KeyCode.F5);
                hasPressedF5 = true;
                RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] F5 pressed, waiting for boss respawn...");
            }
            
            // After pressing F5, wait for boss to respawn
            if (hasPressedF5 && boss != null)
            {
                RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Boss respawned after stuck reset - reset complete");
                ResetEpisode();
                return false; // Resume normal training
            }
            
            return true; // Skip normal step processing
        }
    }
}

