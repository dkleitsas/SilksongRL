using UnityEngine;
using System.Collections;

namespace SilksongRL
{
    /// <summary>
    /// MonoBehaviour that updates ScreenCapture cache at end of frame.
    /// Only captures when agent control is enabled.
    /// Need this because frames render in LateUpdate but our agent acts
    /// during FixedUpdate. So we cache previous frame and use that.
    /// This does introduce a one frame delay but that should be negligible.
    /// </summary>
    public class ScreenCaptureUpdater : MonoBehaviour
    {
        private ScreenCapture screenCapture;
        private Coroutine captureCoroutine;

        public void Initialize(ScreenCapture capture)
        {
            screenCapture = capture;
            captureCoroutine = StartCoroutine(CaptureLoop());
        }

        private IEnumerator CaptureLoop()
        {
            while (true)
            {
                yield return new WaitForEndOfFrame();
                
                if (RLManager.isAgentControlEnabled && screenCapture != null)
                {
                    screenCapture.UpdateCache();
                }
            }
        }

        private void OnDestroy()
        {
            if (captureCoroutine != null)
            {
                StopCoroutine(captureCoroutine);
            }
            
            screenCapture?.ClearCache();
        }
    }
}



