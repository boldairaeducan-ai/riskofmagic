using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;

using AlekGames.Placer3.Profiles;
using AlekGames.Placer3.Shared;


namespace AlekGames.Placer3.Systems.Main
{
    public class PoissonSamplingSpawner : MonoBehaviour, ISpawner, IPrefabPalleteUser
    {
        [SerializeField]
        prefabPalette palette;
        [SerializeField]
        private int specificIndex = -1;

        [SerializeField]
        private int samples = 100;

        [SerializeField, Min(0.1f)]
        private float avoidiance = 3;

        [SerializeField]
        private bool onGround = true;

        [SerializeField]
        private Vector2 gridSize = new Vector2(100, 100);


        
        public Task spawn()
        {
            SpawnDistanceGrid disCheck = new SpawnDistanceGrid(
                new Vector2Int(Mathf.CeilToInt(gridSize.x / avoidiance), Mathf.CeilToInt(gridSize.y / avoidiance)),
                gridSize,
                avoidiance);

            Vector3 tPos = transform.position;


            for (int i = 0; i < samples; i++)
            {
                //Debug.Log("sample");
                Vector3 pos = tPos + new Vector3(
                    Random.Range(0, gridSize.x),
                    0,
                    Random.Range(0, gridSize.y)
                    );

                if (disCheck.canPlace(SpawnDistanceGrid.toDisanceCompAcceptable(pos, tPos)))
                {
                    disCheck.registerPoint(SpawnDistanceGrid.toDisanceCompAcceptable(pos, tPos));

                    if (onGround)
                    {
                        //Debug.Log("a");
                        if (Physics.Raycast(pos, Vector3.down, out RaycastHit info, Mathf.Abs(transform.position.y * 2) + 150, palette.groundLayer))
                        {
                            //Debug.Log("b");

                            if (!Physics.Raycast(pos, Vector3.down, Vector3.Distance(pos, info.point) - 0.001f, palette.avoidedLayer))
                            {
                                //Debug.Log("c");

                                bool angleCorrect = false;
                                if (palette.maxNormal != 180)
                                {
                                    if (Vector3.Angle(Vector3.up, info.normal) <= palette.maxNormal) angleCorrect = true;
                                }
                                else angleCorrect = true;

                                if (angleCorrect)
                                {
                                    GameObject g = palette.spawn(info.point, info.normal, transform, 0, specificIndex);
                                }
                            }
                        }
                    }
                    else
                    {
                        GameObject g = palette.spawn(pos, Vector3.up, transform, 0, specificIndex);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public void setPalette(prefabPalette palette)
        {
            this.palette = palette;
        }
        public prefabPalette getPalette()
        {
            return palette;
        }

        public void setSpecificIndex(int index) => specificIndex = index;
        public int getSpecificIndex()
        {
            return specificIndex;
        }


#if UNITY_EDITOR

        private void OnDrawGizmosSelected()
        {
            Color c = Color.cyan;
            c.a = 0.4f;
            Gizmos.color = c;
            Gizmos.DrawCube(transform.position + new Vector3(gridSize.x / 2, 0, gridSize.y / 2), new Vector3(gridSize.x, 0, gridSize.y));
        }

#endif
    }
}
