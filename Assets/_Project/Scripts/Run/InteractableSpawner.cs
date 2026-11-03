using System;
using System.Collections.Generic;
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
        }

        [SerializeField] private List<Entry> entries = new();
        [SerializeField, Min(0f)] private float minSpacing = 5f;
        [SerializeField, Min(0f)] private float playerSpawnClearance = 8f;
        [SerializeField] private bool spawnOnStart = true;

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
                {
                    if (!bounds.TryGetRandomPoint(out var position, playerSpawnClearance, p => StageBounds.IsFarFrom(p, placed, minSpacing)))
                    {
                        Debug.LogWarning($"[InteractableSpawner] No room left for {entry.prefab.name}.", this);
                        break;
                    }
                    placed.Add(position);
                    var instance = Instantiate(entry.prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    instance.Spawn(true);
                    spawned.Add(instance);
                }
            }
        }
    }
}
