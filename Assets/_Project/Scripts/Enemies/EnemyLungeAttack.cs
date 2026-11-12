using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Enemies
{
    /// <summary>
    /// Gap closer: telegraphed windup, then a straight dash damaging everything it touches once.
    /// Short/fast = wolf bound, long/heavy = ogre charge. Punishes backpedaling.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyLungeAttack : EnemyAttack
    {
        private LungeAttackData D => data as LungeAttackData ?? (LungeAttackData)(data = ScriptableObject.CreateInstance<LungeAttackData>());

        private float minRange => D.minRange;
        private float damage => D.damage;
        private DamageType damageType => D.damageType;
        private float windup => D.windup;
        private float dashSpeed => D.dashSpeed;
        private float dashDuration => D.dashDuration;
        private float recovery => D.recovery;
        private float hitRadius => D.hitRadius;
        private float hitHeight => D.hitHeight;
        private LayerMask hitMask => D.hitMask;

        private readonly HashSet<Health> alreadyHit = new();
        private NavMeshAgent agent;

        protected override void Awake()
        {
            base.Awake();
            agent = GetComponent<NavMeshAgent>();
        }

        public override bool IsInRange(Transform target)
        {
            if (!base.IsInRange(target))
                return false;
            Vector3 offset = target.position - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude >= minRange * minRange;
        }

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            yield return new WaitForSeconds(windup);
            if (!Actor.Health.IsAlive || target == null)
                yield break;

            Vector3 direction = target.transform.position - transform.position;
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            alreadyHit.Clear();

            for (float elapsed = 0f; elapsed < dashDuration && Actor.Health.IsAlive; elapsed += Time.deltaTime)
            {
                if (agent.enabled && agent.isOnNavMesh)
                    agent.Move(direction * (dashSpeed * Time.deltaTime));
                Vector3 center = transform.position + Vector3.up * hitHeight + direction * 0.5f;
                var info = DamageInfo.Create(damage * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team, center, direction);
                AreaDamage.Apply(center, hitRadius, info, hitMask, alreadyHit);
                yield return null;
            }

            yield return new WaitForSeconds(recovery);
        }
    }
}
