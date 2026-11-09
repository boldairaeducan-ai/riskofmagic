using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

using AlekGames.Placer3.Profiles;
using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Systems.Main
{
    public class GridSpawner : MonoBehaviour, ISpawner, IPrefabPalleteUser
    {
        [SerializeField]
        private prefabPalette palette;

        [Space]

        [SerializeField, Range(0, 100), Tooltip("chance of spawning an object in a point")]
        private float spawnChance = 100;

        [Space]

        [SerializeField]
        private float randPosOffset = 0;

        [Space]

        [SerializeField]
        private bool snapToGround = true;

        [Space]

        [SerializeField]
        private string callOnSpawned;
        [SerializeField]
        private bool autoCallSpawners = false;
        [SerializeField, Min(1)]
        private int atOnceSpawners = 5;

        [Space]

        [SerializeField]
        private int specificIndex = -1;

        [Space]

        [SerializeField]
        private GridA.GridSettings gridSettings;



        public async Task spawn()
        {
            Vector3[] grid = GridA.getGrid(gridSettings);

            int curSpawners = 0;

            Vector3 tPos = transform.position;

            foreach (Vector3 p in grid)
            {
                if (!Chance.giveChance(spawnChance)) continue;


                Vector3 pos = p + tPos;
                Vector3 up = Vector3.up;

                if (snapToGround)
                {
                    Ray r = new Ray(pos + new Vector3(Random.Range(-randPosOffset, randPosOffset), 0, Random.Range(-randPosOffset, randPosOffset)), Vector3.down);
                    if (Physics.Raycast(r, out RaycastHit info, 300, palette.groundLayer, QueryTriggerInteraction.Ignore))
                    {
                        if (Physics.Raycast(r, Vector3.Distance(r.origin, info.point), palette.avoidedLayer)) continue;

                        bool angleCorrect = false;
                        if (palette.maxNormal != 180)
                        {
                            if (Vector3.Angle(Vector3.up, info.normal) <= palette.maxNormal) angleCorrect = true;
                        }
                        else angleCorrect = true;

                        if (!angleCorrect) continue;

                        pos = info.point;
                        up = info.normal;
                    }
                    else
                    {
                        continue;
                    }
                }

                GameObject spawned = palette.spawn(pos, up, transform, 0, specificIndex);

                if (callOnSpawned != string.Empty) spawned.SendMessage(callOnSpawned, SendMessageOptions.DontRequireReceiver);

                if (autoCallSpawners)
                {
                    ISpawner s = spawned.GetComponent<ISpawner>();
                    if (s != null)
                    {
                        Task t = s.spawn();
                        curSpawners++;

                        if (curSpawners >= atOnceSpawners)
                        {
                            await t;
                            curSpawners = 0;
                        }
                    }
                }
            }
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
        [HideInInspector]
        public bool drawGizmos = true;

        [HideInInspector]
        public float gizmoDistance = 135;

        private void OnDrawGizmosSelected()
        {
            if (!drawGizmos) return;

            Vector3[] grid = GridA.getGrid(gridSettings);

            float sqrRenderDistance = gizmoDistance * gizmoDistance;
            Vector3 curCamPos = UnityEditor.SceneView.lastActiveSceneView.camera.transform.position;

            Vector3 size = new Vector3(gridSettings.xLen, gridSettings.yLen, gridSettings.zLen) * 0.3f;

            Vector3 tPos = transform.position;

            Gizmos.color = Color.red;

            for (int i = 0; i < grid.Length; i++)
            {
                Vector3 pos = grid[i] + tPos;
                if ((pos - curCamPos).sqrMagnitude <= sqrRenderDistance) Gizmos.DrawLine(pos, pos + Vector3.up);     
            }

        }

#endif
    }
}
