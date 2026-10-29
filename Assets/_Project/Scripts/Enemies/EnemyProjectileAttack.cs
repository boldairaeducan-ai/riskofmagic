using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    public class EnemyProjectileAttack : EnemyAttack
    {
        [SerializeField] private ProjectileDefinition projectile;
        [SerializeField] private float damage = 8f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField, Min(0f)] private float windup = 0.7f;
        [SerializeField, Min(0f)] private float recovery = 0.3f;
        [SerializeField] private Vector3 muzzleOffset = new(0f, 1.4f, 0.6f);
        [SerializeField] private float aimHeight = 1f;

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            yield return new WaitForSeconds(windup);
            if (Actor.Health.IsAlive && target != null && target.IsAlive)
            {
                Vector3 origin = transform.TransformPoint(muzzleOffset);
                Vector3 direction = target.transform.position + Vector3.up * aimHeight - origin;
                var info = DamageInfo.Create(damage * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team, origin, direction);
                ProjectileNetwork.FireServer(projectile, origin, direction, 1f, info, NetworkObject);
            }
            yield return new WaitForSeconds(recovery);
        }
    }
}
