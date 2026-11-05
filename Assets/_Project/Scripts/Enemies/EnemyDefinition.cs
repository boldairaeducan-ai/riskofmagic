using RiskOfMagic.Audio;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemy Definition", fileName = "EnemyDefinition")]
    public class EnemyDefinition : ScriptableObject
    {
        public string displayName = "Enemy";
        [Tooltip("French grammatical gender of the name, for elite adjectives (\"Goule ardente\").")] public bool feminineName;
        [Tooltip("Shown in the boss health bar, spawned by the teleporter event.")] public bool isBoss;

        [Header("Stats")]
        [Min(1f)] public float maxHealth = 50f;
        [Min(0f)] public float moveSpeed = 4.5f;
        [Min(0f)] public float acceleration = 20f;
        [Min(0f)] public float angularSpeed = 540f;

        [Range(0f, 1f), Tooltip("Reduces knockback distance and freeze duration (0 = full effect, 1 = immune).")]
        public float controlResistance;

        [Header("Level scaling (per level above 1)")]
        [Min(0f)] public float healthPerLevel = 0.3f;
        [Min(0f)] public float damagePerLevel = 0.2f;

        [Header("Perception")]
        [Min(0f), Tooltip("A player entering this radius is detected and chased until death.")]
        public float detectionRange = 30f;
        public bool requireLineOfSight;
        [Min(0f), Tooltip("Allies within this radius are alerted when this enemy detects or is hit by a player.")]
        public float alertAlliesRadius = 10f;

        [Header("Movement")]
        public EnemyMovementStyle movementStyle = EnemyMovementStyle.Direct;
        [Min(0f), Tooltip("Flank: distance from the player of the approach point.")] public float flankRadius = 6f;
        [Tooltip("Flank: angle (degrees) from the player's facing of the approach point, randomized per enemy and side.")]
        public Vector2 flankAngle = new(100f, 160f);
        [Tooltip("Kite: preferred distance band to the target.")] public Vector2 kiteRange = new(10f, 24f);
        [Min(0f), Tooltip("Kite: how far to back off when the target gets too close.")] public float retreatDistance = 8f;

        [Header("Idle")]
        [Min(0f)] public float wanderRadius = 6f;
        [Range(0f, 1f)] public float wanderSpeedMultiplier = 0.4f;

        [Header("Rewards")]
        [Min(0), Tooltip("Multiplied by the difficulty coefficient at death.")] public int goldReward = 10;

        [Header("Lifecycle")]
        [Min(0f)] public float despawnDelay = 2f;

        [Header("Audio")]
        public SoundEvent spawnSound;
        [Tooltip("Attack start (windup), every peer.")] public SoundEvent attackSound;
        [SoundSlot(Cooldown = 0.08f, MaxSimultaneous = 4)] public SoundEvent hurtSound;
        [SoundSlot(MaxSimultaneous = 4)] public SoundEvent deathSound;
    }
}
