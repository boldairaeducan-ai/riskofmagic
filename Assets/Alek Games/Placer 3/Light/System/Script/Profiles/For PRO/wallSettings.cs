using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Profiles
{
    [CreateAssetMenu(menuName = "Alek Games/" + PlacerDeafultsData.productName + "/Profiles/Wall Settings", fileName = "new wallSettings")]
    public class wallSettings : ScriptableObject
    {

        [Tooltip("the standing thing, that is conected to one another threw rails")]
        public GameObject post;

        [Space]

        public LayerMask ground = ~0;
        public LayerMask avoided;
        public float avoidianceAccuracy = 0.1f;

        [Space]

        [Tooltip("the thing connecting 2 posts horizontally if you do not use rails, railLen is still needed")]
        public GameObject rail;
        [Tooltip("distance between 2 posts/lengh of a rail")]
        public float railLengh = 2;
        [Tooltip("for each of these, on this height, there will be a rail placed on the post")]
        public float[] railHeights = new float[2] { 0.5f, 1 };

        [Space]

        [Tooltip("forward of model should be right/left of prefab. will be intercepting rails, unless have some offset on right/left")]
        public GameObject picket;
        [Min(0)]
        public int picketCount = 5;

        [Space]
        public bool asPrefab = true;

        public enum toSpawnType { post, rail, picket};


        /// <summary>
        /// spawns wall from given parameters. 
        /// make sure to spawn post on StartPos yourself, as this system assumes it already has one.
        /// </summary>
        /// <param name="StartPos">pos to spawn from</param>
        /// <param name="EndPos">pos to spawn in dir to</param>
        /// <param name="forceGroundSnap">if should snap each wall pice to ground. probably should be set to true, unless you want flying walls</param>
        /// <param name="AllowOverPoint">if should make sure wall crosses the paint point</param>
        /// <param name="parent">spawned objects parent</param>
        /// <returns></returns>
        public Vector3? spawnWall(Vector3 StartPos, Vector3 EndPos, bool AllowOverPoint, bool forceGroundSnap, Transform parent)
        {
            Vector3 FullWallDir = EndPos - StartPos;
            //FullWallDir.y = 0;

            float wallLengh = FullWallDir.magnitude;

            float rl = railLengh;

            float p = wallLengh / rl;
            int posts;

            if (p == ((int)p)) posts = (int)p;
            else if (AllowOverPoint) posts = (int)p + 1;
            else posts = (int)p;

            Vector3 wallMove = FullWallDir.normalized * rl;
            Vector3 picMove = wallMove / (picketCount + 1);

            Vector3 fromPos = StartPos;

            Quaternion postRot = Quaternion.LookRotation(new Vector3(wallMove.x, 0, wallMove.z), Vector3.up);

            for (int i = 0; i < posts; i++)
            {

                Vector3 postPoint;
                Vector3 fPosCopy = fromPos;
                Vector3 nextPoint = fPosCopy + wallMove;


                if (forceGroundSnap)
                {
                    Ray r = new Ray(nextPoint + Vector3.up * 24, Vector3.down);
                    if (Physics.Raycast(r, out RaycastHit info, 48, ground))
                    {
                        if (!Physics.Raycast(r, Vector3.Distance(r.origin, info.point) - 0.001f, avoided))
                            postPoint = info.point;
                        else
                        {
                            fromPos = info.point;
                            continue;
                        }
                    }
                    else
                    {
                        Debug.LogError("couldnt find ground to place a post on. abording spawn");
                        return null;
                    }
                }
                else postPoint = nextPoint;
                fromPos = postPoint;

                spawn(wallSettings.toSpawnType.post, postPoint, postRot, parent);

                Quaternion railLookTo = Quaternion.LookRotation(fPosCopy - postPoint, Vector3.up);
                if (avoided != 0)
                {
                    bool detectedAvoided = false;

                    Vector3 add = railLookTo * Vector3.forward * avoidianceAccuracy;
                    int iter = (int)(railLengh / avoidianceAccuracy);
                    Vector3 current = postPoint + Vector3.up * 24;
                    for (i = 0; i < iter; i++)
                    {
                        Ray r = new Ray(current, Vector3.down);
                        Debug.DrawLine(current, current + add);
                        if (Physics.Raycast(r, out RaycastHit info, 48, ground))
                            if (Physics.Raycast(r, Vector3.Distance(r.origin, info.point) - 0.001f, avoided))
                            {
                                detectedAvoided = true;
                                break;
                            }

                        current += add;
                    }

                    if (detectedAvoided) continue;
                    
                }


                if (picket != null)
                {
                    for (int pic = 1; pic <= picketCount; pic++)
                    {
                        Vector3 picPoint;
                        Vector3 picNextPos = fPosCopy + picMove * pic;
                        if (forceGroundSnap)
                        {
                            if (Physics.Raycast(picNextPos + Vector3.up * 24, Vector3.down, out RaycastHit picInfo, 48, ground))
                            {
                                picPoint = picInfo.point;
                            }
                            else
                            {
                                Debug.LogError("couldnt find ground to place a picket on. abording spawn");
                                return null;
                            }
                        }
                        else picPoint = picNextPos;

                        spawn(wallSettings.toSpawnType.picket, picPoint, postRot, parent);
                    }
                }


                if (rail != null)
                {
                    foreach (float r in railHeights)
                    {
                        Vector3 heightAdd = Vector3.up * r;
                        Vector3 railPos = postPoint + heightAdd;
                        spawn(wallSettings.toSpawnType.rail, railPos, railLookTo, parent);
                    }
                }

                fromPos = postPoint;
            }

            return fromPos;
        }


        public GameObject snapSpawnPost(Vector3 pos, Quaternion rot, Transform parent, out Vector3 spawnedPos)
        {
            Ray r = new Ray(pos + Vector3.up * 24, Vector3.down);

            GameObject g = null;
            spawnedPos = pos;

            if (Physics.Raycast(r, out RaycastHit info, 48, ground))
                if (!Physics.Raycast(r, Vector3.Distance(r.origin, info.point) - 0.001f, avoided))
                {
                    spawnedPos = info.point;
                    g = spawn(toSpawnType.post, spawnedPos, rot, parent);
                }

            return g;
        }

        public GameObject spawn(toSpawnType toSpawn, Vector3 pos, Quaternion rot, Transform parent)
        {
            GameObject toSpawnP = null;

            switch (toSpawn)
            {
                case toSpawnType.post:
                    toSpawnP = post;
                    break;
                case toSpawnType.rail:
                    toSpawnP = rail;
                    break;
                case toSpawnType.picket:
                    toSpawnP = picket;
                    break;
            }

            GameObject spawned = null;

#if UNITY_EDITOR
            if (asPrefab)
            {
                spawned = (UnityEditor.PrefabUtility.InstantiatePrefab(toSpawnP) as GameObject);
                spawned.transform.position = pos;
                spawned.transform.rotation = rot;
            }

            UnityEditor.EditorUtility.SetDirty(spawned);
#endif      
            if (spawned == null) spawned = Instantiate(toSpawnP, pos, rot);


            spawned.transform.parent = parent;
            return spawned;
        }
    }
}
