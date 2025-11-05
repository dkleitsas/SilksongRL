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

        private APIClient apiClient;
        private float stepInterval = 0.1f;

        private static IBossEncounter currentEncounter;
        private TrainingEpisodeManager episodeManager;

        private float[] previousObservations;
        private Action previousAction;
        private bool hasPreviousStep = false;

        private bool isProcessingStep = false;

        public static Action currentAction = new Action();

        private float lastStepTime = 0f;

        private void Awake()
        {
            Logger.LogInfo("RL Test Mod loaded.");
            var harmony = new Harmony("com.yourname.hktestmod");
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

            Logger.LogInfo($"[RL] Initialized with encounter: {currentEncounter.GetEncounterName()}");
            Logger.LogInfo($"[RL] Observation size: {currentEncounter.GetObservationSize()}");
            
            _ = InitializeAPIAsync();
        }

        private async Task InitializeAPIAsync()
        {
            try
            {
                string bossName = currentEncounter.GetEncounterName();
                int obsSize = currentEncounter.GetObservationSize();
                
                Logger.LogInfo($"[RL] Initializing API for boss: {bossName} with observation size: {obsSize}");
                
                var response = await apiClient.InitializeAsync(bossName, obsSize);
                
                if (response != null && response.initialized)
                {
                    Logger.LogInfo($"[RL] API initialized successfully. Checkpoint loaded: {response.checkpoint_loaded}");
                }
                else
                {
                    Logger.LogError("[RL] API initialization failed!");
                }
            }
            catch (Exception e)
            {
                Logger.LogError($"[RL] Error initializing API: {e.Message}");
            }
        }

        private void Update()
        {
            // Toggle control when pressing P
            if (Input.GetKeyDown(KeyCode.P))
            {
                isAgentControlEnabled = !isAgentControlEnabled;
                
                Logger.LogInfo($"[RL] Agent control {(isAgentControlEnabled ? "enabled" : "disabled")}. Hero: {(Hero != null ? "Found" : "Not found")}, Boss: {(Boss != null ? "Found" : "Not found")}");
            }

            // Handle reset (F5) press timing
            if (simulateF5Press && Time.time - f5PressTime > f5PressDuration)
            {
                simulateF5Press = false;
                Logger.LogInfo("[RL] F5 press completed");
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
            episodeManager.UpdateEpisodeState(Hero, Boss);

            // Handle reset sequence if needed
            if (episodeManager.HandleResetSequence(Hero, Boss, ref currentAction))
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
                    Logger.LogWarning("[RL] Hero is null - waiting for hero to spawn");
                    return;
                }
                if (Boss == null)
                {
                    Logger.LogWarning("[RL] Boss is null - waiting for boss to spawn");
                    return;
                }
                
                float[] currentObservations = currentEncounter.ExtractObservationArray(Hero, Boss);
                if (currentObservations == null)
                {
                    Logger.LogWarning("[RL] Observations are null despite Hero and Boss being set");
                    return;
                }

                // Store transition from previous step (if it exists)
                if (hasPreviousStep && previousObservations != null)
                {
                    float reward = currentEncounter.CalculateReward(previousObservations, currentObservations);
                    bool done = episodeManager.IsEpisodeDone();

                    await apiClient.StoreTransitionAsync(previousObservations, previousAction, reward, currentObservations, done);
                }

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
                Logger.LogError($"[RL] Error in StepRL: {e.Message}");
            }
            finally
            {
                isProcessingStep = false;
            }
        }


        private void ResetRL()
        {
            currentAction = new Action();
            previousObservations = null;
            previousAction = null;
            hasPreviousStep = false;
            isProcessingStep = false;
        }

        // Static flag for F5 simulation
        public static bool simulateF5Press = false;
        private float f5PressTime = 0f;
        private float f5PressDuration = 0.2f; // How long to simulate the press

        private void SimulateKeyPress(KeyCode key)
        {
            if (key == KeyCode.F5)
            {
                simulateF5Press = true;
                f5PressTime = Time.time;
                Logger.LogInfo("[RL] Simulating F5 key press");
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
                Debug.Log("[RLManager] Hero found and assigned (Harmony patch)");
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
                    Debug.Log($"[RLManager] Boss locked: {__instance.name} (Harmony patch)");
                }
            }
        }
    }
}
