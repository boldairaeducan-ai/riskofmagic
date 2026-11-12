using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/ProjectileAttackData", fileName = "ProjectileAttackData")]
    public class ProjectileAttackData : EnemyAttackData
    {
        public ProjectileDefinition projectile;
        public float damage = 8f;
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float windup = 0.7f;
        [Min(0f)] public float recovery = 0.3f;
        public Vector3 muzzleOffset = new(0f, 1.4f, 0.6f);
        public float aimHeight = 1f;
        [Min(1)] public int count = 1;
        [Range(0f, 120f), Tooltip("Horizontal fan of the volley (degrees).")] public float spreadAngle;
        [Tooltip("Projectiles home on the target (ProjectileDefinition.homingTurnRate).")] public bool homing;
    }
}
