using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    public class EnemyMeleeAttack : EnemyAttack
    {
        private MeleeAttackData D => data as MeleeAttackData ?? (MeleeAttackData)(data = ScriptableObject.CreateInstance<MeleeAttackData>());

        private float damage => D.damage;
        private DamageType damageType => D.damageType;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private Vector3 hitOffset => D.hitOffset;
        private float hitRadius => D.hitRadius;
        private LayerMask hitMask => D.hitMask;

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
