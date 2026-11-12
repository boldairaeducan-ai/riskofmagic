using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/HauntAttackData", fileName = "HauntAttackData")]
    public class HauntAttackData : EnemyAttackData
    {
        [Min(0.5f)] public float radius = 3.5f;
        [Min(0f)] public float speed = 3.2f;
        [Min(0.5f)] public float duration = 8f;
        [Min(0.1f)] public float tick = 0.4f;
        public float damagePerTick = 4f;
        public DamageType damageType = DamageType.Arcane;
        [Min(0.1f)] public float curseDuration = 2f;
        [Min(0f)] public float windup = 0.7f;
        [Min(0f)] public float recovery = 0.4f;
        [Tooltip("Pulsed at the zone every tick, scaled by the radius.")] public GameObject zoneVfx;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
    }
}
