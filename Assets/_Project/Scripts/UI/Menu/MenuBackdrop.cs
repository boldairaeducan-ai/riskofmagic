using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// 3D set behind the main menu (Prefabs/Menu/MenuBackdrop.prefab, yours to dress: never overwritten).
    /// Applies its own atmosphere (skybox, fog, ambient) and drifts the camera slowly around a focus point,
    /// with a light parallax following the mouse.
    /// </summary>
    public class MenuBackdrop : MonoBehaviour
    {
        [Header("Camera")]
        [SerializeField] private Camera backdropCamera;
        [SerializeField, Tooltip("Point the camera looks at.")] private Transform focus;
        [SerializeField, Tooltip("Degrees of slow back-and-forth orbit around the focus.")] private float swayAngle = 6f;
        [SerializeField, Tooltip("Seconds for a full back-and-forth.")] private float swayPeriod = 40f;
        [SerializeField, Tooltip("Meters the camera shifts with the mouse.")] private Vector2 parallax = new(0.35f, 0.2f);
        [SerializeField] private float parallaxSmoothing = 3f;

        [Header("Atmosphere")]
        [SerializeField, Tooltip("Optional.")] private Material skybox;
        [SerializeField] private Color backgroundColor = new(0.05f, 0.05f, 0.09f);
        [SerializeField] private Color ambientColor = new(0.16f, 0.17f, 0.25f);
        [SerializeField] private bool fog = true;
        [SerializeField] private Color fogColor = new(0.07f, 0.07f, 0.12f);
        [SerializeField] private float fogDensity = 0.035f;

        private Vector3 basePosition;
        private Vector2 smoothedPointer;
        private Material previousSkybox;
        private bool previousFog;

        private void OnEnable()
        {
            if (backdropCamera != null)
            {
                RiskOfMagic.Feedback.PostProcessCamera.Enable(backdropCamera);
                basePosition = backdropCamera.transform.position;
                backdropCamera.clearFlags = skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
                backdropCamera.backgroundColor = backgroundColor;
            }
            previousSkybox = RenderSettings.skybox;
            previousFog = RenderSettings.fog;
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambientColor;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        private void OnDisable()
        {
            RenderSettings.skybox = previousSkybox;
            RenderSettings.fog = previousFog;
        }

        private void LateUpdate()
        {
            if (backdropCamera == null)
                return;
            Vector2 pointer = Vector2.zero;
            var mouse = Mouse.current;
            if (mouse != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = mouse.position.ReadValue();
                pointer = new Vector2(p.x / Screen.width - 0.5f, p.y / Screen.height - 0.5f) * 2f;
            }
            smoothedPointer = Vector2.Lerp(smoothedPointer, pointer, 1f - Mathf.Exp(-parallaxSmoothing * Time.unscaledDeltaTime));

            Vector3 center = focus != null ? focus.position : basePosition + backdropCamera.transform.forward * 10f;
            float angle = swayPeriod > 0f ? Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / swayPeriod) * swayAngle : 0f;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (basePosition - center);
            var t = backdropCamera.transform;
            Vector3 right = Vector3.Cross(Vector3.up, (center - basePosition).normalized).normalized;
            t.position = center + offset + right * (-smoothedPointer.x * parallax.x) + Vector3.up * (smoothedPointer.y * parallax.y);
            t.LookAt(center);
        }
    }
}
