using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/MeleeAttackData", fileName = "MeleeAttackData")]
    public class MeleeAttackData : EnemyAttackData
    {
        public float damage = 10f;
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float windup = 0.5f;
        [Min(0f)] public float recovery = 0.4f;
        public Vector3 hitOffset = new(0f, 1f, 1f);
        [Min(0.1f)] public float hitRadius = 1f;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
    }
}
