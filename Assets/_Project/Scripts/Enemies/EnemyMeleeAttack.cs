using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    public class EnemyMeleeAttack : EnemyAttack
    {
        [SerializeField] private float damage = 10f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0f)] private float windup = 0.5f;
        [SerializeField, Min(0f)] private float recovery = 0.4f;
        [SerializeField] private Vector3 hitOffset = new(0f, 1f, 1f);
        [SerializeField, Min(0.1f)] private float hitRadius = 1f;
        [SerializeField] private LayerMask hitMask = Physics.DefaultRaycastLayers;

        private readonly Collider[] overlaps = new Collider[16];
        private readonly HashSet<Health> alreadyHit = new();

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            yield return new WaitForSeconds(windup);
            if (Actor.Health.IsAlive)
                DealDamage();
            yield return new WaitForSeconds(recovery);
        }

        private void DealDamage()
        {
            Vector3 center = transform.TransformPoint(hitOffset);
            int count = Physics.OverlapSphereNonAlloc(center, hitRadius, overlaps, hitMask, QueryTriggerInteraction.Ignore);
            alreadyHit.Clear();

            for (int i = 0; i < count; i++)
            {
                if (!CombatUtility.TryGetDamageable(overlaps[i], out var target))
                    continue;
                if (target.Team == Actor.Health.Team || !target.IsAlive || !alreadyHit.Add(target.Health))
                    continue;

                Vector3 point = overlaps[i].ClosestPoint(center);
                target.ApplyDamage(DamageInfo.Create(damage, damageType, NetworkObject, Actor.Health.Team, point, transform.forward));
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(hitOffset), hitRadius);
        }
    }
}
