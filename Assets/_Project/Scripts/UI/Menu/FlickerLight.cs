using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Fire-like light flicker (intensity + range noise).</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField, Range(0f, 1f)] private float amount = 0.25f;
        [SerializeField] private float speed = 6f;

        private Light target;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.value * 100f;
        }

        private void Update()
        {
            float noise = Mathf.PerlinNoise(seed, Time.unscaledTime * speed) * 2f - 1f;
            target.intensity = baseIntensity * (1f + noise * amount);
        }
    }
}
