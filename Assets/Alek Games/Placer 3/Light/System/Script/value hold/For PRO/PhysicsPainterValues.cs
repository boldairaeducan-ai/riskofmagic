using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using AlekGames.Placer3.Profiles;

namespace AlekGames.Placer3.Shared
{
    [System.Serializable]
    public struct PhysicsPainterValues
    {
        public prefabPalette palette;

        public int specificIndex;



        public bool holdActivation;

        public float holdActivationDistance;



        public float upOffset;

        [Range(0, 150)]
        public float brushSize;

        [Range(0, 200)]
        public int scatterCount;

        [Range(0, 100)]
        public float scatterAvoidenceDistance;



        [Tooltip("if should keep simulated objects in scene, even if the rigidbody is sleeping (not moveing). if false, if object is not moveing, the system will remove it from scene")]
        public bool keepInScene;

        [Tooltip("how ofte should simulation update realtime. this value is in miliseconds"), Min(1)]
        public int simulationDelta;

        [Tooltip("amount of iterations per physics refresh"), Min(1)]
        public int iterations;

        [Tooltip("step for Physics.Simulate per iteration."), Min(0.001f)]
        public float iterationStep;

        public PhysicsPainterValues(prefabPalette p)
        {
            palette = p;
            specificIndex = -1;
            holdActivation = true;
            holdActivationDistance = 2;
            upOffset = 5;
            brushSize = 5;
            scatterCount = 8;
            scatterAvoidenceDistance = 1;
            keepInScene = false;
            simulationDelta = 2;
            iterations = 8;
            iterationStep = 0.02f;
        }
    }
}
