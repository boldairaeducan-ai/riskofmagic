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

        [Header("Recoil / landing dip / cast flash")]
        [SerializeField, Tooltip("Recoil recovery speed.")] private float recoilRecovery = 14f;
        [SerializeField, Tooltip("Landing dip recovery speed.")] private float dipRecovery = 9f;
        [SerializeField] private Color flashColor = new(1f, 0.85f, 0.6f);
        [SerializeField] private float flashRange = 7f;

        private float recoilPitch;
        private float recoilYaw;
        private float dip;
        private float dipVelocity;
        private Light flashLight;
        private float flashIntensity;

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

        /// <summary>Weapon kick: the view jumps up by <paramref name="degrees"/> (a bit sideways), then recovers.</summary>
        public void Recoil(float degrees)
        {
            recoilPitch = Mathf.Min(recoilPitch + degrees, degrees * 2.5f);
            recoilYaw += Random.Range(-0.35f, 0.35f) * degrees;
        }

        /// <summary>Landing: the camera dips down by <paramref name="meters"/> then springs back.</summary>
        public void Dip(float meters) => dipVelocity -= meters * 14f;

        /// <summary>Short light at the hands (casts, shots). Color = null: warm white.</summary>
        public void Flash(float intensity, Color? color = null)
        {
            if (flashLight == null)
            {
                var go = new GameObject("CastFlash");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0.25f, -0.25f, 0.8f);
                flashLight = go.AddComponent<Light>();
                flashLight.type = LightType.Point;
                flashLight.range = flashRange;
                flashLight.shadows = LightShadows.None;
                flashLight.intensity = 0f;
            }
            flashLight.color = color ?? flashColor;
            flashIntensity = Mathf.Max(flashIntensity, intensity);
        }

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
            // Recoil (kick up, recover) and landing dip (damped spring) on top of the shake.
            float dt = Time.deltaTime;
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));
            dipVelocity += (-dip * 160f - dipVelocity * dipRecovery * 2f) * dt;
            dip += dipVelocity * dt;
            transform.localRotation = Quaternion.Euler(-recoilPitch, recoilYaw, 0f) * transform.localRotation;
            transform.localPosition = new Vector3(Noise(t, 3f), Noise(t, 4f), 0f) * (maxOffset * shake) + Vector3.up * dip;
            if (flashLight != null)
            {
                flashIntensity = Mathf.MoveTowards(flashIntensity, 0f, dt * 40f);
                flashLight.intensity = flashIntensity;
                flashLight.enabled = flashIntensity > 0.01f;
            }

            float target = Time.time < fovKickUntil ? fovKickTarget : 0f;
            fovKick = Mathf.Lerp(fovKick, target, 1f - Mathf.Exp(-fovKickSharpness * Time.deltaTime));
            cam.fieldOfView = BaseFieldOfView + fovKick;
        }

        private float Noise(float t, float channel) => Mathf.PerlinNoise(seed + channel * 10f, t) * 2f - 1f;
    }
}
