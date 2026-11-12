using System;
using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Enemies.Elites;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Run
{
    /// <summary>
    /// Server-side RoR2-style director.
    /// Stage population: at the start of each stage, idle enemies are scattered across the StageBounds (exploration blockers).
    /// Waves: credits earned over time (faster with difficulty, players and events) buy enemies.
    ///  - Normally: spawned anywhere in the StageBounds but far from every player, idle until they detect someone
    ///    (cleared areas stay clear, no constant swarming).
    ///  - During an event (teleporter): spawned around a player and hunting immediately (pressure).
    /// </summary>
    public class CombatDirector : NetworkBehaviour
    {
        [SerializeField] private List<SpawnCard> cards = new();

        private float creditMultiplier => RiskOfMagic.Core.GameBalance.Instance.director.creditMultiplier;
        private float startingCredits => RiskOfMagic.Core.GameBalance.Instance.director.startingCredits;

        [Header("Stage population")]
        [SerializeField, Tooltip("Off when a StageManager drives the stages.")] private bool populateOnStart;
        private float stagePopulationCredits => RiskOfMagic.Core.GameBalance.Instance.director.stagePopulationCredits;
        private float populationMinDistanceFromSpawns => RiskOfMagic.Core.GameBalance.Instance.director.populationMinDistanceFromSpawns;
        private float populationMinSpacing => RiskOfMagic.Core.GameBalance.Instance.director.populationMinSpacing;

        private bool wavesHuntPlayers => RiskOfMagic.Core.GameBalance.Instance.director.wavesHuntPlayers;
        private Vector2 waveInterval => RiskOfMagic.Core.GameBalance.Instance.director.waveInterval;
        private int maxSpawnsPerWave => RiskOfMagic.Core.GameBalance.Instance.director.maxSpawnsPerWave;
        private int maxAliveEnemies => RiskOfMagic.Core.GameBalance.Instance.director.maxAliveEnemies;

        private float eliteMinProgress => RiskOfMagic.Core.GameBalance.Instance.director.eliteMinProgress;
        private float eliteWaveChance => RiskOfMagic.Core.GameBalance.Instance.director.eliteWaveChance;

        private Vector2 farSpawnDistance => RiskOfMagic.Core.GameBalance.Instance.director.farSpawnDistance;
        private float minSpawnDistance => RiskOfMagic.Core.GameBalance.Instance.director.minSpawnDistance;
        private float maxSpawnDistance => RiskOfMagic.Core.GameBalance.Instance.director.maxSpawnDistance;
        [SerializeField] private float navMeshSampleRadius = 6f;
        [SerializeField, Min(1)] private int placementAttempts = 8;

        [Header("Debug")]
        [SerializeField] private bool logDiagnostics = true;

        private readonly List<SpawnCard> candidates = new();
        private readonly List<PlayerAvatar> alivePlayers = new();
        private float credits;
        private float nextWaveTime;
        private string lastDiagnostic;
        private readonly List<Vector3> populationPoints = new();

        public static CombatDirector Instance { get; private set; }

        /// <summary>Server only. Extra credit income during events (teleporter).</summary>
        public float EventCreditMultiplier { get; set; } = 1f;
        public bool IsEventActive => EventCreditMultiplier > 1f;

        /// <summary>Server only. No credits nor waves while paused (game over).</summary>
        public bool Paused { get; set; }

        /// <summary>Server only.</summary>
        public event Action<EnemyActor> EnemySpawned;

        public float Credits => credits;

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (!IsServer)
            {
                enabled = false;
                return;
            }
            credits = startingCredits;
            nextWaveTime = Time.time + 5f;

            if (cards.Count == 0)
                Debug.LogError("[CombatDirector] No spawn cards assigned.", this);
            ValidateCards();
            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
                Debug.LogError("[CombatDirector] No NavMesh loaded in the scene: bake the NavMeshSurface on 'Level'.", this);
            Diagnose($"Started with {startingCredits} credits and {cards.Count} cards.");
            if (populateOnStart)
                PopulateStage();
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Server only. Called by the StageManager when a new stage begins.</summary>
        public void ResetForStage()
        {
            credits = startingCredits;
            nextWaveTime = Time.time + 5f;
            EventCreditMultiplier = 1f;
            Paused = false;
            PopulateStage();
        }

        /// <summary>Server only. Scatters idle enemies across the map, away from player spawns.</summary>
        public void PopulateStage()
        {
            var bounds = StageBounds.Instance;
            if (!IsServer || bounds == null)
            {
                if (bounds == null)
                    Debug.LogError("[CombatDirector] No StageBounds in the scene: stage population skipped.", this);
                return;
            }

            float coefficient = RunDifficulty.CurrentCoefficient;
            float budget = stagePopulationCredits * Mathf.Sqrt(coefficient);
            int level = RunDifficulty.CurrentEnemyLevel;
            int spawned = 0;
            populationPoints.Clear();

            while (EnemyActor.Active.Count < maxAliveEnemies)
            {
                var card = PickCard(budget, RunDifficulty.CurrentProgress * RunDifficulty.CurrentUnlockMultiplier);
                if (card == null || !bounds.TryGetRandomPoint(out var position, populationMinDistanceFromSpawns,
                        p => StageBounds.IsFarFrom(p, populationPoints, populationMinSpacing)))
                    break;
                populationPoints.Add(position);
                Spawn(card, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), level);
                budget -= card.cost;
                spawned++;
            }
            Diagnose($"Stage populated with {spawned} enemies.");
        }

        private void ValidateCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null)
                    Debug.LogError($"[CombatDirector] Card slot {i} is empty.", this);
                else if (card.prefab == null)
                    Debug.LogError($"[CombatDirector] {card.name} has no prefab.", card);
                else if (card.weight <= 0f)
                    Debug.LogWarning($"[CombatDirector] {card.name} has a weight of 0 and will never be picked.", card);
                else
                    Diagnose($"Card {card.name}: prefab {card.prefab.name}, cost {card.cost}, weight {card.weight}, min coefficient {card.minCoefficient}.");
            }
        }

        /// <summary>Server only. Debug helper.</summary>
        public void ForceWave(float bonusCredits = 100f)
        {
            if (!IsServer)
                return;
            credits += bonusCredits;
            nextWaveTime = 0f;
        }

        private void Update()
        {
            if (!IsSpawned || Paused || PlayerAvatar.All.Count == 0)
                return;

            float coefficient = RunDifficulty.CurrentCoefficient;
            int players = PlayerAvatar.All.Count;
            credits += creditMultiplier * EventCreditMultiplier * RunDifficulty.CurrentDirectorCreditMultiplier * 0.75f * (1f + 0.4f * coefficient) * (players + 1) * 0.5f * Time.deltaTime;

            if (Time.time < nextWaveTime)
                return;
            nextWaveTime = Time.time + Random.Range(waveInterval.x, waveInterval.y);
            SpawnWave(coefficient);
        }

        private void SpawnWave(float coefficient)
        {
            float progress = RunDifficulty.CurrentProgress * RunDifficulty.CurrentUnlockMultiplier;
            var card = PickCard(credits, progress);
            if (card == null)
            {
                Diagnose($"No affordable card (credits {credits:0}, coefficient {coefficient:0.00}).");
                return;
            }

            // Elite wave: same card, cost multiplied by the affix.
            EliteAffix elite = null;
            float cost = card.cost;
            if (progress >= eliteMinProgress && Random.value < eliteWaveChance && GameCatalog.Instance != null)
            {
                var affix = GameCatalog.Instance.RollElite();
                if (affix != null && credits >= card.cost * affix.costMultiplier)
                {
                    elite = affix;
                    cost = card.cost * affix.costMultiplier;
                }
            }

            int level = RunDifficulty.CurrentEnemyLevel;
            int spawned = 0;
            bool nearPlayers = IsEventActive;
            bool hunt = wavesHuntPlayers || IsEventActive;
            Vector3 groupCenter = default;

            for (int i = 0; i < maxSpawnsPerWave && credits >= cost && EnemyActor.Active.Count < maxAliveEnemies; i++)
            {
                PlayerAvatar anchorPlayer;
                Vector3 position;
                bool found = nearPlayers
                    ? TryFindSpawnPosition(out position, out anchorPlayer)
                    : TryFindFarPosition(i == 0 ? (Vector3?)null : groupCenter, out position, out anchorPlayer);
                if (!found)
                {
                    Diagnose(nearPlayers
                        ? "No NavMesh position found around the players (NavMesh baked? players on it?)."
                        : "No spawn position far enough from the players (map too small? check farSpawnDistance / StageBounds).");
                    break;
                }
                if (i == 0)
                    groupCenter = position;

                Vector3 facing = Vector3.ProjectOnPlane(anchorPlayer.transform.position - position, Vector3.up);
                var enemy = Spawn(card, position, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity, level, elite);
                if (hunt && enemy.TryGetComponent(out EnemyBrain brain))
                    brain.Alert(anchorPlayer, false);
                credits -= cost;
                spawned++;
            }
            if (spawned > 0)
                Diagnose($"Spawned {spawned}x {card.name}{(elite != null ? $" [{elite.name}]" : "")} (level {level}), {credits:0} credits left.");
        }

        private void Diagnose(string message)
        {
            if (!logDiagnostics || message == lastDiagnostic)
                return;
            lastDiagnostic = message;
            Debug.Log($"[CombatDirector] {message}", this);
        }

        /// <summary>Server. Random card unlocked at the current progress, whatever its cost (map events).</summary>
        public SpawnCard PickUnlockedCard() => PickCard(float.MaxValue, RunDifficulty.CurrentProgress * RunDifficulty.CurrentUnlockMultiplier);

        private SpawnCard PickCard(float budget, float progress)
        {
            candidates.Clear();
            float totalWeight = 0f;
            foreach (var card in cards)
            {
                if (card == null || card.prefab == null || card.cost > budget || progress < card.minCoefficient || card.weight <= 0f)
                    continue;
                candidates.Add(card);
                totalWeight += card.weight;
            }

            float roll = Random.value * totalWeight;
            foreach (var card in candidates)
            {
                roll -= card.weight;
                if (roll <= 0f)
                    return card;
            }
            return candidates.Count > 0 ? candidates[^1] : null;
        }

        private bool TryFindSpawnPosition(out Vector3 position, out PlayerAvatar anchorPlayer)
        {
            position = default;
            anchorPlayer = null;
            alivePlayers.Clear();
            foreach (var player in PlayerAvatar.All)
                if (player.IsAlive)
                    alivePlayers.Add(player);
            if (alivePlayers.Count == 0)
                return false;

            anchorPlayer = alivePlayers[Random.Range(0, alivePlayers.Count)];
            Vector3 anchor = anchorPlayer.transform.position;
            for (int i = 0; i < placementAttempts; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 candidate = anchor + new Vector3(dir.x, 0f, dir.y) * Random.Range(minSpawnDistance, maxSpawnDistance);
                if (NavMesh.SamplePosition(candidate, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                {
                    position = hit.position;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Point in the StageBounds whose nearest living player is within farSpawnDistance.
        /// Members of a wave spawn grouped around the first one (groupCenter).
        /// </summary>
        private bool TryFindFarPosition(Vector3? groupCenter, out Vector3 position, out PlayerAvatar nearestPlayer)
        {
            position = default;
            nearestPlayer = null;
            alivePlayers.Clear();
            foreach (var player in PlayerAvatar.All)
                if (player.IsAlive)
                    alivePlayers.Add(player);
            if (alivePlayers.Count == 0)
                return false;

            if (groupCenter.HasValue)
            {
                for (int i = 0; i < placementAttempts; i++)
                {
                    Vector2 offset = Random.insideUnitCircle * 6f;
                    if (NavMesh.SamplePosition(groupCenter.Value + new Vector3(offset.x, 0f, offset.y), out var hit, navMeshSampleRadius, NavMesh.AllAreas)
                        && IsFarFromPlayers(hit.position, out nearestPlayer))
                    {
                        position = hit.position;
                        return true;
                    }
                }
            }

            var bounds = StageBounds.Instance;
            if (bounds != null && bounds.TryGetRandomPoint(out position, 0f, p => IsFarFromPlayers(p, out _), placementAttempts * 4))
                return IsFarFromPlayers(position, out nearestPlayer);
            return false;
        }

        private bool IsFarFromPlayers(Vector3 position, out PlayerAvatar nearestPlayer)
        {
            nearestPlayer = null;
            float nearestSqr = float.MaxValue;
            foreach (var player in alivePlayers)
            {
                float sqr = (player.transform.position - position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearestPlayer = player;
                }
            }
            return nearestPlayer != null
                   && nearestSqr >= farSpawnDistance.x * farSpawnDistance.x
                   && nearestSqr <= farSpawnDistance.y * farSpawnDistance.y;
        }

        /// <summary>Server only. Also usable by shrines, bosses, events.</summary>
        public EnemyActor Spawn(SpawnCard card, Vector3 position, Quaternion rotation, int level, EliteAffix elite = null)
        {
            var enemy = Instantiate(card.prefab, position, rotation);
            enemy.Initialize(level);
            if (elite != null)
                enemy.MakeElite(elite);
            enemy.NetworkObject.Spawn(true);
            EnemySpawned?.Invoke(enemy);
            return enemy;
        }
    }
}
