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
        [SerializeField, Min(1)] private int count = 1;
        [SerializeField, Range(0f, 120f), Tooltip("Horizontal fan of the volley (degrees).")] private float spreadAngle;
        [SerializeField, Tooltip("Projectiles home on the target (ProjectileDefinition.homingTurnRate).")] private bool homing;

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
