using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using AlekGames.Placer3.Profiles;

namespace AlekGames.Placer3.Shared
{
    [System.Serializable]
    public struct PainterValues
    {
        public enum massReplaceDestroyM { exact, massByDistance, massByColliders};
        public enum paintM { scatter, exact, remove, replace };

        public enum holdActivationM { disabled, enabledSimple, enabledRotateWithDragDir}

        public paintM paintMode;

        public prefabPalette palette;

        public int specificIndex;

        public holdActivationM holdActivation;

        public bool ignoreFirstHoldPlace;

        public float holdActivationDistance;

        public float rotateByDegrees;

        [Tooltip("if greater than 0, grid placing is enabled"), Min(0)]
        public float placeGridSize;

        public bool planeProjection;
        public Plane projectedPlane;

        [Range(0, 150)]
        public float brushSize;

        [Range(0, 200)]
        public int scatterCount;

        [Range(0, 100)]
        public float scatterAvoidenceDistance;

        public massReplaceDestroyM massReplaceDestroyMode;

        [Min(1)]
        public int findCount;


        public bool spawnPreview;

        public PainterValues(prefabPalette palette)
        {
            paintMode = paintM.exact;
            this.palette = palette;
            specificIndex = -1;
            holdActivation = holdActivationM.enabledSimple;
            ignoreFirstHoldPlace = false;
            holdActivationDistance = 2;
            rotateByDegrees = 45;
            placeGridSize = 0;
            planeProjection = true;
            projectedPlane = new Plane(Vector3.up, 0);
            brushSize = 5;
            scatterCount = 8;
            scatterAvoidenceDistance = 1;
            massReplaceDestroyMode = massReplaceDestroyM.exact;
            findCount = 2;
            spawnPreview = true;
        }
    }
}
