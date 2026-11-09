using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using AlekGames.Placer3.Shared;

namespace AlekGames.Placer3.Profiles
{
    [CreateAssetMenu(menuName = "Alek Games/" + PlacerDeafultsData.productName + "/Profiles/Prefab Palette", fileName = "new PrefabPalette")]
    public class prefabPalette : ScriptableObject
    {
        [Tooltip("this is only for autoAssighn")]
        public GameObject[] objects = new GameObject[0];

        public objectSettings[] prefabs = new objectSettings[0];

        [Space]

        public bool spawnAsPrefab = true;

        [Space]

        [Tooltip("set to 0,0 to use transform settings")]
        public Vector2 minMaxScale = new Vector2(0.9f, 1.1f);
        [Tooltip("x,y,z random rotation addons. random rotation on each axis will be from -value to value, so 180 is fully random on axis, 90 is 180 rotation freedom 0 is none and so on"), Min(0)]
        public Vector3 randRotAdd = new Vector3(0, 180, 0);

        [Space]

        [Range(0, 180)]
        public float maxNormal = 60;

        [Space]

        public LayerMask groundLayer;
        public LayerMask avoidedLayer;

        #region functions

        public GameObject spawn(Vector3 pos, Vector3 surfaceNormal, Transform parent, float YRotationOffset, int specificIndex)
        {
            Transform spawned = null;

            objectSettings s = getSettings(specificIndex);
            GameObject toSpawn = s.prefab;
#if UNITY_EDITOR
            if (spawnAsPrefab)
            {
                spawned = (UnityEditor.PrefabUtility.InstantiatePrefab(toSpawn) as GameObject).transform;
                spawned.position = pos;
                spawned.rotation = toSpawn.transform.rotation;
            }
#endif      
            if (spawned == null) spawned = Instantiate(toSpawn, pos, toSpawn.transform.rotation).transform;

            setRotation(spawned, surfaceNormal, YRotationOffset, s);
            setScale(spawned, s);

            spawned.parent = parent; //set parent after scaling/positioning

            if (s.snapChildren) snapChildrenOf(spawned, s);

            GameObject sp = spawned.gameObject;
#if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(sp, "spawned object from palette");
            UnityEditor.EditorUtility.SetDirty(sp);
#endif
            return sp;
        }

        /// <summary>
        /// updates given object, as if it was spawned with this palette.
        /// usen when object changed position/rotation, and you want to update it to correct rotation
        /// </summary>
        /// <param name="previewObj"></param>
        /// <param name="toPos"></param>
        /// <param name="toNomal"></param>
        public GameObject updatePreviewWithSettings(GameObject previewObj, Vector3 toPos, Vector3 toNomal, float YRotationOffset, int specificIndex, bool reMake, objectSettings before, out objectSettings change)
        {
            if (previewObj == null) reMake = true;

            change = before;

            if (reMake)
            {
                if (previewObj != null) DestroyImmediate(previewObj);
                change = getSettings(specificIndex);
                GameObject toSpawn = change.prefab;
                previewObj = Instantiate(toSpawn, toPos, toSpawn.transform.rotation);

                Collider colR = previewObj.GetComponent<Collider>();
                if (colR != null) DestroyImmediate(colR);
                Collider[] colC = previewObj.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colC.Length; i++)
                {
                    DestroyImmediate(colC[i]);
                }
            }

            previewObj.transform.position = toPos;
            setRotation(previewObj.transform, toNomal, YRotationOffset, change, reMake);
            if (reMake) setScale(previewObj.transform, change);
            if (change.snapChildren) snapChildrenOf(previewObj.transform, change);

            return previewObj;
        }

        private void snapChildrenOf(Transform spt, objectSettings s)
        {
            if (spt.childCount > 0)
            {
                for (int i = 0; i < spt.childCount; i++)
                {
                    Transform child = spt.GetChild(i);
                    if (Physics.Raycast(child.position + Vector3.up * 6, Vector3.down, out RaycastHit info, 50, groundLayer))
                    {
                        child.position = info.point;
                        child.up = Vector3.Lerp(Vector3.up, info.normal, s.normalAllighn);
                    }
                }
            }
        }

        private void setRotation(Transform obj, Vector3 normal, float YOffset, objectSettings s, bool randomize = true)
        {

            obj.up = Vector3.Lerp(Vector3.up, normal, s.normalAllighn).normalized;
            if (randomize)
            {
                obj.Rotate(Random.Range(-randRotAdd.x, randRotAdd.x),
                    Random.Range(-randRotAdd.y, randRotAdd.y),
                    Random.Range(-randRotAdd.z, randRotAdd.z), Space.Self);
            }

            obj.rotation *= Quaternion.AngleAxis(YOffset, obj.up);
        }

        private void setScale(Transform obj, objectSettings s)
        {
            if (minMaxScale != Vector2.zero)
            {
                obj.localScale = Vector3.one;
                obj.localScale *= Random.Range(minMaxScale.x, minMaxScale.y);
            }

            if (s.minMaxScaleMltp != Vector2.zero)
            {
                obj.localScale *= Random.Range(s.minMaxScaleMltp.x, s.minMaxScaleMltp.y);
            }
        }

        /// <summary>
        /// gets an object on specific index from palette
        /// </summary>
        /// <param name="specificIndex"></param>
        /// <returns></returns>
        public objectSettings getSettings(int specificIndex)
        {
            return specificIndex >= 0 ?
                prefabs[specificIndex] :
                prefabs.OrderBy(t => Random.Range(-1, t.weight)).First();
        }

        #endregion

        [System.Serializable]
        public struct objectSettings
        {
            public GameObject prefab;
            [Min(0)]
            public float weight;

            [Tooltip("this is additional mltp to the main one of this palette. will be used even if the normal one is 0,0 (disabled), and this one is not 0,0. remembre that this is mltp not overright so if you disable the main minmax scale, the spawned object scale will be the same as prefab, but mltp'ed")]
            public Vector2 minMaxScaleMltp;

            [Range(0, 1)]
            public float normalAllighn;

            [Tooltip("if should snap the children of spawned object. perfect if you have a prefab containing a little village, fence etc. .when snapping children, avoided layer does not count")]
            public bool snapChildren;

            public objectSettings(GameObject obj)
            {
                prefab = obj;
                weight = 1;
                minMaxScaleMltp = Vector2.one;
                normalAllighn = 0.5f;
                snapChildren = false;
            }
        }

    }
}
