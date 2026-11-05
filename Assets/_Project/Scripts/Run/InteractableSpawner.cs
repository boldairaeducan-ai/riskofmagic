using System;
using System.Collections.Generic;
using RiskOfMagic.Maps;
using RiskOfMagic.Run.MapEvents;
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

            foreach (var entry in entries)
            {
                if (entry.prefab == null)
                    continue;
                for (int i = 0; i < entry.count; i++)
                    if (!TrySpawn(entry.prefab, entry.useSpawnSpots, entry.spotType, bounds))
                        break;
            }
            SpawnMapEvents(bounds);
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
