using System;
using System.Collections.Generic;
using RiskOfMagic.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Run
{
    /// <summary>Server: scatters chests/shrines on the NavMesh when the stage starts.</summary>
    public class InteractableSpawner : NetworkBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public NetworkObject prefab;
            [Min(0)] public int count;
        }

        [SerializeField] private List<Entry> entries = new();
        [SerializeField] private Vector3 areaCenter;
        [SerializeField] private Vector2 areaSize = new(100f, 100f);
        [SerializeField, Min(0f)] private float minSpacing = 5f;
        [SerializeField, Min(0f)] private float playerSpawnClearance = 8f;
        [SerializeField, Min(1)] private int attemptsPerObject = 30;

        private readonly List<Vector3> placed = new();

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
                return;

            placed.Clear();
            foreach (var entry in entries)
            {
                if (entry.prefab == null)
                    continue;
                for (int i = 0; i < entry.count; i++)
                {
                    if (!TryFindPosition(out var position))
                    {
                        Debug.LogWarning($"[InteractableSpawner] No room left for {entry.prefab.name}.", this);
                        break;
                    }
                    placed.Add(position);
                    var instance = Instantiate(entry.prefab, position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                    instance.Spawn(true);
                }
            }
        }

        private bool TryFindPosition(out Vector3 position)
        {
            for (int attempt = 0; attempt < attemptsPerObject; attempt++)
            {
                var candidate = areaCenter + new Vector3(Random.Range(-0.5f, 0.5f) * areaSize.x, 0f, Random.Range(-0.5f, 0.5f) * areaSize.y);
                if (!NavMesh.SamplePosition(candidate, out var hit, 3f, NavMesh.AllAreas) || !IsFree(hit.position))
                    continue;
                position = hit.position;
                return true;
            }
            position = default;
            return false;
        }

        private bool IsFree(Vector3 position)
        {
            foreach (var other in placed)
                if ((other - position).sqrMagnitude < minSpacing * minSpacing)
                    return false;
            foreach (var spawn in PlayerSpawnPoint.All)
                if ((spawn.transform.position - position).sqrMagnitude < playerSpawnClearance * playerSpawnClearance)
                    return false;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(areaCenter, new Vector3(areaSize.x, 1f, areaSize.y));
        }
    }
}
