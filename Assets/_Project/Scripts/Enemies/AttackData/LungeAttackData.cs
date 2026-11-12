using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/LungeAttackData", fileName = "LungeAttackData")]
    public class LungeAttackData : EnemyAttackData
    {
        [Min(0f), Tooltip("Too close: the brain keeps approaching instead.")] public float minRange;
        public float damage = 10f;
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float windup = 0.45f;
        [Min(0.1f)] public float dashSpeed = 18f;
        [Min(0.05f)] public float dashDuration = 0.35f;
        [Min(0f)] public float recovery = 0.6f;
        [Min(0.1f)] public float hitRadius = 1.3f;
        public float hitHeight = 0.9f;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
    }
}
