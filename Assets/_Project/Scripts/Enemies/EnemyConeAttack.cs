using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Channelled cone (breath): telegraphed windup, then damage ticks in a cone in front of the enemy for a duration.
    /// The enemy turns slowly while channelling (TrackingFactor): strafing around it is the counterplay.
    /// </summary>
    public class EnemyConeAttack : EnemyAttack
    {
        [SerializeField, Range(5f, 180f)] private float angle = 50f;
        [SerializeField] private float damagePerTick = 6f;
        [SerializeField] private DamageType damageType = DamageType.Fire;
        [SerializeField] private DotEffect dot;
        [SerializeField, Min(0f)] private float windup = 0.8f;
        [SerializeField, Min(0.1f)] private float duration = 2f;
        [SerializeField, Min(0.05f)] private float tickInterval = 0.25f;
        [SerializeField, Min(0f)] private float recovery = 0.8f;
        [SerializeField, Range(0f, 1f)] private float trackingFactor = 0.12f;
        [SerializeField] private float originHeight = 1.2f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;
        [SerializeField, Tooltip("Ground marker during the windup (VfxNetwork).")] private GameObject telegraphVfx;
        [SerializeField, Tooltip("Spawned along the cone each tick (VfxNetwork).")] private GameObject tickVfx;

        private readonly List<Health> targets = new();

        public override float TrackingFactor => IsAttacking ? trackingFactor : 1f;

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            VfxNetwork.Spawn(telegraphVfx, transform.position + transform.forward * (range * 0.5f), Quaternion.identity, range * 0.4f);
            yield return new WaitForSeconds(windup);

            for (float elapsed = 0f; elapsed < duration; elapsed += tickInterval)
            {
                if (!Actor.Health.IsAlive)
                    yield break;
                Tick();
                yield return new WaitForSeconds(tickInterval);
            }
            yield return new WaitForSeconds(recovery);
        }

        private void Tick()
        {
            Vector3 origin = transform.position + Vector3.up * originHeight;
            Vector3 forward = transform.forward;
            VfxNetwork.Spawn(tickVfx, origin + forward * (range * Random.Range(0.35f, 0.9f)), Quaternion.identity, 1f + range * 0.08f);

            TargetFinder.FindHostiles(origin, range, Actor.Health.Team, targets, hitMask);
            float cos = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
            foreach (var health in targets)
            {
                Vector3 offset = TargetFinder.Center(health) - origin;
                if (Mathf.Abs(offset.y) > maxHeightDifference + originHeight)
                    continue;
                Vector3 flat = new(offset.x, 0f, offset.z);
                if (flat.sqrMagnitude > 0.01f && Vector3.Dot(flat.normalized, forward) < cos)
                    continue;
                var info = DamageInfo.Create(damagePerTick * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team,
                    TargetFinder.Center(health), offset.normalized);
                health.ApplyDamage(info);
                if (dot != null)
                    DotSystem.Apply(health, dot, info);
            }
        }
    }
}
