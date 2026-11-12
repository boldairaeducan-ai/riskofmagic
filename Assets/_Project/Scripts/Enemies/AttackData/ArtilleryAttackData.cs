using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/ArtilleryAttackData", fileName = "ArtilleryAttackData")]
    public class ArtilleryAttackData : EnemyAttackData
    {
        public ProjectileDefinition projectile;
        public float damage = 14f;
        public DamageType damageType = DamageType.Fire;
        [Min(0f), Tooltip("Too close: the brain backs off instead.")] public float minRange = 5f;
        [Min(0f)] public float windup = 0.8f;
        [Min(0.2f)] public float flightTime = 1.1f;
        [Min(0f)] public float recovery = 0.6f;
        public Vector3 muzzleOffset = new(0f, 1.8f, 0.3f);
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        [Tooltip("Ground marker, replicated via VfxNetwork, scaled by the explosion radius.")] public GameObject telegraphVfx;
    }
}
