using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run
{
    /// <summary>
    /// Server: run and stage lifecycle on the current map.
    /// Places the teleporter, starts the next stage when it is used, detects the game over (every player dead)
    /// and restarts the run on request.
    /// </summary>
    public class StageManager : NetworkBehaviour
    {
        [SerializeField] private Teleporter teleporterPrefab;
        [SerializeField, Min(0f)] private float teleporterMinDistanceFromSpawns = 45f;

        private readonly NetworkVariable<int> stageNumber = new(1);
        private readonly NetworkVariable<bool> gameOver = new();
        private readonly NetworkVariable<float> finalTime = new();
        private readonly NetworkVariable<int> kills = new();
        private Teleporter teleporter;

        public static StageManager Instance { get; private set; }

        /// <summary>Every peer: (stage number, 1-based).</summary>
        public event Action<int> StageStarted;
        /// <summary>Every peer: (is game over).</summary>
        public event Action<bool> GameOverChanged;

        public int StageNumber => stageNumber.Value;
        public bool IsGameOver => gameOver.Value;
        /// <summary>Run duration frozen at the game over.</summary>
        public float FinalTime => finalTime.Value;
        public int Kills => kills.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            stageNumber.OnValueChanged += OnStageChanged;
            gameOver.OnValueChanged += OnGameOverChanged;
            if (!IsServer)
                return;
            EnemyActor.Killed += OnEnemyKilled;
            SpawnTeleporter();
        }

        public override void OnNetworkDespawn()
        {
            stageNumber.OnValueChanged -= OnStageChanged;
            gameOver.OnValueChanged -= OnGameOverChanged;
            EnemyActor.Killed -= OnEnemyKilled;
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (IsServer && IsSpawned && !gameOver.Value && AllPlayersDead())
                TriggerGameOver();
        }

        // ---------------------------------------------------------------- Run (server)

        /// <summary>Server only: teleporter used.</summary>
        public void AdvanceStage()
        {
            if (!IsServer || gameOver.Value)
                return;
            if (RunDifficulty.Instance != null)
                RunDifficulty.Instance.CompleteStage();
            foreach (var player in PlayerAvatar.All)
                player.Wallet.ResetGold(); // gold doesn't carry over between stages
            ResetStage(fullHeal: false);
            stageNumber.Value++;
        }

        /// <summary>Server only: new run after a game over (or any time, e.g. from a debug menu).</summary>
        public void RestartRun()
        {
            if (!IsServer)
                return;
            if (RunDifficulty.Instance != null)
                RunDifficulty.Instance.ResetRun();
            foreach (var player in PlayerAvatar.All)
            {
                player.Inventory.Clear();
                player.Wallet.ResetGold();
            }

            ResetStage(fullHeal: true);
            stageNumber.Value = 1;
            kills.Value = 0;
            finalTime.Value = 0f;
            gameOver.Value = false;
        }

        private void TriggerGameOver()
        {
            finalTime.Value = RunDifficulty.Instance != null ? RunDifficulty.Instance.ElapsedSeconds : 0f;
            gameOver.Value = true;
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.Paused = true;
        }

        private static bool AllPlayersDead()
        {
            if (PlayerAvatar.All.Count == 0)
                return false;
            foreach (var player in PlayerAvatar.All)
                if (player.IsAlive)
                    return false;
            return true;
        }

        private void ResetStage(bool fullHeal)
        {
            DespawnAll(new List<EnemyActor>(EnemyActor.Active));
            DespawnAll(new List<ItemPickup>(ItemPickup.Active));
            if (teleporter != null && teleporter.IsSpawned)
                teleporter.NetworkObject.Despawn();

            var interactables = GetComponent<InteractableSpawner>();
            if (interactables != null)
                interactables.Respawn();

            foreach (var player in PlayerAvatar.All)
            {
                if (!player.IsAlive)
                    player.Health.Revive(1f);
                else if (fullHeal)
                    player.Health.Heal(player.Health.Max);
                player.TeleportToSpawnRpc();
            }

            SpawnTeleporter();
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.ResetForStage();
        }

        private void SpawnTeleporter()
        {
            var bounds = StageBounds.Instance;
            if (teleporterPrefab == null || bounds == null)
            {
                Debug.LogError("[StageManager] Missing teleporter prefab or StageBounds.", this);
                return;
            }

            if (!bounds.TryGetRandomPoint(out var position, teleporterMinDistanceFromSpawns, attempts: 60) &&
                !bounds.TryGetRandomPoint(out position, 0f, attempts: 60))
            {
                Debug.LogError("[StageManager] No valid position for the teleporter.", this);
                return;
            }

            teleporter = Instantiate(teleporterPrefab, position, Quaternion.identity);
            teleporter.NetworkObject.Spawn(true);
        }

        private static void DespawnAll<T>(List<T> objects) where T : NetworkBehaviour
        {
            foreach (var item in objects)
                if (item != null && item.IsSpawned)
                    item.NetworkObject.Despawn();
        }

        private void OnEnemyKilled(EnemyActor enemy, DamageInfo killingBlow)
        {
            if (!gameOver.Value)
                kills.Value++;
        }

        private void OnStageChanged(int previous, int current) => StageStarted?.Invoke(current);
        private void OnGameOverChanged(bool previous, bool current) => GameOverChanged?.Invoke(current);
    }
}
