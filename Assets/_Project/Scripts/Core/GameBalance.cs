using System;
using UnityEngine;

namespace RiskOfMagic.Core
{
    /// <summary>
    /// Global game balance (Resources/GameBalance.asset, created once by the builder, never overwritten): run difficulty,
    /// combat director, player experience, status effects and regular enemy multipliers. Read live by the systems:
    /// changes apply in Play mode. Missing asset = these code defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Game Balance", fileName = "GameBalance")]
    public class GameBalance : ScriptableObject
    {
        public const string ResourcePath = "GameBalance";

        [Serializable]
        public class DifficultyBalance
        {
            public float scalingPerMinute = 0.1f;
            public float playerFactorPerPlayer = 0.3f;
            public float stageMultiplier = 1.08f;
            [Min(1f), Tooltip("Coefficient at the very start of a run (×player factor): stage 1 starts busy.")] public float startingCoefficient = 1.25f;
            public float levelStep = 0.33f;
            public int maxEnemyLevel = 99;
            public float hardDirectorCreditMultiplier = 1.3f;
            public float hardDetectionMultiplier = 1.25f;
            [Tooltip("Enemy variety unlocks as if the run were this much further.")] public float hardUnlockMultiplier = 1.2f;
        }

        [Serializable]
        public class DirectorBalance
        {
            public float creditMultiplier = 1f;
            public float startingCredits = 40f;
            [Min(0f), Tooltip("Credits spent on idle enemies at stage start (scaled by sqrt(difficulty coefficient)).")] public float stagePopulationCredits = 110f;
            [Min(0f)] public float populationMinDistanceFromSpawns = 45f;
            [Min(0f)] public float populationMinSpacing = 6f;
            [Tooltip("Outside events, wave enemies know a player immediately instead of waiting for detection.")] public bool wavesHuntPlayers;
            public Vector2 waveInterval = new(8f, 15f);
            [Min(1)] public int maxSpawnsPerWave = 4;
            [Min(1)] public int maxAliveEnemies = 22;
            [Min(0f), Tooltip("Run progress (like SpawnCard.minCoefficient) from which waves can be elite.")] public float eliteMinProgress = 1.5f;
            [Range(0f, 1f), Tooltip("Chance for a wave to be elite (same affix), if the boosted cost is affordable.")] public float eliteWaveChance = 0.35f;
            [Tooltip("Normal waves: distance band to the NEAREST living player.")] public Vector2 farSpawnDistance = new(45f, 90f);
            [Tooltip("Event waves (teleporter): distance band around a random living player.")] public float minSpawnDistance = 25f;
            public float maxSpawnDistance = 50f;
        }

        [Serializable]
        public class ExperienceBalance
        {
            [Min(0f), Tooltip("Experience per point of gold reward.")] public float experiencePerGold = 0.6f;
            [Min(1f), Tooltip("Experience to go from level 1 to 2.")] public float firstLevelExperience = 35f;
            [Min(1.01f), Tooltip("Each level needs this much more experience than the previous one.")] public float levelGrowth = 1.65f;
            [Min(1)] public int maxLevel = 99;
        }

        [Serializable]
        public class StatusBalance
        {
            public float curseDamageDealtMultiplier = 0.5f;
            public float curseSpeedMultiplier = 0.9f;
            public float curseDamageTakenMultiplier = 1.3f;
            public float chillSpeedMultiplier = 0.7f;
            public float channelingSpeedMultiplier = 0.5f;
            public float bannerDamageBonus = 0.2f;
            public float stealthSpeedBonus = 0.3f;
            public float markedDamageTakenMultiplier = 1.5f;
            public float runeBlessingDamageBonus = 0.3f;
            public float rallyBonus = 0.4f;
            public float warhornBonusPerStack = 0.15f;
        }

        [Serializable]
        public class EnemyBalance
        {
            [Min(0.1f), Tooltip("Damage multiplier of regular (non-boss) enemies.")] public float regularDamageMultiplier = 1.15f;
            [Min(0.1f), Tooltip("Attack rate multiplier of regular enemies (cooldowns divided by it).")] public float regularAttackRateMultiplier = 1.3f;
            [Min(0.1f), Tooltip("Damage multiplier of bosses (teleporter, secret, final).")] public float bossDamageMultiplier = 1f;
            [Min(0.1f), Tooltip("Attack rate multiplier of bosses.")] public float bossAttackRateMultiplier = 1f;
        }

        [Tooltip("Run difficulty: coefficient = (player factor × starting coefficient + minutes × scaling) × stage multiplier^stages.")] public DifficultyBalance difficulty = new();
        [Tooltip("Combat director: credits, stage population, waves, elites, placement.")] public DirectorBalance director = new();
        [Tooltip("Player levels (experience = gold reward × experiencePerGold).")] public ExperienceBalance experience = new();
        [Tooltip("Status effect strengths (players and enemies).")] public StatusBalance statuses = new();
        [Tooltip("Global multipliers on top of each enemy's attack data.")] public EnemyBalance enemies = new();

        private static GameBalance instance;

        public static GameBalance Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<GameBalance>(ResourcePath);
                    if (instance == null)
                        instance = CreateInstance<GameBalance>();
                }
                return instance;
            }
        }
    }
}
