using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Maps
{
    public enum SpawnSpotType : byte
    {
        Teleporter,
        Merchant,
        Interactable,
        /// <summary>Hiding place of a secret's rune fragment (MapSecret). Not snapped to the NavMesh.</summary>
        SecretFragment,
        /// <summary>Candidate location of the secret's altar (MapSecret).</summary>
        SecretAltar
    }

    /// <summary>
    /// Level design: candidate location for an important object. One is picked at random per stage among the
    /// active spots of the requested type (spots inside inactive layout variants are ignored).
    /// </summary>
    public class SpawnSpot : MonoBehaviour
    {
        private static readonly List<SpawnSpot> spots = new();
        private static readonly List<SpawnSpot> candidates = new();

        [SerializeField] private SpawnSpotType type;

        public SpawnSpotType Type => type;

        private void OnEnable() => spots.Add(this);
        private void OnDisable() => spots.Remove(this);

        public static bool TryPick(SpawnSpotType type, out Pose pose, Predicate<Vector3> filter = null, bool snapToNavMesh = true)
        {
            candidates.Clear();
            foreach (var spot in spots)
                if (spot.type == type && (filter == null || filter(spot.transform.position)))
                    candidates.Add(spot);

            while (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                var spot = candidates[index];
                candidates.RemoveAt(index);
                if (!snapToNavMesh)
                {
                    pose = new Pose(spot.transform.position, spot.transform.rotation);
                    return true;
                }
                if (!NavMesh.SamplePosition(spot.transform.position, out var hit, 3f, NavMesh.AllAreas))
                    continue;
                pose = new Pose(hit.position, spot.transform.rotation);
                return true;
            }
            pose = default;
            return false;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = type switch
            {
                SpawnSpotType.Teleporter => new Color(0.7f, 0.3f, 1f),
                SpawnSpotType.Merchant => new Color(1f, 0.8f, 0.2f),
                SpawnSpotType.SecretFragment => new Color(0.3f, 0.9f, 1f),
                SpawnSpotType.SecretAltar => new Color(1f, 0.35f, 0.8f),
                _ => new Color(0.3f, 1f, 0.5f)
            };
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 1f);
            Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 2.5f);
        }
    }
}
