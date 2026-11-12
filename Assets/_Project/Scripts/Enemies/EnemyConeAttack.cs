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
        private ConeAttackData D => data as ConeAttackData ?? (ConeAttackData)(data = ScriptableObject.CreateInstance<ConeAttackData>());

        private float angle => D.angle;
        private float damagePerTick => D.damagePerTick;
        private DamageType damageType => D.damageType;
        private DotEffect dot => D.dot;
        private bool applyStatus => D.applyStatus;
        private StatusType status => D.status;
        private float statusDuration => D.statusDuration;
        private float windup => D.windup;
        private float duration => D.duration;
        private float tickInterval => D.tickInterval;
        private float recovery => D.recovery;
        private float trackingFactor => D.trackingFactor;
        private float originHeight => D.originHeight;
        private LayerMask hitMask => D.hitMask;
        private GameObject telegraphVfx => D.telegraphVfx;
        private GameObject tickVfx => D.tickVfx;

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
                if (applyStatus && health.TryGetComponent(out StatusEffects effects))
                    effects.Apply(status, statusDuration);
            }
        }
    }
}
