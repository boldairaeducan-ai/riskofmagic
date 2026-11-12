using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    public class EnemyProjectileAttack : EnemyAttack
    {
        private ProjectileAttackData D => data as ProjectileAttackData ?? (ProjectileAttackData)(data = ScriptableObject.CreateInstance<ProjectileAttackData>());

        private ProjectileDefinition projectile => D.projectile;
        private float damage => D.damage;
        private DamageType damageType => D.damageType;
        private float windup => D.windup;
        private float recovery => D.recovery;
        private Vector3 muzzleOffset => D.muzzleOffset;
        private float aimHeight => D.aimHeight;
        private int count => D.count;
        private float spreadAngle => D.spreadAngle;
        private bool homing => D.homing;

        protected override IEnumerator Perform(PlayerAvatar target)
        {
            yield return new WaitForSeconds(windup);
            if (Actor.Health.IsAlive && target != null && target.IsAlive)
            {
                Vector3 origin = transform.TransformPoint(muzzleOffset);
                Vector3 direction = target.transform.position + Vector3.up * aimHeight - origin;
                var info = DamageInfo.Create(damage * Actor.DamageMultiplier, damageType, NetworkObject, Actor.Health.Team, origin, direction);
                for (int i = 0; i < count; i++)
                {
                    float angle = count > 1 ? Mathf.Lerp(-spreadAngle * 0.5f, spreadAngle * 0.5f, i / (count - 1f)) : 0f;
                    Vector3 shot = Quaternion.AngleAxis(angle, Vector3.up) * direction;
                    ProjectileNetwork.FireServer(projectile, origin, shot, 1f, info, NetworkObject,
                        homingTarget: homing ? target.NetworkObject : null);
                }
            }
            yield return new WaitForSeconds(recovery);
        }
    }
}
