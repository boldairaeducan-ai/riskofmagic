using System;
using System.Collections.Generic;
using RiskOfMagic.Enemies;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Run
{
    /// <summary>
    /// Server: stage lifecycle on the current map. Places the teleporter and, when it is used, starts the next
    /// stage (difficulty step, cleanup, new chests/enemies/teleporter, players revived and sent back to spawn).
    /// </summary>
    public class StageManager : NetworkBehaviour
    {
        [SerializeField] private Teleporter teleporterPrefab;
        [SerializeField, Min(0f)] private float teleporterMinDistanceFromSpawns = 45f;

        private readonly NetworkVariable<int> stageNumber = new(1);
        private Teleporter teleporter;

        public static StageManager Instance { get; private set; }

        /// <summary>Every peer: (stage number, 1-based).</summary>
        public event Action<int> StageStarted;

        public int StageNumber => stageNumber.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            stageNumber.OnValueChanged += OnStageChanged;
            if (IsServer)
                SpawnTeleporter();
        }

        public override void OnNetworkDespawn()
        {
            stageNumber.OnValueChanged -= OnStageChanged;
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Server only.</summary>
        public void AdvanceStage()
        {
            if (!IsServer)
                return;

            if (RunDifficulty.Instance != null)
                RunDifficulty.Instance.CompleteStage();

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
                player.TeleportToSpawnRpc();
            }

            SpawnTeleporter();
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.ResetForStage();
            stageNumber.Value++;
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

        private void OnStageChanged(int previous, int current) => StageStarted?.Invoke(current);
    }
}
