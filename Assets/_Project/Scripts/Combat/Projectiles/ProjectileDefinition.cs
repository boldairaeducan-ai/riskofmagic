using RiskOfMagic.Audio;
using UnityEngine;

namespace RiskOfMagic.Combat.Projectiles
{
    /// <summary>Must be registered in the GameCatalog to be replicated.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Projectile", fileName = "Projectile")]
    public class ProjectileDefinition : ScriptableObject
    {
        [Header("Visuals")]
        public GameObject visualPrefab;
        public GameObject impactVfx;

        [Header("Audio")]
        public SoundEvent fireSound;
        public SoundEvent impactSound;

        [Header("Motion")]
        [Min(0f)] public float speed = 60f;
        public float gravity;
        [Min(0.05f)] public float lifetime = 3f;
        [Min(0f), Tooltip("Max travelled distance (m) before vanishing/exploding. 0 = lifetime only.")] public float maxRange;
        [Min(0f)] public float radius = 0.15f;
        [Min(0f), Tooltip("Degrees per second toward the homing target (0 = no homing).")] public float homingTurnRate;

        [Header("Hits")]
        [Min(0)] public int pierceCount;
        [Min(0f)] public float explosionRadius;
        public bool explodeOnExpire;
        public LayerMask hitMask = Physics.DefaultRaycastLayers;
        [Tooltip("Optional damage over time applied by direct hits and the explosion.")] public DotEffect dot;
        [Min(0f), Tooltip("Freezes enemies hit (direct hit and explosion), seconds.")] public float freezeDuration;
        [Tooltip("Direct hits from behind the target are guaranteed criticals.")] public bool backstabCritical;
        [Min(1f), Tooltip("Damage multiplier of a critical direct hit (2 = the normal critical).")] public float criticalMultiplier = 2f;

        [Header("Seals (Paladin)")]
        [Tooltip("A direct hit detonates the target's seals (SealSystem) before dealing its damage.")] public bool detonateSeals;
        [Min(0f), Tooltip("Extra damage per seal, in the attacker's damage.")] public float sealDamageCoefficient = 0.8f;
        [Min(0f), Tooltip("Heal per seal for the players around the target, fraction of their max health.")] public float sealHealFraction = 0.05f;
        [Min(0f)] public float sealHealRadius = 8f;
        [Tooltip("Replicated effect at the detonation (must be in the GameCatalog effects).")] public GameObject sealVfx;
    }
}
