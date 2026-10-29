using System.Collections.Generic;
using RiskOfMagic.Core;
using UnityEngine;

namespace RiskOfMagic.Vfx
{
    public static class VfxUtility
    {
        /// <summary>Local-only effect spawn. Null prefab is a no-op so art can be added later.</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (prefab == null)
                return null;
            var go = GameObjectPool.Spawn(prefab, position, rotation);
            go.transform.localScale = prefab.transform.localScale * scale;
            if (go.TryGetComponent(out TimedEffect timed))
                timed.Restart();
            return go;
        }

        public static void SpawnLine(GameObject prefab, IReadOnlyList<Vector3> points)
        {
            var go = Spawn(prefab, points.Count > 0 ? points[0] : Vector3.zero, Quaternion.identity);
            if (go != null && go.TryGetComponent(out LineEffect line))
                line.SetPoints(points);
        }
    }
}
