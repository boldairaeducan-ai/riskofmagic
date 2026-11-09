using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public class SpawnDistanceGrid
    {
        public Vector3?[,] grid { get; private set; }
        public float sqrAvoidiance { get; private set; }
        public float avoidiance { get; private set; }
        private Vector2Int cellAmmount;
        private Vector2 posRange;

        public SpawnDistanceGrid(Vector2Int cells, Vector2 posRange, float avoidianceRange)
        {
            avoidiance = avoidianceRange; 
            sqrAvoidiance = avoidiance * avoidiance;

            //float RangeToSide = 1.24070098f;//from sphere range to in that sphere box side lengh
            cellAmmount = cells;

            this.posRange = posRange;
            grid = new Vector3?[cellAmmount.x, cellAmmount.y];
            //Debug.Log(cellAmmount);
            //for (int x = 0; x < cellAmmount.x; x++) for (int y = 0; y < cellAmmount.y; y++) grid[x, y] = null;
        }



        public bool canPlace(Vector3 point)
        {
            posToCells(point, out int x, out int y);

            if (x < 0 || x >= cellAmmount.x ||
                y < 0 || y >= cellAmmount.y)
            {
                //Debug.Log("not good " + point.x + " " + x + "   y " + point.y + " " + y);
                return false;
            }
            //Debug.Log(point.x + " x to " + x + "   " + point.z + " z to " + y);

            if (grid[x, y] != null) return false;

            //return true; // for tests

            if (isTooClose(x, y - 1, point)) return false; 
            if (isTooClose(x, y + 1, point)) return false;
            if (isTooClose(x - 1, y , point)) return false; 
            if (isTooClose(x + 1, y, point)) return false;

            if (isTooClose(x + 1, y + 1, point)) return false;
            if (isTooClose(x - 1, y + 1, point)) return false;
            if (isTooClose(x + 1, y - 1, point)) return false;
            if (isTooClose(x - 1, y - 1, point)) return false;

            //grid[x, y] = point;

            return true;
        }

        public void registerPoint(Vector3 point)
        {
            posToCells(point, out int x, out int y);

            grid[x, y] = point;
        }

        private void posToCells(Vector3 point, out int x, out int y)
        {
            x = valueToCell(point.x, posRange.x, cellAmmount.x - 1);
            y = valueToCell(point.z, posRange.y, cellAmmount.y - 1);
        }

        private int valueToCell(float v, float r1, float r2)
        {
            v = Mathf.Clamp(v, 0, r1);
            return (int)(v / r1 * r2);
        }


        private bool isTooClose(int x, int y, Vector3 point)
        {
            if (x < 0 || x >= cellAmmount.x ||
                y < 0 || y >= cellAmmount.y) return false;

            Vector3? p = grid[x, y];
            if (p == null) return false;

            return (p.Value - point).sqrMagnitude <= sqrAvoidiance;
        }


        public static Vector3 toDisanceCompAcceptable(Vector3 v, Vector3 tPos)
        {
            return new Vector3(v.x - tPos.x, 0, v.z - tPos.z);
        }
    }
}
