using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>Shows a visual (bubble, aura...) while the Health is shielded. Runs on every peer.</summary>
    public class HealthShieldVisual : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private GameObject visual;

        private void Awake()
        {
            if (health == null)
                health = GetComponentInParent<Health>();
            if (visual != null)
                visual.SetActive(false);
        }

        private void Update()
        {
            bool shielded = health != null && health.IsShielded;
            if (visual != null && visual.activeSelf != shielded)
                visual.SetActive(shielded);
        }
    }
}
