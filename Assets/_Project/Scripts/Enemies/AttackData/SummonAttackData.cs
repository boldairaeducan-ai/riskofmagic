using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Run;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    [CreateAssetMenu(menuName = "Risk of Magic/Enemies/SummonAttackData", fileName = "SummonAttackData")]
    public class SummonAttackData : EnemyAttackData
    {
        public SpawnCard minion;
        [Tooltip("Optional: random card per minion instead of `minion`.")] public SpawnCard[] minionPool = new SpawnCard[0];
        [Min(1)] public int countPerCast = 4;
        [Min(1)] public int maxAlive = 8;
        public Vector2 spawnDistance = new(2f, 5f);
        [Min(0f)] public float windup = 1f;
        [Min(0f)] public float recovery = 0.5f;
        [Tooltip("Spawned at each minion (VfxNetwork).")] public GameObject spawnVfx;
        [Range(0.05f, 1f), Tooltip("Only cast below this health fraction (1 = always).")] public float onlyBelowHealth = 1f;
        [Min(0), Tooltip("Max casts per life (0 = unlimited).")] public int maxCasts;
    }
}
