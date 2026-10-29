using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Player
{
    public class PlayerSpawnPoint : MonoBehaviour
    {
        private static readonly List<PlayerSpawnPoint> points = new();

        public static bool TryGet(int index, out Pose pose)
        {
            pose = default;
            if (points.Count == 0)
                return false;
            var point = points[Mathf.Abs(index) % points.Count].transform;
            pose = new Pose(point.position, point.rotation);
            return true;
        }

        private void OnEnable() => points.Add(this);
        private void OnDisable() => points.Remove(this);

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
            Gizmos.DrawRay(transform.position + Vector3.up, transform.forward);
        }
    }
}
