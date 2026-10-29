using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Vfx
{
    [RequireComponent(typeof(LineRenderer))]
    public class LineEffect : MonoBehaviour
    {
        private LineRenderer line;

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = true;
        }

        public void SetPoints(IReadOnlyList<Vector3> points)
        {
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
                line.SetPosition(i, points[i]);
        }
    }
}
