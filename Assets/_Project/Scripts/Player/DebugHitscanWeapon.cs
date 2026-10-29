using System;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Temporary primary attack used to test the damage pipeline until the skill system lands.
    /// Client sends aim, server validates and resolves the hit.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class DebugHitscanWeapon : NetworkBehaviour
    {
        [SerializeField] private SkillSlot slot = SkillSlot.Primary;
        [SerializeField] private float damage = 12f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0.1f)] private float shotsPerSecond = 4f;
        [SerializeField] private float range = 100f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField, Tooltip("Max distance between the claimed aim origin and the server-side position.")]
        private float maxOriginError = 4f;

        private readonly RaycastHit[] hits = new RaycastHit[16];
        private PlayerAvatar avatar;
        private float nextLocalShot;
        private float nextServerShot;

        private void Awake() => avatar = GetComponent<PlayerAvatar>();

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !avatar.ControlEnabled || !avatar.Look.IsCursorLocked)
                return;
            if (!avatar.Input.IsSkillHeld(slot) || Time.time < nextLocalShot)
                return;

            nextLocalShot = Time.time + 1f / shotsPerSecond;
            Ray ray = avatar.Look.AimRay;
            Debug.DrawRay(ray.origin, ray.direction * range, Color.yellow, 0.1f);
            FireRpc(ray.origin, ray.direction);
        }

        [Rpc(SendTo.Server)]
        private void FireRpc(Vector3 origin, Vector3 direction)
        {
            if (!avatar.IsAlive || Time.time < nextServerShot)
                return;
            if ((origin - transform.position).sqrMagnitude > maxOriginError * maxOriginError)
                return;
            nextServerShot = Time.time + 0.8f / shotsPerSecond; // tolerance for latency jitter

            direction.Normalize();
            int count = Physics.RaycastNonAlloc(origin, direction, hits, range, hitMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, 0, count, RaycastHitDistanceComparer.Instance);

            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(transform))
                    continue;
                if (CombatUtility.TryGetDamageable(hit.collider, out var target) && target.IsAlive)
                    target.ApplyDamage(DamageInfo.Create(damage, damageType, NetworkObject, avatar.Health.Team, hit.point, direction));
                return; // first non-self hit blocks the shot
            }
        }
    }
}
