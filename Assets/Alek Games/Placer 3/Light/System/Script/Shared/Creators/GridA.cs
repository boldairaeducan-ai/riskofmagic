using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public static class GridA
    {

        public static Vector3[] getGrid(GridSettings grid)
        {
            float xLen = grid.xLen;
            int xSpawn = grid.xSpawn;

            float yLen = grid.yLen;
            int ySpawn = grid.ySpawn;

            float zLen = grid.zLen;
            int zSpawn = grid.zSpawn;


            List<Vector3> points = new List<Vector3>(xSpawn * zSpawn * ySpawn);

            for (int y = 0; y < ySpawn; y++)
            {
                float ySpace = y * yLen;
                for (int x = 0; x < xSpawn; x++)
                {
                    float xSpace = x * xLen;
                    for (int z = 0; z < zSpawn; z++) points.Add(new Vector3(xSpace, ySpace, z * zLen));              
                }
            }

            return points.ToArray();
        }

        [System.Serializable]
        public class GridSettings
        {
            [Range(1, 300), Tooltip("spawn iterations on axis x (ammount of grid points spawned on axis x)")]
            public int xSpawn = 15;
            [Range(0.1f, 30f), Tooltip("distance between grid points on axis x")]
            public float xLen = 1f;

            [Range(1, 300), Tooltip("spawn iterations on axis z (ammount of grid points spawned on axis z)")]
            public int zSpawn = 15;
            [Range(0.1f, 30f), Tooltip("distance between grid points on axis z")]
            public float zLen = 1f;

            [Range(1, 300), Tooltip("spawn iterations on axis y (ammount of grid points spawned on axis y)")]
            public int ySpawn = 1;
            [Range(0.1f, 30f), Tooltip("distance between grid points on axis y")]
            public float yLen = 1f;

        }
    }
}
