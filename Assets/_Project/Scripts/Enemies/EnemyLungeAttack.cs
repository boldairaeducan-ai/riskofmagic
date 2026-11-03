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
        [SerializeField, Min(0f), Tooltip("Too close: the brain keeps approaching instead.")] private float minRange;
        [SerializeField] private float damage = 10f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0f)] private float windup = 0.45f;
        [SerializeField, Min(0.1f)] private float dashSpeed = 18f;
        [SerializeField, Min(0.05f)] private float dashDuration = 0.35f;
        [SerializeField, Min(0f)] private float recovery = 0.6f;
        [SerializeField, Min(0.1f)] private float hitRadius = 1.3f;
        [SerializeField] private float hitHeight = 0.9f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

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
