using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>Owner-only reactions of the camera to combat: shake when hit (scaled by the damage).</summary>
    [RequireComponent(typeof(Health))]
    public class LocalCombatFeedback : MonoBehaviour
    {
        [SerializeField, Tooltip("Trauma for a hit worth 100% of max health.")] private float traumaPerMaxHealth = 2f;
        [SerializeField] private float minTrauma = 0.12f;
        [SerializeField] private float maxTraumaPerHit = 0.6f;

        private Health health;
        private PlayerAvatar avatar;

        private void Awake()
        {
            health = GetComponent<Health>();
            avatar = GetComponent<PlayerAvatar>();
        }

        private void OnEnable() => health.Damaged += OnDamaged;
        private void OnDisable() => health.Damaged -= OnDamaged;

        private void OnDamaged(DamageInfo info)
        {
            if (avatar == null || !avatar.IsOwner || avatar.Look.Effects == null || info.IgnoreModifiers)
                return;
            float ratio = health.Max > 0f ? info.Amount / health.Max : 0f;
            avatar.Look.Effects.AddTrauma(Mathf.Clamp(ratio * traumaPerMaxHealth, minTrauma, maxTraumaPerHit));
        }
    }
}
