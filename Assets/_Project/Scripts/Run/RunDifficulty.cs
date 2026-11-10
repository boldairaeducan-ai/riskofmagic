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
        [SerializeField] private float scalingPerMinute = 0.1f;
        [SerializeField] private float playerFactorPerPlayer = 0.3f;
        [SerializeField] private float stageMultiplier = 1.08f;
        [SerializeField, Min(1f), Tooltip("Coefficient at the very start of a run (×player factor): stage 1 starts busy.")]
        private float startingCoefficient = 1.25f;
        [SerializeField] private float levelStep = 0.33f;
        [SerializeField] private int maxEnemyLevel = 99;

        [Header("Hard mode (time scaling is already x1.5 through the difficulty value)")]
        [SerializeField] private float hardDirectorCreditMultiplier = 1.3f;
        [SerializeField] private float hardDetectionMultiplier = 1.25f;
        [SerializeField, Tooltip("Enemy variety unlocks as if the run were this much further.")] private float hardUnlockMultiplier = 1.2f;

        private readonly NetworkVariable<double> runStartTime = new();
        /// <summary>Server time when the solo pause started (0 = running): the run clock is frozen meanwhile.</summary>
        private readonly NetworkVariable<double> pausedAt = new();
        private readonly NetworkVariable<int> stagesCompleted = new();
        private readonly NetworkVariable<DifficultyLevel> difficulty = new(DifficultyLevel.Normal);

        public static RunDifficulty Instance { get; private set; }
        public static float CurrentCoefficient => Instance != null && Instance.IsSpawned ? Instance.Coefficient : 1f;
        public static float CurrentDirectorCreditMultiplier => IsHard ? Instance.hardDirectorCreditMultiplier : 1f;
        public static float CurrentDetectionMultiplier => IsHard ? Instance.hardDetectionMultiplier : 1f;
        public static float CurrentUnlockMultiplier => IsHard ? Instance.hardUnlockMultiplier : 1f;
        private static bool IsHard => Instance != null && Instance.IsSpawned && Instance.difficulty.Value == DifficultyLevel.Hard;

        public static int CurrentEnemyLevel => Instance != null && Instance.IsSpawned ? Instance.EnemyLevel : 1;

        public DifficultyLevel Difficulty => difficulty.Value;
        public int StagesCompleted => stagesCompleted.Value;
        public float ElapsedSeconds => IsSpawned
            ? Mathf.Max(0f, (float)((pausedAt.Value > 0d ? pausedAt.Value : NetworkManager.ServerTime.Time) - runStartTime.Value))
            : 0f;

        /// <summary>Server only (solo pause): freezes the run clock; the paused duration is not counted.</summary>
        public void SetPaused(bool paused)
        {
            if (!IsServer || paused == pausedAt.Value > 0d)
                return;
            double now = NetworkManager.ServerTime.Time;
            if (paused)
            {
                pausedAt.Value = now;
                return;
            }
            runStartTime.Value += now - pausedAt.Value;
            pausedAt.Value = 0d;
        }
        public float PlayerFactor => 1f + playerFactorPerPlayer * (Mathf.Max(1, PlayerAvatar.All.Count) - 1);

        public float Coefficient
        {
            get
            {
                float playerFactor = PlayerFactor;
                float timeFactor = scalingPerMinute * (int)difficulty.Value * Mathf.Pow(playerFactor, 0.2f);
                float stageFactor = Mathf.Pow(stageMultiplier, stagesCompleted.Value);
                return (playerFactor * startingCoefficient + ElapsedSeconds / 60f * timeFactor) * stageFactor;
            }
        }

        /// <summary>Coefficient without the player-count factor: 1 at the start of a run whatever the number of players.</summary>
        public float Progress => Coefficient / PlayerFactor;
        public static float CurrentProgress => Instance != null && Instance.IsSpawned ? Instance.Progress : 1f;

        public int EnemyLevel => Mathf.Clamp(1 + Mathf.FloorToInt((Coefficient - PlayerFactor) / levelStep), 1, maxEnemyLevel);

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer)
                return;
            runStartTime.Value = NetworkManager.ServerTime.Time;
            difficulty.Value = RiskOfMagic.Flow.RunSettings.Difficulty ?? startingDifficulty;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Server only: restarts the run clock and stage count.</summary>
        public void ResetRun()
        {
            if (!IsServer)
                return;
            runStartTime.Value = NetworkManager.ServerTime.Time;
            pausedAt.Value = 0d;
            stagesCompleted.Value = 0;
        }

        /// <summary>Server only.</summary>
        public void CompleteStage()
        {
            if (IsServer)
                stagesCompleted.Value++;
        }
    }
}
