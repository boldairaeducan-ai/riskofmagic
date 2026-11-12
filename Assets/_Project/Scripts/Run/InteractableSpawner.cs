using System;
using System.Collections.Generic;
using RiskOfMagic.Maps;
using RiskOfMagic.Run.MapEvents;
using RiskOfMagic.Run.Secrets;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Run
{
    /// <summary>Server: scatters chests/shrines inside the StageBounds. Respawned by the StageManager on each stage.</summary>
    public class InteractableSpawner : NetworkBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public NetworkObject prefab;
            [Min(0)] public int count;
            [Tooltip("Place on SpawnSpots of this type when the map has some (random NavMesh point otherwise).")]
            public bool useSpawnSpots;
            public SpawnSpotType spotType;
        }

        [Serializable]
        public struct MapEventEntry
        {
            public MapEventType type;
            public NetworkObject prefab;
        }

        [SerializeField] private List<Entry> entries = new();
        [SerializeField, Tooltip("Count, weights and toggles come from Resources/MapEventsConfig.")]
        private List<MapEventEntry> mapEvents = new();
        [Header("Map secret (needs a MapSecret + SecretAltar/SecretFragment SpawnSpots in the map)")]
        [SerializeField] private NetworkObject secretAltarPrefab;
        [SerializeField] private NetworkObject secretFragmentPrefab;
        [SerializeField, Min(0f)] private float minSpacing = 5f;
        [SerializeField, Min(0f)] private float playerSpawnClearance = 8f;
        [SerializeField, Tooltip("Off when a StageManager drives the stages.")] private bool spawnOnStart;

        private readonly List<Vector3> placed = new();
        private readonly List<NetworkObject> spawned = new();

        public override void OnNetworkSpawn()
        {
            if (IsServer && spawnOnStart)
                SpawnAll();
        }

        /// <summary>Server only.</summary>
        public void Respawn()
        {
            DespawnAll();
            SpawnAll();
        }

        /// <summary>Server only.</summary>
        public void DespawnAll()
        {
            foreach (var networkObject in spawned)
                if (networkObject != null && networkObject.IsSpawned)
                    networkObject.Despawn();
            spawned.Clear();
            placed.Clear();
        }

        /// <summary>Server only.</summary>
        public void SpawnAll()
        {
            var bounds = StageBounds.Instance;
            if (!IsServer || bounds == null)
            {
                if (bounds == null)
                    Debug.LogError("[InteractableSpawner] No StageBounds in the scene.", this);
                return;
            }

            SpawnInteractables(bounds);
            SpawnMapEvents(bounds);
            SpawnSecret();
        }

        /// <summary>Altar on a random SecretAltar spot + fragments on distinct random SecretFragment spots.</summary>
        private void SpawnSecret()
        {
            var secret = MapSecret.Current;
            if (secret == null || secretAltarPrefab == null || secretFragmentPrefab == null || Random.value > secret.activeChance)
                return;
            if (!SpawnSpot.TryPick(SpawnSpotType.SecretAltar, out var altarPose))
            {
                Debug.LogWarning("[InteractableSpawner] MapSecret without any SecretAltar spot on the NavMesh.", this);
                return;
            }
            var fragmentPoses = new List<Pose>();
            for (int i = 0; i < secret.fragmentCount; i++)
            {
                if (!SpawnSpot.TryPick(SpawnSpotType.SecretFragment, out var pose,
                        p => (p - altarPose.position).sqrMagnitude > 1f && fragmentPoses.TrueForAll(f => (f.position - p).sqrMagnitude > 1f),
                        snapToNavMesh: false))
                {
                    Debug.LogWarning($"[InteractableSpawner] MapSecret needs {secret.fragmentCount} SecretFragment spots (found {fragmentPoses.Count}).", this);
                    return;
                }
                fragmentPoses.Add(pose);
            }

            var altarInstance = Instantiate(secretAltarPrefab, altarPose.position, altarPose.rotation);
            if (altarInstance.TryGetComponent(out SecretAltar altar))
                altar.Setup(fragmentPoses.Count);
            altarInstance.Spawn(true);
            spawned.Add(altarInstance);
            foreach (var pose in fragmentPoses)
            {
                var fragment = Instantiate(secretFragmentPrefab, pose.position, pose.rotation);
                fragment.Spawn(true);
                spawned.Add(fragment);
            }
        }

        /// <summary>
        /// Guaranteed ones (merchant), then a credit budget (GameBalance.economy: difficulty, stage, players) spent on
        /// random affordable interactables, weighted. Entries without a cost in GameBalance use their fixed count.
        /// </summary>
        private void SpawnInteractables(StageBounds bounds)
        {
            var economy = RiskOfMagic.Core.GameBalance.Instance.economy;
            var pool = new List<(Entry entry, RiskOfMagic.Core.GameBalance.InteractableCost cost)>();
            foreach (var entry in entries)
            {
                if (entry.prefab == null)
                    continue;
                var cost = economy.Find(entry.prefab.name);
                int fixedCount = cost != null ? cost.guaranteed : entry.count;
                for (int i = 0; i < fixedCount; i++)
                    if (!TrySpawn(entry.prefab, entry.useSpawnSpots, entry.spotType, bounds))
                        break;
                if (cost != null && cost.weight > 0f)
                    pool.Add((entry, cost));
            }

            bool hard = RunDifficulty.IsHard;
            int stage = StageManager.Instance != null ? Mathf.Max(1, StageManager.Instance.StageNumber) : 1;
            int players = Mathf.Max(1, RiskOfMagic.Player.PlayerAvatar.All.Count);
            float budget = (hard ? economy.hardCredits : economy.normalCredits)
                           * (1f + (hard ? economy.hardGrowthPerStage : economy.normalGrowthPerStage) * (stage - 1))
                           * (1f + economy.perExtraPlayer * (players - 1));
            int spawnedCount = 0;
            for (int guard = 0; guard < 200 && pool.Count > 0; guard++)
            {
                float total = 0f;
                foreach (var option in pool)
                    if (option.cost.credits <= budget)
                        total += option.cost.weight;
                if (total <= 0f)
                    break;
                float roll = Random.value * total;
                foreach (var option in pool)
                {
                    if (option.cost.credits > budget)
                        continue;
                    roll -= option.cost.weight;
                    if (roll > 0f)
                        continue;
                    budget -= option.cost.credits;
                    if (TrySpawn(option.entry.prefab, option.entry.useSpawnSpots, option.entry.spotType, bounds))
                        spawnedCount++;
                    else
                        pool.Remove(option); // no room left for it
                    break;
                }
            }
            Debug.Log($"[InteractableSpawner] Stage {stage}: {spawnedCount} interactables from the budget ({(hard ? "hard" : "normal")}, {players} player(s)).");
        }

        /// <summary>0..N distinct events per stage, weighted (MapEventsConfig).</summary>
        private void SpawnMapEvents(StageBounds bounds)
        {
            var config = MapEventsConfig.Instance;
            var pool = new List<MapEventEntry>();
            foreach (var entry in mapEvents)
                if (entry.prefab != null && config.IsEnabled(entry.type) && config.GetWeight(entry.type) > 0f)
                    pool.Add(entry);

            int count = Random.Range(Mathf.Max(0, config.eventsPerStage.x), Mathf.Max(config.eventsPerStage.x, config.eventsPerStage.y) + 1);
            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                float total = 0f;
                foreach (var entry in pool)
                    total += config.GetWeight(entry.type);
                float roll = Random.value * total;
                int picked = pool.Count - 1;
                for (int j = 0; j < pool.Count; j++)
                {
                    roll -= config.GetWeight(pool[j].type);
                    if (roll <= 0f)
                    {
                        picked = j;
                        break;
                    }
                }
                TrySpawn(pool[picked].prefab, true, SpawnSpotType.Interactable, bounds);
                pool.RemoveAt(picked);
            }
        }

        private bool TrySpawn(NetworkObject prefab, bool useSpawnSpots, SpawnSpotType spotType, StageBounds bounds)
        {
            Vector3 position;
            var rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            if (useSpawnSpots && SpawnSpot.TryPick(spotType, out var spot, p => StageBounds.IsFarFrom(p, placed, minSpacing)))
            {
                position = spot.position;
                rotation = spot.rotation;
            }
            else if (!bounds.TryGetRandomPoint(out position, playerSpawnClearance, p => StageBounds.IsFarFrom(p, placed, minSpacing)))
            {
                Debug.LogWarning($"[InteractableSpawner] No room left for {prefab.name}.", this);
                return false;
            }
            placed.Add(position);
            var instance = Instantiate(prefab, position, rotation);
            instance.Spawn(true);
            spawned.Add(instance);
            return true;
        }
    }
}
