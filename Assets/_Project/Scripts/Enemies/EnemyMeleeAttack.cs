using System.Collections;
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

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            yield return new WaitForSeconds(windup);
            if (Actor.Health.IsAlive)
            {
                Vector3 center = transform.TransformPoint(hitOffset);
                var info = DamageInfo.Create(damage * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team, center, transform.forward);
                AreaDamage.Apply(center, hitRadius, info, hitMask);
            }
            yield return new WaitForSeconds(recovery);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.TransformPoint(hitOffset), hitRadius);
        }
    }
}
