using RiskOfMagic.Settings;
using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>
    /// Local camera juice: trauma-based shake (scaled by the Camera Shake option) and temporary FOV kicks.
    /// Lives on the player camera (child of the pitch pivot); owns the camera's field of view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraEffects : MonoBehaviour
    {
        [Header("Shake")]
        [SerializeField] private float maxAngle = 3.5f;
        [SerializeField] private float maxOffset = 0.06f;
        [SerializeField] private float frequency = 22f;
        [SerializeField, Tooltip("Trauma lost per second.")] private float traumaDecay = 1.6f;

        [Header("FOV kick")]
        [SerializeField] private float fovKickSharpness = 12f;

        private Camera cam;
        private float trauma;
        private float fovKick;
        private float fovKickTarget;
        private float fovKickUntil;
        private float seed;

        public float BaseFieldOfView { get; set; } = 80f;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            BaseFieldOfView = cam.fieldOfView;
            seed = Random.value * 100f;
        }

        /// <summary>0..1, accumulates; the shake grows with trauma².</summary>
        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        /// <summary>Widens the FOV by <paramref name="degrees"/> for <paramref name="duration"/> seconds (dashes...).</summary>
        public void KickFov(float degrees, float duration)
        {
            fovKickTarget = Time.time < fovKickUntil ? Mathf.Max(fovKickTarget, degrees) : degrees;
            fovKickUntil = Mathf.Max(fovKickUntil, Time.time + duration);
        }

        private void LateUpdate()
        {
            trauma = Mathf.Max(0f, trauma - traumaDecay * Time.deltaTime);
            float shake = trauma * trauma * Mathf.Clamp01(GameSettings.CameraShake);
            float t = Time.time * frequency;
            transform.localRotation = Quaternion.Euler(
                maxAngle * shake * Noise(t, 0f),
                maxAngle * shake * Noise(t, 1f),
                maxAngle * 0.5f * shake * Noise(t, 2f));
            transform.localPosition = new Vector3(Noise(t, 3f), Noise(t, 4f), 0f) * (maxOffset * shake);

            float target = Time.time < fovKickUntil ? fovKickTarget : 0f;
            fovKick = Mathf.Lerp(fovKick, target, 1f - Mathf.Exp(-fovKickSharpness * Time.deltaTime));
            cam.fieldOfView = BaseFieldOfView + fovKick;
        }

        private float Noise(float t, float channel) => Mathf.PerlinNoise(seed + channel * 10f, t) * 2f - 1f;
    }
}
