using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Items;
using RiskOfMagic.Maps;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskOfMagic.Run
{
    /// <summary>
    /// Server: run and stage lifecycle. Persistent across map scenes (spawned once by GameBootstrap).
    /// Each stage: picks the map scene from the MapRotation (loads it if it differs from the current one),
    /// rolls the layout seed (MapLayout variants, replicated), then places interactables, the teleporter
    /// (on a Teleporter SpawnSpot when the map has some) and the enemy population.
    /// Also detects the game over (every player dead) and restarts the run on request.
    /// </summary>
    public class StageManager : NetworkBehaviour
    {
        [SerializeField] private Teleporter teleporterPrefab;
        [SerializeField, Min(0f)] private float teleporterMinDistanceFromSpawns = 45f;
        [SerializeField, Min(1), Tooltip("Stages per loop; the last one is the final stage (final boss, victory or loop).")]
        private int stagesPerLoop = 5;

        private readonly NetworkVariable<int> stageNumber = new(1);
        // Declared before gameOver: NetworkVariables apply in declaration order, so it's up to date in GameOverChanged.
        private readonly NetworkVariable<bool> victory = new();
        private readonly NetworkVariable<bool> gameOver = new();
        private readonly NetworkVariable<float> finalTime = new();
        private readonly NetworkVariable<int> kills = new();
        private readonly NetworkVariable<int> layoutSeed = new();
        private Teleporter teleporter;
        private bool pendingFullHeal;

        public static StageManager Instance { get; private set; }

        /// <summary>Every peer: (stage number, 1-based).</summary>
        public event Action<int> StageStarted;
        /// <summary>Every peer: (is game over).</summary>
        public event Action<bool> GameOverChanged;

        public int StageNumber => stageNumber.Value;
        /// <summary>Run ended (defeat or victory).</summary>
        public bool IsGameOver => gameOver.Value;
        public bool IsVictory => gameOver.Value && victory.Value;
        public int StagesPerLoop => stagesPerLoop;
        /// <summary>Last stage of a loop (5, 10, 15...): final boss, then victory or loop.</summary>
        public bool IsFinalStage => stageNumber.Value % stagesPerLoop == 0;
        /// <summary>Completed loops (0 during the first one).</summary>
        public int Loop => (stageNumber.Value - 1) / stagesPerLoop;
        /// <summary>Run duration frozen at the game over.</summary>
        public float FinalTime => finalTime.Value;
        public int Kills => kills.Value;
        /// <summary>Seed of the current stage's MapLayout variants.</summary>
        public int LayoutSeed => layoutSeed.Value;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            stageNumber.OnValueChanged += OnStageChanged;
            gameOver.OnValueChanged += OnGameOverChanged;
            layoutSeed.OnValueChanged += OnLayoutSeedChanged;
            if (!IsServer)
            {
                ApplyLayoutLocally(layoutSeed.Value);
                return;
            }
            EnemyActor.Killed += OnEnemyKilled;
            ResetStage(fullHeal: false); // first stage, in the scene that spawned us
        }

        /// <summary>Server: called by the GameBootstrap of a newly loaded map scene.</summary>
        public void OnMapSceneReady()
        {
            if (IsServer)
                ResetStage(pendingFullHeal);
        }

        public override void OnNetworkDespawn()
        {
            stageNumber.OnValueChanged -= OnStageChanged;
            gameOver.OnValueChanged -= OnGameOverChanged;
            layoutSeed.OnValueChanged -= OnLayoutSeedChanged;
            EnemyActor.Killed -= OnEnemyKilled;
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (IsServer && IsSpawned && !gameOver.Value && AllPlayersDead())
                EndRun(won: false);
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
            stageNumber.Value++;
            GoToStage(fullHeal: false);
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
                if (player.Equipment != null)
                    player.Equipment.ClearEquipment();
                if (player.RunStats != null)
                    player.RunStats.ResetStats();
            }
            victory.Value = false;

            stageNumber.Value = 1;
            kills.Value = 0;
            finalTime.Value = 0f;
            gameOver.Value = false;
            GoToStage(fullHeal: true);
        }

        /// <summary>Loads the stage's map scene if it differs from the current one, otherwise resets in place.</summary>
        private void GoToStage(bool fullHeal)
        {
            pendingFullHeal = fullHeal;
            var rotation = RiskOfMagic.Core.GameCatalog.Instance != null ? RiskOfMagic.Core.GameCatalog.Instance.MapRotation : null;
            string next = rotation != null ? rotation.PickScene(stageNumber.Value - 1) : null;
            if (!string.IsNullOrEmpty(next) && next != SceneManager.GetActiveScene().name)
            {
                // Scene objects (enemies, chests, teleporter...) go away with the old map;
                // the new map's GameBootstrap calls OnMapSceneReady once every client has loaded it.
                NetworkManager.SceneManager.LoadScene(next, LoadSceneMode.Single);
                return;
            }
            ResetStage(fullHeal);
        }

        /// <summary>Server only. Victory: sealed at the final stage. Defeat: every player dead.</summary>
        public void EndRun(bool won)
        {
            if (!IsServer || gameOver.Value)
                return;
            finalTime.Value = RunDifficulty.Instance != null ? RunDifficulty.Instance.ElapsedSeconds : 0f;
            victory.Value = won;
            gameOver.Value = true;
            if (won)
                DespawnAll(new List<EnemyActor>(EnemyActor.Active));
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

            // Layout first: variants change the geometry, the NavMesh and the active SpawnSpots.
            layoutSeed.Value = UnityEngine.Random.Range(1, int.MaxValue);
            if (MapLayout.Instance != null)
                MapLayout.Instance.Apply(layoutSeed.Value, rebuildNavMesh: true);

            var interactables = GetComponent<InteractableSpawner>();
            if (interactables != null)
                interactables.Respawn();

            foreach (var player in PlayerAvatar.All)
            {
                if (player.Downed != null)
                    player.Downed.ForceStand();
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

            Vector3 position;
            if (SpawnSpot.TryPick(SpawnSpotType.Teleporter, out var spot))
                position = spot.position;
            else if (!bounds.TryGetRandomPoint(out position, teleporterMinDistanceFromSpawns, attempts: 60) &&
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
            if (!gameOver.Value && !enemy.IsIllusion)
                kills.Value++;
        }

        private void OnStageChanged(int previous, int current) => StageStarted?.Invoke(current);

        private void OnLayoutSeedChanged(int previous, int current)
        {
            if (!IsServer)
                ApplyLayoutLocally(current);
        }

        /// <summary>Clients only show the variants; the NavMesh is server-side.</summary>
        private static void ApplyLayoutLocally(int seed)
        {
            if (seed != 0 && MapLayout.Instance != null && MapLayout.Instance.AppliedSeed != seed)
                MapLayout.Instance.Apply(seed, rebuildNavMesh: false);
        }
        private void OnGameOverChanged(bool previous, bool current) => GameOverChanged?.Invoke(current);
    }
}
