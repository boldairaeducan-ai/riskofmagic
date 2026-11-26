using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Feedback
{
    /// <summary>
    /// Owner-only camera juice: shake when hit (scaled by the damage), landing dip + dust (scaled by the fall speed),
    /// wider field of view while sprinting.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class LocalCombatFeedback : MonoBehaviour
    {
        [SerializeField, Tooltip("Trauma for a hit worth 100% of max health.")] private float traumaPerMaxHealth = 2f;
        [SerializeField] private float minTrauma = 0.12f;
        [SerializeField] private float maxTraumaPerHit = 0.6f;
        [SerializeField, Tooltip("Landings slower than this (m/s) are ignored.")] private float minLandingSpeed = 6f;
        [SerializeField] private float sprintFov = 6f;

        private Health health;
        private PlayerAvatar avatar;
        private PlayerMotor motor;

        private void Awake()
        {
            health = GetComponent<Health>();
            avatar = GetComponent<PlayerAvatar>();
            motor = GetComponent<PlayerMotor>();
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            if (motor != null)
                motor.Landed += OnLanded;
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            if (motor != null)
                motor.Landed -= OnLanded;
        }

        private void Update()
        {
            if (avatar != null && avatar.IsOwner && avatar.Look.Effects != null && motor != null && motor.IsSprinting)
                avatar.Look.Effects.KickFov(sprintFov, 0.15f); // held while sprinting, eases out after
        }

        private void OnLanded()
        {
            if (avatar == null || !avatar.IsOwner || avatar.Look.Effects == null)
                return;
            float speed = motor.LastLandingSpeed;
            if (speed < minLandingSpeed)
                return;
            float k = Mathf.InverseLerp(minLandingSpeed, 30f, speed);
            avatar.Look.Effects.Dip(Mathf.Lerp(0.05f, 0.22f, k));
            if (k > 0.5f)
                avatar.Look.Effects.AddTrauma(Mathf.Lerp(0.05f, 0.2f, k));
            var slot = RiskOfMagic.Core.ArtLibrary.Juice?.landingDust;
            if (slot != null && slot.IsSet)
                slot.Spawn(transform.position, Quaternion.identity, Mathf.Lerp(1f, 1.5f, k));
            else
                HitSparks.Burst(transform.position + Vector3.up * 0.1f, Vector3.up, new Color(0.75f, 0.7f, 0.62f), Mathf.RoundToInt(Mathf.Lerp(6f, 18f, k)), 0.6f, 1.4f);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (avatar == null || !avatar.IsOwner || avatar.Look.Effects == null || info.IgnoreModifiers)
                return;
            float ratio = health.Max > 0f ? info.Amount / health.Max : 0f;
            avatar.Look.Effects.AddTrauma(Mathf.Clamp(ratio * traumaPerMaxHealth, minTrauma, maxTraumaPerHit));
        }
    }
}
