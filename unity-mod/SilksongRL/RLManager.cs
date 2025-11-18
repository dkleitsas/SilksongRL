using BepInEx;
using HarmonyLib;
using UnityEngine;
using System;
using System.Threading.Tasks;
using HutongGames.PlayMaker.Actions;

namespace SilksongRL
{
    [BepInPlugin("com.jimmie.silksongrl", "SilksongRL", "1.0.0")]
    public class RLManager : BaseUnityPlugin
    {
        public static bool isTraining = true;
        public static string targetBossScene = "Lace_1";
        public static bool isAgentControlEnabled = false;

        // Hero and Boss references (tracked via Harmony patches)
        public static HeroController Hero { get; private set; }
        public static HealthManager Boss { get; private set; }
        
        // Static logger reference for use in Harmony patches and other classes
        public static BepInEx.Logging.ManualLogSource StaticLogger;

        private APIClient apiClient;
        private float stepInterval = 0.1f;

        private static IBossEncounter currentEncounter;
        private TrainingEpisodeManager episodeManager;

        private float[] previousObservations;
        private Action previousAction;
        private bool hasPreviousStep = false;
        private bool pendingDoneTransition = false; // Set when episode ends, cleared after storing final transition
        private int who_dead = -1; // 0: Hornet, 1: Boss (same use as above ^^^)

        private bool isProcessingStep = false;

        public static Action currentAction = new Action();

        private float lastStepTime = 0f;

        private void Awake()
        {
            StaticLogger = Logger;
            StaticLogger.LogInfo("SilksongRL Mod loaded.");
            var harmony = new Harmony("com.jimmie.silksongrl");
            harmony.PatchAll();
            
            APIConfig config = new APIConfig
            {
                BaseUrl = "http://localhost:8000",
                Timeout = 10f,
                MaxRetries = 3,
                RetryDelay = 1f
            };
            apiClient = new APIClient(config);

            // Initialize encounter-specific components (This is still hardcoded, will change later)
            currentEncounter = new LaceEncounter();
            episodeManager = new TrainingEpisodeManager(currentEncounter);
            
            episodeManager.OnSimulateKeyPress = SimulateKeyPress;
            episodeManager.OnResetComplete = ResetRL;

            StaticLogger.LogInfo($"[RL] Initialized with encounter: {currentEncounter.GetEncounterName()}");
            StaticLogger.LogInfo($"[RL] Observation size: {currentEncounter.GetObservationSize()}");
            
            _ = InitializeAPIAsync();
        }

        private async Task InitializeAPIAsync()
        {
            try
            {
                string bossName = currentEncounter.GetEncounterName();
                int obsSize = currentEncounter.GetObservationSize();
                
                StaticLogger.LogInfo($"[RL] Initializing API for boss: {bossName} with observation size: {obsSize}");
                
                var response = await apiClient.InitializeAsync(bossName, obsSize);
                
                if (response != null && response.initialized)
                {
                    StaticLogger.LogInfo($"[RL] API initialized successfully. Checkpoint loaded: {response.checkpoint_loaded}");
                }
                else
                {
                    StaticLogger.LogError("[RL] API initialization failed!");
                }
            }
            catch (Exception e)
            {
                StaticLogger.LogError($"[RL] Error initializing API: {e.Message}");
            }
        }

        private void Update()
        {
            // Toggle control when pressing P
            if (Input.GetKeyDown(KeyCode.P))
            {
                isAgentControlEnabled = !isAgentControlEnabled;
                
                StaticLogger.LogInfo($"[RL] Agent control {(isAgentControlEnabled ? "enabled" : "disabled")}. Hero: {(Hero != null ? "Found" : "Not found")}, Boss: {(Boss != null ? "Found" : "Not found")}");
            }
        }

