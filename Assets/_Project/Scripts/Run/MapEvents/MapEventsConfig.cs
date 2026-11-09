using System;
using RiskOfMagic.Items;
using UnityEngine;

namespace RiskOfMagic.Run.MapEvents
{
    public enum MapEventType : byte
    {
        PurificationTomb,
        ChallengeAltar,
        CursedChest
    }

    [Serializable]
    public class TombSettings
    {
        public bool enabled = true;
        [Min(0f)] public float weight = 1f;
        [Min(1f)] public float radius = 6f;
        [Min(1f), Tooltip("Seconds with at least one player inside the zone.")] public float purifyDuration = 30f;
        [Min(1f)] public float summonInterval = 8f;
        public Vector2Int summonCount = new(2, 3);
        public Vector2 summonDistance = new(10f, 15f);
        [Header("Reward")]
        [Min(1f)] public float healRadius = 8f;
        [Range(0f, 1f), Tooltip("Fraction of max health healed per second.")] public float healPerSecond = 0.05f;
        [Min(0f)] public float healDuration = 20f;
        [Tooltip("One item per player.")] public RarityWeights reward = new(0f, 75f, 25f, 0f);
    }

    [Serializable]
    public class ChallengeSettings
    {
        public bool enabled = true;
        [Min(0f)] public float weight = 1f;
        [Min(1f), Tooltip("Budget = this × difficulty coefficient (grows with time, stages and players).")]
        public float creditsPerCoefficient = 40f;
        [Min(1f), Tooltip("An elite costs its spawn card cost × this. What the budget can't afford as elites comes as normal enemies.")]
        public float eliteCostMultiplier = 3f;
        [Min(1)] public int maxEnemies = 8;
        [Range(0.2f, 1f), Tooltip("Share of the enemies in the first wave; the rest arrives when half of it is dead.")]
        public float firstWaveShare = 0.6f;
        public Vector2 spawnDistance = new(8f, 14f);
        [Tooltip("One item per player.")] public RarityWeights reward = new(0f, 65f, 35f, 0f);
    }

    [Serializable]
    public class CursedChestSettings
    {
        public bool enabled = true;
        [Min(0f)] public float weight = 1f;
        [Range(1, 95), Tooltip("Max health lost (%) until the end of the stage, stacks with other cursed chests.")]
        public int maxHealthCostPercent = 20;
        public RarityWeights reward = new(0f, 0f, 90f, 10f);
    }

    /// <summary>
    /// Every map event value in one place: Assets/_Project/Resources/MapEventsConfig.asset.
    /// Created once by the prototype builder and never overwritten: edit it freely.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Map Events Config", fileName = "MapEventsConfig")]
    public class MapEventsConfig : ScriptableObject
    {
        public const string ResourcePath = "MapEventsConfig";

        [Tooltip("Random count per stage (inclusive), never twice the same event on a stage.")]
        public Vector2Int eventsPerStage = new(0, 2);
        public TombSettings purificationTomb = new();
        public ChallengeSettings challengeAltar = new();
        public CursedChestSettings cursedChest = new();

        private static MapEventsConfig instance;

        public static MapEventsConfig Instance
        {
            get
            {
                if (instance == null)
                    instance = Resources.Load<MapEventsConfig>(ResourcePath);
                if (instance == null)
                    instance = CreateInstance<MapEventsConfig>(); // defaults
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;

        public bool IsEnabled(MapEventType type) => type switch
        {
            MapEventType.PurificationTomb => purificationTomb.enabled,
            MapEventType.ChallengeAltar => challengeAltar.enabled,
            _ => cursedChest.enabled
        };

        public float GetWeight(MapEventType type) => type switch
        {
            MapEventType.PurificationTomb => purificationTomb.weight,
            MapEventType.ChallengeAltar => challengeAltar.weight,
            _ => cursedChest.weight
        };
    }
}
