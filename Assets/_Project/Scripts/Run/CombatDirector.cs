using System;
using System.Collections.Generic;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Run
{
    /// <summary>
    /// Server-side RoR2-style director: earns credits over time (faster with difficulty and players)
    /// and spends them on waves of spawn cards around living players.
    /// </summary>
    public class CombatDirector : NetworkBehaviour
    {
        [SerializeField] private List<SpawnCard> cards = new();

        [Header("Credits")]
        [SerializeField] private float creditMultiplier = 1.8f;
        [SerializeField] private float startingCredits = 100f;

        [Header("Waves")]
        [SerializeField] private Vector2 waveInterval = new(2f, 6f);
        [SerializeField, Min(1)] private int maxSpawnsPerWave = 8;
        [SerializeField, Min(1)] private int maxAliveEnemies = 40;

        [Header("Placement")]
        [SerializeField] private float minSpawnDistance = 12f;
        [SerializeField] private float maxSpawnDistance = 40f;
        [SerializeField] private float navMeshSampleRadius = 6f;
        [SerializeField, Min(1)] private int placementAttempts = 8;

        [Header("Debug")]
        [SerializeField] private bool logDiagnostics = true;

        private readonly List<SpawnCard> candidates = new();
        private readonly List<PlayerAvatar> alivePlayers = new();
        private float credits;
        private float nextWaveTime;
        private string lastDiagnostic;

        /// <summary>Server only.</summary>
        public event Action<EnemyActor> EnemySpawned;

        public float Credits => credits;

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                enabled = false;
                return;
            }
            credits = startingCredits;
            nextWaveTime = Time.time + 2f;

            if (cards.Count == 0)
                Debug.LogError("[CombatDirector] No spawn cards assigned.", this);
            ValidateCards();
            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
                Debug.LogError("[CombatDirector] No NavMesh loaded in the scene: bake the NavMeshSurface on 'Level'.", this);
            Diagnose($"Started with {startingCredits} credits and {cards.Count} cards.");
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
            if (!IsSpawned || PlayerAvatar.All.Count == 0)
                return;

            float coefficient = RunDifficulty.CurrentCoefficient;
            int players = PlayerAvatar.All.Count;
            credits += creditMultiplier * 0.75f * (1f + 0.4f * coefficient) * (players + 1) * 0.5f * Time.deltaTime;

            if (Time.time < nextWaveTime)
                return;
            nextWaveTime = Time.time + Random.Range(waveInterval.x, waveInterval.y);
            SpawnWave(coefficient);
        }

        private void SpawnWave(float coefficient)
        {
            var card = PickCard(coefficient);
            if (card == null)
            {
                Diagnose($"No affordable card (credits {credits:0}, coefficient {coefficient:0.00}).");
                return;
            }

            int level = RunDifficulty.CurrentEnemyLevel;
            int spawned = 0;
            for (int i = 0; i < maxSpawnsPerWave && credits >= card.cost && EnemyActor.Active.Count < maxAliveEnemies; i++)
            {
                if (!TryFindSpawnPosition(out var position, out var lookTarget))
                {
                    Diagnose("No NavMesh position found around the players (NavMesh baked? players on it?).");
                    break;
                }
                Vector3 facing = Vector3.ProjectOnPlane(lookTarget - position, Vector3.up);
                Spawn(card, position, facing.sqrMagnitude > 0.01f ? Quaternion.LookRotation(facing) : Quaternion.identity, level);
                credits -= card.cost;
                spawned++;
            }
            if (spawned > 0)
                Diagnose($"Spawned {spawned}x {card.name} (level {level}), {credits:0} credits left.");
        }

        private void Diagnose(string message)
        {
            if (!logDiagnostics || message == lastDiagnostic)
                return;
            lastDiagnostic = message;
            Debug.Log($"[CombatDirector] {message}", this);
        }

        private SpawnCard PickCard(float coefficient)
        {
            candidates.Clear();
            float totalWeight = 0f;
            foreach (var card in cards)
            {
                if (card == null || card.prefab == null || card.cost > credits || coefficient < card.minCoefficient || card.weight <= 0f)
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

        private bool TryFindSpawnPosition(out Vector3 position, out Vector3 anchor)
        {
            position = anchor = default;
            alivePlayers.Clear();
            foreach (var player in PlayerAvatar.All)
                if (player.IsAlive)
                    alivePlayers.Add(player);
            if (alivePlayers.Count == 0)
                return false;

            anchor = alivePlayers[Random.Range(0, alivePlayers.Count)].transform.position;
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

        /// <summary>Server only. Also usable by shrines, bosses, events.</summary>
        public EnemyActor Spawn(SpawnCard card, Vector3 position, Quaternion rotation, int level)
        {
            var enemy = Instantiate(card.prefab, position, rotation);
            enemy.Initialize(level);
            enemy.NetworkObject.Spawn(true);
            EnemySpawned?.Invoke(enemy);
            return enemy;
        }
    }
}
