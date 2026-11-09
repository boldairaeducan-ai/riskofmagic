using AlekGames.Placer3.Profiles;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public static class NoiseSpawnerValues
    {
        [System.Serializable]
        public struct values
        {
            [SerializeField]
            public bool spawnOnAwakeWithRandSeed;
            [Header("Spawn")]
            [SerializeField]
            public prefabPalette palette;
            [SerializeField]
            public int specificIndex;

            [Header("spawn rules")]
            [SerializeField, Min(0), Tooltip("min noise value to attempt spawning an object")]
            public float noiseSpawnThreshold;
            [SerializeField, Range(0, 100), Tooltip("chance of spawning an object in a point")]
            public float spawnChance;
            [SerializeField, Min(0), Tooltip("min distance diffrienc between 2 spawned objects. 0 to disable. i advise to just use placePosScale, if in playmode, couse it will be faster")]
            public float avoidienceDistance;
            [SerializeField, Tooltip("if should add the picked po to avoided pos list even if chance failed")]
            public bool addPosIfChanceFail;

            [Header("placing")]
            [SerializeField, Tooltip("position scale (makes stuff spawn further)")]
            public float placePosScale;
            [SerializeField]
            public float randPosOffset;

            [SerializeField]
            public bool snapToGround;

            [SerializeField]
            public string callOnSpawned;
            [SerializeField]
            public bool autoCallSpawners;
            [SerializeField, Min(1)]
            public int atOnceSpawners;

            [SerializeField, Min(1)] public int perLoopWait;

            public values(prefabPalette palette)
            {
                this.palette = palette;
                spawnOnAwakeWithRandSeed = true;
                specificIndex = -1;
                noiseSpawnThreshold = 0.7f;
                spawnChance = 100;
                avoidienceDistance = 5;
                addPosIfChanceFail = true;
                placePosScale = 1;
                randPosOffset = 1;
                snapToGround = true;
                callOnSpawned = string.Empty;
                autoCallSpawners = false;
                atOnceSpawners = 5;
                perLoopWait = 75;
            }
        }


        [System.Serializable]
        public class noiseSettings
        {
            [Min(1), Tooltip("noise width (x axis)")]
            public int width = 100;
            [Min(1), Tooltip("noise height (z axis)")]
            public int height = 100;
            [Tooltip("noise seed")]
            public int seed;
            [Min(0.001f), Tooltip("noise scale (zoom)")]
            public float scale = 15;
            [Min(1), Tooltip("ammount of noise adddons to the noise")]
            public int octaves = 3;
            [Range(0, 1), Tooltip("affect of octave in comparison to previous one")]
            public float persistance = 0.8f;
            [Tooltip("affect of octave scale in comparison to previous one")]
            public float lacunarity = 0.5f;
            [Tooltip("noise offset")]
            public Vector2 offset;
            [Tooltip("if should clamp noise scale to be between 0-1, while keeping proportions.")]
            public bool proportionClamp01 = true;
        }

    }

}