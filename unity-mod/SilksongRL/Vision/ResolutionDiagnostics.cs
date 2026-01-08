using UnityEngine;
using UnityEngine.Rendering;
using BepInEx.Logging;

namespace SilksongRL
{
    /// <summary>
    /// Diagnostic utility to log resolution and rendering settings.
    /// Helps ensure consistent screen captures across different machines.
    /// Note that running on a certain aspect ratio will cause issues for
    /// an agent trained on a different one. It will need to be retrained.
    /// </summary>
    public static class ResolutionDiagnostics
    {
        /// <summary>
        /// Logs comprehensive resolution and rendering information.
        /// Call this during initialization to verify capture consistency.
        /// </summary>
        public static void LogResolutionInfo(ManualLogSource logger)
        {
            logger.LogInfo("=== Resolution Diagnostics ===");
            
            logger.LogInfo($"Screen.width x height: {Screen.width}x{Screen.height}");
            logger.LogInfo($"Screen.currentResolution: {Screen.currentResolution.width}x{Screen.currentResolution.height} @ {Screen.currentResolution.refreshRate}Hz");
            
            logger.LogInfo($"Display.main.systemWidth x systemHeight: {Display.main.systemWidth}x{Display.main.systemHeight}");
            
            logger.LogInfo($"Screen.dpi: {Screen.dpi}");
            
            logger.LogInfo($"Screen.fullScreenMode: {Screen.fullScreenMode}");
            logger.LogInfo($"Screen.fullScreen: {Screen.fullScreen}");
            
            float widthScale = ScalableBufferManager.widthScaleFactor;
            float heightScale = ScalableBufferManager.heightScaleFactor;
            logger.LogInfo($"ScalableBufferManager.widthScaleFactor: {widthScale}");
            logger.LogInfo($"ScalableBufferManager.heightScaleFactor: {heightScale}");
            
            if (widthScale < 1.0f || heightScale < 1.0f)
            {
                int actualRenderWidth = (int)(Screen.width * widthScale);
                int actualRenderHeight = (int)(Screen.height * heightScale);
                logger.LogWarning($"[!] Dynamic resolution ACTIVE - actual render: {actualRenderWidth}x{actualRenderHeight}");
            }
            
            var cam = Camera.main;
            if (cam != null)
            {
                logger.LogInfo($"Camera.main.pixelWidth x pixelHeight: {cam.pixelWidth}x{cam.pixelHeight}");
                logger.LogInfo($"Camera.main.allowDynamicResolution: {cam.allowDynamicResolution}");
                logger.LogInfo($"Camera.main.targetTexture: {(cam.targetTexture != null ? cam.targetTexture.name + $" ({cam.targetTexture.width}x{cam.targetTexture.height})" : "null (rendering to screen)")}");
            }
            else
            {
                logger.LogInfo("Camera.main: null (not yet available)");
            }
            
            logger.LogInfo($"QualitySettings.currentLevel: {QualitySettings.names[QualitySettings.GetQualityLevel()]} (index {QualitySettings.GetQualityLevel()})");
            
            logger.LogInfo($"QualitySettings.vSyncCount: {QualitySettings.vSyncCount}");
            
            logger.LogInfo($"Application.targetFrameRate: {Application.targetFrameRate}");
            
            var currentRP = GraphicsSettings.currentRenderPipeline;
            if (currentRP != null)
            {
                logger.LogInfo($"Render Pipeline: {currentRP.name} ({currentRP.GetType().Name})");
            }
            else
            {
                logger.LogInfo("Render Pipeline: Built-in (Legacy)");
            }
            
            logger.LogInfo("=== End Resolution Diagnostics ===");
        }

        /// <summary>
        /// Checks if the current resolution setup might cause inconsistent captures.
        /// Returns true if there are potential issues.
        /// </summary>
        public static bool CheckForPotentialIssues(ManualLogSource logger)
        {
            bool hasIssues = false;
            
            if (ScalableBufferManager.widthScaleFactor < 1.0f || 
                ScalableBufferManager.heightScaleFactor < 1.0f)
            {
                logger.LogWarning("[ResolutionDiagnostics] Dynamic resolution is active - captures may differ from native resolution!");
                hasIssues = true;
            }
            
            if (Screen.width != Display.main.systemWidth || 
                Screen.height != Display.main.systemHeight)
            {
                logger.LogInfo($"[ResolutionDiagnostics] Game resolution ({Screen.width}x{Screen.height}) differs from display ({Display.main.systemWidth}x{Display.main.systemHeight})");
            }
            
            var cam = Camera.main;
            if (cam != null && cam.allowDynamicResolution)
            {
                logger.LogWarning("[ResolutionDiagnostics] Camera.main.allowDynamicResolution is enabled!");
                hasIssues = true;
            }
            
            if (Screen.dpi > 0 && Screen.dpi > 150)
            {
                logger.LogInfo($"[ResolutionDiagnostics] High DPI detected ({Screen.dpi}) - ensure OS scaling isn't affecting capture");
            }
            
            return hasIssues;
        }
    }
}

