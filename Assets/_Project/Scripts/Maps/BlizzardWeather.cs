using RiskOfMagic.Player;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Maps
{
    /// <summary>
    /// Irregular blizzards: fog density (and optional snow particles following the camera) driven by Perlin noise of
    /// the server time + the stage layout seed, so every peer sees the same storm without any replication.
    /// Visual only for now (enemy detection is unchanged). Intensity is public for future gameplay hooks.
    /// </summary>
    public class BlizzardWeather : MonoBehaviour
    {
        [SerializeField] private Color fogColor = new(0.82f, 0.86f, 0.92f);
        [SerializeField, Min(0f)] private float clearDensity = 0.004f;
        [SerializeField, Min(0f)] private float stormDensity = 0.06f;
        [SerializeField, Min(1f), Tooltip("Seconds per noise unit: higher = slower weather changes.")] private float period = 35f;
        [SerializeField, Range(0f, 1f), Tooltip("Noise level where a storm starts (higher = rarer storms).")] private float stormThreshold = 0.55f;
        [SerializeField, Range(0.01f, 0.5f)] private float transition = 0.12f;
        [SerializeField, Tooltip("Optional snow, moved to the camera every frame; emission scales with the intensity.")]
        private ParticleSystem snow;
        [SerializeField, Min(0f)] private float maxSnowRate = 600f;

        public static float Intensity { get; private set; }

        private void OnDisable() => Intensity = 0f;

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            double time = manager != null && manager.IsListening ? manager.ServerTime.Time : Time.timeAsDouble;
            int seed = StageManager.Instance != null ? StageManager.Instance.LayoutSeed : 0;
            float offset = (seed % 10007) * 0.173f;
            float noise = Mathf.PerlinNoise((float)(time / period) + offset, 0.37f);
            Intensity = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(stormThreshold, stormThreshold + transition, noise));

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = Mathf.Lerp(clearDensity, stormDensity, Intensity);

            if (snow == null)
                return;
            var cam = PlayerAvatar.Local != null && PlayerAvatar.Local.Look.Camera != null ? PlayerAvatar.Local.Look.Camera : Camera.main;
            if (cam != null)
                snow.transform.position = cam.transform.position + Vector3.up * 6f;
            var emission = snow.emission;
            emission.rateOverTime = maxSnowRate * (0.1f + 0.9f * Intensity);
        }
    }
}
