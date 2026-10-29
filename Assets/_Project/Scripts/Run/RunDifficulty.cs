using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run
{
    public enum DifficultyLevel : byte
    {
        Easy = 1,
        Normal = 2,
        Hard = 3
    }

    /// <summary>
    /// Run-wide difficulty scaling (RoR2 formula): the coefficient grows with time, player count and stages,
    /// and drives enemy level, director credits, gold rewards and costs.
    /// </summary>
    public class RunDifficulty : NetworkBehaviour
    {
        [SerializeField] private DifficultyLevel startingDifficulty = DifficultyLevel.Normal;
        [SerializeField] private float scalingPerMinute = 0.0506f;
        [SerializeField] private float playerFactorPerPlayer = 0.3f;
        [SerializeField] private float stageMultiplier = 1.15f;
        [SerializeField] private float levelStep = 0.33f;
        [SerializeField] private int maxEnemyLevel = 99;

        private readonly NetworkVariable<double> runStartTime = new();
        private readonly NetworkVariable<int> stagesCompleted = new();
        private readonly NetworkVariable<DifficultyLevel> difficulty = new(DifficultyLevel.Normal);

        public static RunDifficulty Instance { get; private set; }
        public static float CurrentCoefficient => Instance != null && Instance.IsSpawned ? Instance.Coefficient : 1f;
        public static int CurrentEnemyLevel => Instance != null && Instance.IsSpawned ? Instance.EnemyLevel : 1;

        public DifficultyLevel Difficulty => difficulty.Value;
        public int StagesCompleted => stagesCompleted.Value;
        public float ElapsedSeconds => IsSpawned ? Mathf.Max(0f, (float)(NetworkManager.ServerTime.Time - runStartTime.Value)) : 0f;
        public float PlayerFactor => 1f + playerFactorPerPlayer * (Mathf.Max(1, PlayerAvatar.All.Count) - 1);

        public float Coefficient
        {
            get
            {
                float playerFactor = PlayerFactor;
                float timeFactor = scalingPerMinute * (int)difficulty.Value * Mathf.Pow(playerFactor, 0.2f);
                float stageFactor = Mathf.Pow(stageMultiplier, stagesCompleted.Value);
                return (playerFactor + ElapsedSeconds / 60f * timeFactor) * stageFactor;
            }
        }

        public int EnemyLevel => Mathf.Clamp(1 + Mathf.FloorToInt((Coefficient - PlayerFactor) / levelStep), 1, maxEnemyLevel);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer)
                return;
            runStartTime.Value = NetworkManager.ServerTime.Time;
            difficulty.Value = startingDifficulty;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Server only.</summary>
        public void CompleteStage()
        {
            if (IsServer)
                stagesCompleted.Value++;
        }
    }
}
