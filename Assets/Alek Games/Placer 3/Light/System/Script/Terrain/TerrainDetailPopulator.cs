
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AlekGames.Placer3.Shared
{
    public static class TerrainDetailPopulator
    {
        [System.Serializable]
        public struct values
        {
            [Tooltip("index of a detail on details of terrain")]
            public int detailILayer;

            [Space]

            [Range(0, 10), Tooltip("density of detail on a checked spot if decided to place detail there")]
            public int density;
            [Range(0, 100), Tooltip("chance for a bold spot")]
            public float zeroChance;
            [Range(0, 90), Tooltip("max normal angle of ground for detai to be placed")]
            public float maxNormal;

            [Space]

            public Vector2 minmaxHeight;
            [Tooltip("obstacle layer. grass will not be spawned on spot covered by obstacles")]
            public LayerMask obstacles;

            public avoidedTerrainLayer[] avoidedTerrainLayers;

            public values(int density)
            {
                detailILayer = 0;
                this.density = density;
                zeroChance = 10;
                maxNormal = 45;
                minmaxHeight = new Vector2(0, 100);

                obstacles = 0;
                avoidedTerrainLayers = new avoidedTerrainLayer[0];
            }
        }


        [System.Serializable]
        public struct avoidedTerrainLayer
        {
            public int layerIndex;

            public float avoidedWeightOver;
        }

        public static void populate(Terrain terrain, values v)
        {

            if (terrain == null)
            {
                Debug.LogError("no terrain found");
                return;
            }


            Transform transform = terrain.transform;
            TerrainData terrainData = terrain.terrainData;

            int width = terrainData.detailWidth;
            int height = terrainData.detailHeight;
            int[,] details = new int[width, height];


            Vector2 size = new Vector2(terrainData.size.x, terrainData.size.z);

            float widthToPos = size.x / width;
            float heightToPos = size.y / height;

            Vector3 terrainPos = terrain.transform.position;

            for (int y = 0; y < height; y++)
            {

                for (int x = 0; x < width; x++)
                {
                    int chosen = 0;

                    if (!Chance.giveChance(v.zeroChance))
                    {
                        Vector3 pos = transform.position + new Vector3((y * widthToPos), v.minmaxHeight.y, (x * heightToPos));

                        bool alfaMapCorrect = true;

                        if (v.avoidedTerrainLayers.Length > 0)
                        {
                            Vector2Int alphaPos = ConvertPositionToAlphamap(terrainData, terrainPos, pos);

                            foreach (avoidedTerrainLayer atl in v.avoidedTerrainLayers)
                            {
                                float[,,] aMap = terrainData.GetAlphamaps(alphaPos.x, alphaPos.y, 1, 1);
                                if (aMap[0, 0, atl.layerIndex] > atl.avoidedWeightOver) 
                                {
                                    alfaMapCorrect = false;
                                }
                            }
                        }

                        if (alfaMapCorrect)
                        {

                            Physics.Raycast(pos, Vector3.down, out RaycastHit info, Mathf.Abs(v.minmaxHeight.y - v.minmaxHeight.x), 1 << transform.gameObject.layer, QueryTriggerInteraction.Ignore);

                            if (info.point.y > v.minmaxHeight.x && info.point.y < v.minmaxHeight.y)
                            {
                                if (!Physics.Raycast(pos, Vector3.down, Vector3.Distance(pos, info.point) - 0.001f, v.obstacles))
                                {
                                    if (Vector3.Angle(info.normal, Vector3.up) <= v.maxNormal) chosen = v.density;
                                }
                            }
                        }

                    }

                    details[x, y] = chosen;
                }
            }

            terrainData.SetDetailLayer(0, 0, v.detailILayer, details);
        }


        private static Vector2Int ConvertPositionToAlphamap(TerrainData data, Vector3 terrainPos, Vector3 checkedPos)
        {
            Vector3 terrainPosition = checkedPos - terrainPos; ;
            Vector3 mapPosition = new Vector3
            (terrainPosition.x / data.size.x, 0,
            terrainPosition.z / data.size.z);
            float xCoord = mapPosition.x * data.alphamapWidth;
            float zCoord = mapPosition.z * data.alphamapHeight;
            return new Vector2Int( (int)xCoord, (int)zCoord);
        }


        [ContextMenu("clear")]
        public static void clearTerrainLayerDetail(Terrain terrain, values v)
        {

            if (terrain == null)
            {
                Debug.LogError("no terrain found");
                return;
            }

            TerrainData terrainData = terrain.terrainData;

            int width = terrainData.detailWidth;
            int height = terrainData.detailHeight;
            int[,] details = new int[width, height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    details[x, y] = 0;

            terrainData.SetDetailLayer(0, 0, v.detailILayer, details);
        }


#if UNITY_EDITOR

        public static void drawGizmos(Terrain terrain, values v)
        {
            Gizmos.color = Color.red;

            Transform transform = terrain.transform;

            Vector3 one = transform.position + Vector3.up * v.minmaxHeight.x;
            Vector3 two = transform.position + Vector3.up * v.minmaxHeight.y;
            Gizmos.DrawLine(one, two);
            Gizmos.DrawSphere(one, 0.6f);
            Gizmos.DrawSphere(two, 0.6f);
            Color c = Color.red;

            if (terrain == null)
            {
                Debug.LogError("no terrain found");
                return;
            }

            TerrainData terrainData = terrain.terrainData;
            c.a = 0.5f;
            Gizmos.color = c;
            Vector3 terSize = new Vector3(terrainData.size.x, 0, terrainData.size.z);
            Gizmos.DrawCube(one + terSize / 2, terSize);
            Gizmos.DrawCube(two + terSize / 2, terSize);
        }

#endif
    }
}