        private void FixedUpdate()
        {

            if (!isAgentControlEnabled)
            {
                currentAction = new Action();
                return;
            }

            // Ensure currentAction is never null
            if (currentAction == null)
            {
                currentAction = new Action();
            }

            // Update episode state (death detection, etc.)
            var previousState = episodeManager.CurrentState;
            episodeManager.UpdateEpisodeState(Hero, Boss);
            
            // If we just transitioned to a death state, mark that we need to store a done transition
            if (previousState == TrainingEpisodeManager.EpisodeState.Training && 
                (episodeManager.CurrentState == TrainingEpisodeManager.EpisodeState.HeroDead || 
                 episodeManager.CurrentState == TrainingEpisodeManager.EpisodeState.BossDead))
            {
                pendingDoneTransition = true;
                who_dead = (episodeManager.CurrentState == TrainingEpisodeManager.EpisodeState.HeroDead) ? 0 : 1;
                StaticLogger.LogInfo($"[RL] Episode ended - will store final transition with done=true");
            }

            // Handle reset sequence if needed
            if (episodeManager.HandleResetSequence(Hero, Boss))
            {
                return; // Skip normal step processing during reset
            }

            // Step on a **frame independent** fixed time interval 
            if (Time.fixedTime - lastStepTime >= stepInterval)
            {
                lastStepTime = Time.fixedTime;
                _ = StepRLAsync();
            }
        }

        private async Task StepRLAsync()
        {
            if (isProcessingStep) return;

            isProcessingStep = true;

            try
            {
                if (Hero == null)
                {
                    StaticLogger.LogWarning("[RL] Hero is null - waiting for hero to spawn");
                    return;
                }
                if (Boss == null)
                {
                    StaticLogger.LogWarning("[RL] Boss is null - waiting for boss to spawn");
                    return;
                }
                
                float[] currentObservations = currentEncounter.ExtractObservationArray(Hero, Boss);
                if (currentObservations == null)
                {
                    StaticLogger.LogWarning("[RL] Observations are null despite Hero and Boss being set");
                    return;
                }

                // Store transition from previous step (if it exists)
                if (hasPreviousStep && previousObservations != null)
                {
                    float reward = currentEncounter.CalculateReward(previousObservations, currentObservations, who_dead);
                    bool done = pendingDoneTransition;

                    await apiClient.StoreTransitionAsync(previousObservations, previousAction, reward, currentObservations, done);
                    
                    // If this was a terminal transition, clear previous step data and don't get new action
                    if (done)
                    {
                        StaticLogger.LogInfo($"[RL] Stored final transition with done=true");
                        previousObservations = null;
                        previousAction = null;
                        hasPreviousStep = false;
                        pendingDoneTransition = false;
                        who_dead = -1;
                        return; // Don't get new action, we're in reset
                    }
                }

                // Only get new action if we're in normal training (not handling a done transition)
                Action action = await apiClient.GetActionAsync(currentObservations);

                if (action != null)
                {
                    currentAction = action;

                    previousObservations = currentObservations;
                    previousAction = action;
                    hasPreviousStep = true;
                }
            }
            catch (Exception e)
            {
                StaticLogger.LogError($"[RL] Error in StepRL: {e.Message}");
            }
            finally
            {
                isProcessingStep = false;
            }
        }


        private void ResetRL()
        {
            // Clear current action and processing flag
            // Note: We DON'T clear previousObservations/previousAction/hasPreviousStep here
            // because we need to store the final transition with done=true on the first step of the new episode
            currentAction = new Action();
            isProcessingStep = false;
        }

        // Static flag for F5 simulation
        public static bool simulateF5Press = false;

        private void SimulateKeyPress(KeyCode key)
        {
            if (key == KeyCode.F5)
            {
                simulateF5Press = true;
                StaticLogger.LogInfo("[RL] Simulating F5 key press");
            }
        }

        /// <summary>
        /// Harmony patch to automatically catch Hero spawns.
        /// </summary>
        [HarmonyPatch(typeof(HeroController), "Awake")]
        public static class HeroController_Awake_Patch
        {
            static void Postfix(HeroController __instance)
            {
                Hero = __instance;
                StaticLogger.LogInfo("[RL] Hero found and assigned (Harmony patch)");
            }
        }

        /// <summary>
        /// Harmony patch to automatically catch Boss spawns.
        /// </summary>
        [HarmonyPatch(typeof(HealthManager), "Awake")]
        public static class HealthManager_Awake_Patch
        {
            static void Postfix(HealthManager __instance)
            {
                // Only assign if we have an encounter configured and this matches
                if (currentEncounter != null && currentEncounter.IsEncounterMatch(__instance))
                {
                    Boss = __instance;
                    StaticLogger.LogInfo($"[RL] Boss locked: {__instance.name} (Harmony patch)");
                }
            }
        }
    }
}
