using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/VortexAttackData", fileName = "VortexAttackData")]
    public class VortexAttackData : EnemyAttackData
    {
        [Min(1)] public int riftsPerPlayer = 1;
        public Vector2 offsetFromPlayer = new(3f, 6f);
        [Min(1f)] public float pullRadius = 9f;
        [Min(0.5f)] public float coreRadius = 2f;
        [Tooltip("Pull speed (m/s) added per second at the edge, stronger near the core.")] public float pullStrength = 9f;
        public float coreDamagePerTick = 6f;
        public DamageType damageType = DamageType.Arcane;
        [Min(0.5f)] public float duration = 4f;
        [Min(0.1f)] public float tick = 0.25f;
        [Min(0f)] public float windup = 0.9f;
        [Min(0f)] public float recovery = 0.5f;
        public GameObject telegraphVfx;
        [Tooltip("Pulsed at each rift, scaled by the core radius.")] public GameObject riftVfx;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
    }
}
