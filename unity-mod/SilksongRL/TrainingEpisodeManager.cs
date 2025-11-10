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
            BossDead,        // Boss died, need to reset
            Resetting        // Currently in reset sequence
        }

        public EpisodeState CurrentState { get; private set; }

        private IBossEncounter encounter;
        
        private bool isInResetSequence = false;
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
            if (hero == null || boss == null)
                return;

            bool heroAlive = !encounter.IsHeroDead(hero);
            bool bossAlive = !encounter.IsBossDead(boss);

            // Only detect death transitions when in training mode
            if (CurrentState == EpisodeState.Training)
            {
                if (!heroAlive)
                {
                    CurrentState = EpisodeState.HeroDead;
                    Debug.Log("[TrainingEpisodeManager] Hero died - transitioning to reset sequence");
                }
                else if (!bossAlive)
                {
                    CurrentState = EpisodeState.BossDead;
                    Debug.Log("[TrainingEpisodeManager] Boss died - transitioning to reset sequence");
                }
            }
        }

        /// <summary>
        /// Handles the reset sequence. Returns true if reset is in progress (skip normal step processing).
        /// </summary>
        public bool HandleResetSequence(HeroController hero, HealthManager boss, ref Action currentAction)
        {
            switch (CurrentState)
            {
                case EpisodeState.HeroDead:
                    return HandleDeathReset(hero, boss, "Hero died");

                case EpisodeState.BossDead:
                    return HandleDeathReset(hero, boss, "Boss defeated");

                case EpisodeState.Resetting:
                    return HandleResettingState(hero, boss, ref currentAction);

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
            isInResetSequence = false;
            hasTriggeredReset = false;
            Debug.Log("[TrainingEpisodeManager] Episode reset complete, resuming training");
            
            OnResetComplete?.Invoke();
        }

        private bool HandleDeathReset(HeroController hero, HealthManager boss, string reason)
        {
            if (!isInResetSequence)
            {
                isInResetSequence = true;
                hasTriggeredReset = false;
                resetSequenceStartTime = Time.time;
                Debug.Log($"[TrainingEpisodeManager] {reason} - starting automatic reset sequence...");
            }
            
            // Wait for delay, then press reset (F5) and transition to resetting state
            // Delay is necessary because immediate press would often break the game
            if (!hasTriggeredReset && Time.time - resetSequenceStartTime >= resetDelayDuration)
            {
                OnSimulateKeyPress?.Invoke(KeyCode.F5);
                hasTriggeredReset = true;
                CurrentState = EpisodeState.Resetting;
                Debug.Log("[TrainingEpisodeManager] F5 pressed, transitioning to Resetting state");
            }
            
            return true; // Skip normal step processing
        }

        private bool HandleResettingState(HeroController hero, HealthManager boss, ref Action currentAction)
        {
            if (!isInResetSequence)
                return true;

            // Get boss-specific reset action (e.g., move right for Lace)
            currentAction = encounter.GetResetAction(hero, boss);
            
            // Check if reset is complete using boss-specific logic
            if (encounter.IsResetComplete(hero, boss))
            {
                ResetEpisode();
                return false; // Resume normal training
            }
            
            return true; // Continue reset sequence
        }
    }
}

