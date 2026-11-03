using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Shows a visual (bubble, aura...) while the Health is shielded, on every peer: pops in when raised and
    /// pulses each time it absorbs a hit.
    /// </summary>
    public class HealthShieldVisual : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private GameObject visual;
        [SerializeField, Min(0.01f)] private float popDuration = 0.15f;
        [SerializeField] private float hitPulseScale = 0.15f;
        [SerializeField, Min(0.01f)] private float hitPulseDuration = 0.2f;

        private Vector3 baseScale = Vector3.one;
        private float shownAt = float.NegativeInfinity;
        private float hitAt = float.NegativeInfinity;

        private void Awake()
        {
            if (health == null)
                health = GetComponentInParent<Health>();
            if (visual != null)
            {
                baseScale = visual.transform.localScale;
                visual.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (health != null)
                health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (health != null)
                health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (health.IsShielded)
                hitAt = Time.time;
        }

        private void Update()
        {
            if (visual == null)
                return;
            bool shielded = health != null && health.IsShielded;
            if (visual.activeSelf != shielded)
            {
                visual.SetActive(shielded);
                shownAt = Time.time;
            }
            if (!shielded)
                return;

            float pop = Mathf.Clamp01((Time.time - shownAt) / popDuration);
            float pulse = 1f + hitPulseScale * Mathf.Clamp01(1f - (Time.time - hitAt) / hitPulseDuration);
            visual.transform.localScale = baseScale * (Mathf.Lerp(0.5f, 1f, pop) * pulse);
        }
    }
}
