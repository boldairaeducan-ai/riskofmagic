using AlekGames.Placer3.Profiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public static class BezierCurveValues
    {
        public enum stepM { oneShoot, segmental };
        public enum spawnT { Wall, PrefabPalette };

        [System.Serializable]
        public struct values
        {

            public spawnT spawnType;

            public wallSettings wallPreset;

            [Tooltip(" if should go further than picked position, to ensuer that the wall has indeed went threw it, and spawned when it was picked. this may resul in a lot of mess and inaccuracy")]
            public bool allowOverPoint;

            public prefabPalette palette;

            public int specificIndex;

            [Range(0, 30), Tooltip("min distanc between spawns (or their attempts)")]
            public float minSpawnDistance;

            [Tooltip("if should snap spawned objets to ground")]
            public bool snapToGround;


            [Tooltip("curvature calculation used by the curve. the curve preview does not match the curve then, only the spawn points previews do")]
            public curveMode curveMode;


            public stepM stepMode;

            [Range(0.001f, 0.1f)]
            public float curveSteps;

            public List<Vector2> fromCurveMinMaxDistances;

            public anchorSettings[] curve;

            public values(bool snapToGround)
            {
                spawnType = spawnT.PrefabPalette;
                palette = null;
                wallPreset = null;
                allowOverPoint = false;
                specificIndex = -1;
                minSpawnDistance = 1;
                this.snapToGround = snapToGround;
                curveMode = curveMode.cubic;
                stepMode = stepM.segmental;
                curveSteps = 0.1f;
                fromCurveMinMaxDistances = new List<Vector2>(1) { Vector2.zero };
                curve = new anchorSettings[0];
            }
        }

        [System.Serializable]
        public struct editValues
        {
            public enum Tmode { automatic, halfManual, manual };
            public enum OtherTMode { none, keepRatio, Copy };
            [Tooltip("edit mode of tangents. automatic will place tangents for you, half manual, will let you mov them, while keeping  straight line with the other one, and manula doesnt give a f, and lets you do anything")]
            public Tmode curveTangentMode;
            public OtherTMode otherTangentMode;

            [Range(0.1f, 1f)]
            public float previewSpawnBallsSize;

            public editValues(float previewSpawnBallsSize = 0.4f)
            {
                curveTangentMode = Tmode.automatic;
                otherTangentMode = OtherTMode.keepRatio;
                this.previewSpawnBallsSize = previewSpawnBallsSize;
            }
        }

        [System.Serializable]
        public struct anchorSettings
        {
            public Vector3 anchor;
            public Vector3 inT;
            public Vector3 outT;

            public anchorSettings(Vector3 anchor, Vector3 inT, Vector3 outT)
            {
                this.anchor = anchor;
                this.inT = inT;
                this.outT = outT;
            }

            public anchorSettings(Vector3 anchor)
            {
                this.anchor = anchor;
                outT = inT = Vector3.zero;
            }
        }

        public enum curveMode { cubic, quadratic, linear };
    }
}
