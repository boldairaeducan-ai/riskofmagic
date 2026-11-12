using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/MeteorAttackData", fileName = "MeteorAttackData")]
    public class MeteorAttackData : EnemyAttackData
    {
        public ProjectileDefinition meteor;
        public float damage = 45f;
        public DamageType damageType = DamageType.Fire;
        [Min(0f)] public float windup = 0.6f;
        [Min(0.3f), Tooltip("Time between the ground marker and the impact.")] public float fallTime = 1.6f;
        [Min(0)] public int extraMeteorsPerPlayer = 2;
        public Vector2 extraOffset = new(5f, 11f);
        [Min(5f)] public float fallHeight = 35f;
        [Min(0f)] public float recovery = 0.6f;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public GameObject telegraphVfx;
    }
}
