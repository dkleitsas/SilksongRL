using UnityEngine;

namespace SilksongRL
{
    /// <summary>
    /// Interface for boss encounter configurations.
    /// Each boss encounter implements this to define its specific behavior,
    /// observation space, action space, and reset mechanics.
    /// </summary>
    public interface IBossEncounter
    {
        /// <summary>
        /// Gets the human-readable name of this encounter.
        /// </summary>
        string GetEncounterName();

        /// <summary>
        /// Gets the action space type for this encounter.
        /// Some bosses might require more actions to be beatable.
        /// </summary>
        ActionSpaceType GetActionSpaceType();

        /// <summary>
        /// Checks if the given HealthManager matches this encounter.
        /// </summary>
        bool IsEncounterMatch(HealthManager hm);

        /// <summary>
        /// Extracts observations from the current game state.
        /// This allows each encounter to define its own observation space
        /// (e.g., base observations, projectiles, summons, environmental hazards).
        /// </summary>
        float[] ExtractObservationArray(HeroController hero, HealthManager boss);

        /// <summary>
        /// Returns the size of the observation array for this encounter.
        /// Must match the length of the array returned by ExtractObservationArray().
        /// </summary>
        int GetObservationSize();

        /// <summary>
        /// Checks if the boss is in a dormant/inactive state.
        /// Used during reset sequences to determine when the fight can resume.
        /// </summary>
        bool IsBossDormant(HealthManager boss);

        /// <summary>
        /// Returns the action the hero should take during reset to initiate the fight.
        /// </summary>
        Action GetResetAction(HeroController hero, HealthManager boss);

        /// <summary>
        /// Checks if the reset sequence is complete and training can resume.
        /// </summary>
        bool IsResetComplete(HeroController hero, HealthManager boss);

        /// <summary>
        /// Calculates the reward for the current transition.
        /// Each encounter can define its own reward function.
        /// </summary>
        float CalculateReward(float[] previousObservations, float[] currentObservations, int who_dead);

        /// <summary>
        /// Checks if the hero is stuck.
        /// Conditions are arena/boss specific.
        /// Might not be required for all encounters.
        /// </summary>
        bool IsHeroStuck(float heroY, float heroX);
    }
}

