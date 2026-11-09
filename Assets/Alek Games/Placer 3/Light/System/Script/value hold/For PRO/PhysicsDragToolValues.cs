using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using AlekGames.Placer3.Profiles;

namespace AlekGames.Placer3.Shared
{
    [System.Serializable]
    public class PhysicsDragToolValues
    {
        public float moveSpeed;
        public float rotateSpeed;

        [Tooltip("how ofte should simulation update realtime. this value is in miliseconds"), Min(1)]
        public int simulationDelta;

        [Tooltip("amount of iterations per physics refresh"), Min(1)]
        public int iterations;

        [Tooltip("step for Physics.Simulate per iteration."), Min(0.001f)]
        public float iterationStep;



        public PhysicsDragToolValues(float mass)
        {
            moveSpeed = 3;
            rotateSpeed = 15;
            simulationDelta = 2;
            iterations = 3;
            iterationStep = 0.01f;
        }
    }
}
