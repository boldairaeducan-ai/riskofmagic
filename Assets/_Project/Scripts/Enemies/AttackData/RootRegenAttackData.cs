using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/RootRegenAttackData", fileName = "RootRegenAttackData")]
    public class RootRegenAttackData : EnemyAttackData
    {
        public SpawnCard rootCard;
        [Min(1)] public int rootCount = 3;
        public Vector2 rootDistance = new(4f, 7f);
        [Range(0f, 0.2f), Tooltip("Max health healed per second while at least one root lives.")] public float healPerSecond = 0.03f;
        [Min(1f)] public float channelDuration = 8f;
        [Range(0.1f, 1f), Tooltip("Only below this health fraction.")] public float onlyBelowHealth = 0.75f;
        [Min(0f)] public float windup = 0.8f;
        [Min(0f)] public float recovery = 0.6f;
        public GameObject spawnVfx;
        [Tooltip("Pulsed on the boss while it regenerates.")] public GameObject healVfx;
    }
}
