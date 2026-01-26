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
        
        private int previousHeroHealth = 10;

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

                // If the boss is null, it means the boss has died
                // This is a guarantee as with the new SaveState respawn
                // handling the boss does not go null in between as it did before
                if (boss == null)
                {
                    CurrentState = EpisodeState.BossDead;
                    RLManager.StaticLogger?.LogInfo($"[TrainingEpisodeManager] Boss died detected - boss is null");
                    consecutiveStuckSteps = 0; // Reset stuck counter on death
                }
                // If the hero's health has increased AND the boss is at max HP, 
                // it means the hero has died. 
                // NOTE: This would work even with an action space that has healing
                // since for the hero to be able to heal, they must have dealt some
                // damage to the boss first. (This is not true if you start the encounter
                // with full silk. So like. Don't do that. This might also not work with 
                // bosses like First Sinner that can heal but I don't care right now)
                else if (previousHeroHealth < currentHeroHealth && boss.hp == encounter.GetMaxHP())
                {
                    CurrentState = EpisodeState.HeroDead;
                    RLManager.StaticLogger?.LogInfo($"[TrainingEpisodeManager] Hero died detected - health jumped from {previousHeroHealth} to {currentHeroHealth} and boss is at max HP");
                }
            }


            previousHeroHealth = currentHeroHealth;
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
                    return false;
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
            previousHeroHealth = 10;
            consecutiveStuckSteps = 0;
            
            RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Episode reset complete, resuming training");
            
            OnResetComplete?.Invoke();
        }

        private bool HandleDeathReset(HeroController hero, HealthManager boss, string reason)
        {
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
                if (boss != null)
                {
                    RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Boss respawned - reset complete");
                    ResetEpisode();
                    return false;
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
            
            return true;
        }

        private bool HandleStuckReset(HeroController hero, HealthManager boss)
        {
            if (!hasTriggeredReset)
            {
                hasTriggeredReset = true;
                resetSequenceStartTime = Time.time;
                RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Hero stuck - starting automatic reset sequence...");
            }
            
            if (!hasPressedF5 && Time.time - resetSequenceStartTime >= resetDelayDuration)
            {
                OnSimulateKeyPress?.Invoke(KeyCode.F5);
                hasPressedF5 = true;
                RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] F5 pressed, waiting for boss respawn...");
            }
            
            if (hasPressedF5 && boss != null)
            {
                RLManager.StaticLogger?.LogInfo("[TrainingEpisodeManager] Boss respawned after stuck reset - reset complete");
                ResetEpisode();
                return false;
            }
            
            return true;
        }
    }
}

