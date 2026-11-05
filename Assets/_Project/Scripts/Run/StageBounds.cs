using System.Collections.Generic;
using RiskOfMagic.Player;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Run
{
    /// <summary>
    /// Playable volume of the current map, placed in the scene by the level designer.
    /// Used to scatter enemies, chests and the teleporter on the NavMesh (platforms included).
    /// </summary>
    public class StageBounds : MonoBehaviour
    {
        [SerializeField] private Vector3 center = new(0f, 10f, 0f);
        [SerializeField] private Vector3 size = new(110f, 40f, 110f);
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2f;
        [SerializeField, Min(0f), Tooltip("Falling this far below the volume = out of the map (players are brought back, enemies die).")]
        private float killMargin = 5f;

        public static StageBounds Instance { get; private set; }

        /// <summary>World height below which something has fallen off the map.</summary>
        public float KillHeight => transform.TransformPoint(center).y - size.y * 0.5f - killMargin;

        public static bool IsBelowMap(Vector3 position) => Instance != null && position.y < Instance.KillHeight;

        private void OnEnable() => Instance = this;

        private void OnDisable()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Random walkable point, at least minDistanceFromSpawns from player spawns and accepted by filter.</summary>
        public bool TryGetRandomPoint(out Vector3 point, float minDistanceFromSpawns = 0f, System.Predicate<Vector3> filter = null, int attempts = 30)
        {
            Vector3 worldCenter = transform.TransformPoint(center);
            float top = worldCenter.y + size.y * 0.5f;
            for (int i = 0; i < attempts; i++)
            {
                var origin = new Vector3(
                    worldCenter.x + Random.Range(-0.5f, 0.5f) * size.x,
                    top,
                    worldCenter.z + Random.Range(-0.5f, 0.5f) * size.z);

                // Raycast from the top so elevated platforms can be picked too.
                if (!Physics.Raycast(origin, Vector3.down, out var ground, size.y, groundMask, QueryTriggerInteraction.Ignore))
                    continue;
                if (!NavMesh.SamplePosition(ground.point, out var hit, navMeshSampleRadius, NavMesh.AllAreas))
                    continue;
                if (!IsFarFromSpawns(hit.position, minDistanceFromSpawns) || (filter != null && !filter(hit.position)))
                    continue;
                point = hit.position;
                return true;
            }
            point = default;
            return false;
        }

        public static bool IsFarFromSpawns(Vector3 position, float minDistance)
        {
            float sqr = minDistance * minDistance;
            foreach (var spawn in PlayerSpawnPoint.All)
                if ((spawn.transform.position - position).sqrMagnitude < sqr)
                    return false;
            return true;
        }

        public static bool IsFarFrom(Vector3 position, IReadOnlyList<Vector3> others, float minDistance)
        {
            float sqr = minDistance * minDistance;
            foreach (var other in others)
                if ((other - position).sqrMagnitude < sqr)
                    return false;
            return true;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.6f);
            Gizmos.DrawWireCube(transform.TransformPoint(center), size);
        }
    }
}
