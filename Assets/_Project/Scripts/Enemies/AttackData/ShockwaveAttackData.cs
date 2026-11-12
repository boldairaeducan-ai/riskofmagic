using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/ShockwaveAttackData", fileName = "ShockwaveAttackData")]
    public class ShockwaveAttackData : EnemyAttackData
    {
        [Min(0f), Tooltip("Cosmetic leap during the windup, landing on the slam (0 = no leap).")] public float jumpHeight;
        [Range(0f, 0.6f), Tooltip("Share of the windup spent crouching before the leap.")] public float crouchShare = 0.25f;
        public float slamDamage = 30f;
        [Min(0f)] public float slamRadius = 4f;
        [Min(0f)] public float windup = 1f;
        [Min(0f)] public float recovery = 0.8f;
        public float waveDamage = 20f;
        [Min(1f)] public float waveMaxRadius = 25f;
        [Min(0.1f), Tooltip("Must match the ExpandingRing VFX speed.")] public float waveSpeed = 14f;
        [Min(0.1f)] public float waveThickness = 1.5f;
        [Min(0f), Tooltip("Players higher than this above the ground are jumping over the wave.")] public float jumpClearance = 0.5f;
        public DamageType damageType = DamageType.Physical;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
        public LayerMask groundMask = Physics.DefaultRaycastLayers;
        public GameObject telegraphVfx;
        public GameObject slamVfx;
        [Tooltip("ExpandingRing prefab, scaled by the max radius.")] public GameObject waveVfx;
    }
}
