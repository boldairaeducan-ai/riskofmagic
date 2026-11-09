using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RiskOfMagic.Feedback
{
    /// <summary>URP camera setup shared by the gameplay and menu cameras.</summary>
    public static class PostProcessCamera
    {
        /// <summary>Turns post-processing on (volumes of the scene) for a camera rendering to the screen.</summary>
        public static void Enable(Camera camera, bool enabled = true)
        {
            if (camera == null)
                return;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = enabled;
            data.antialiasing = AntialiasingMode.None; // MSAA from the URP asset keeps low-poly edges crisp
            data.stopNaN = enabled;
            data.dithering = enabled; // avoids banding in skies and fog gradients
        }
    }
}
