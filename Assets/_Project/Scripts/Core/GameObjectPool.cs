using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Core
{
    /// <summary>Prefab-keyed pool for non-networked objects (projectile visuals, VFX).</summary>
    public static class GameObjectPool
    {
        private static readonly Dictionary<GameObject, Stack<PooledObject>> pools = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => pools.Clear();

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!pools.TryGetValue(prefab, out var stack))
                pools[prefab] = stack = new Stack<PooledObject>();

            PooledObject item = null;
            while (item == null && stack.Count > 0)
                item = stack.Pop(); // entries can be destroyed by a scene unload

            if (item == null)
            {
                var go = Object.Instantiate(prefab, position, rotation);
                if (!go.TryGetComponent(out item))
                    item = go.AddComponent<PooledObject>();
                item.Prefab = prefab;
            }
            else
            {
                item.transform.SetPositionAndRotation(position, rotation);
                item.transform.localScale = prefab.transform.localScale;
                item.gameObject.SetActive(true);
            }

            item.InPool = false;
            return item.gameObject;
        }

        public static void Release(GameObject instance)
        {
            if (instance.TryGetComponent(out PooledObject item) && item.Prefab != null && pools.TryGetValue(item.Prefab, out var stack))
            {
                if (item.InPool)
                    return;
                item.InPool = true;
                instance.SetActive(false);
                stack.Push(item);
            }
            else
            {
                Object.Destroy(instance);
            }
        }
    }

    public sealed class PooledObject : MonoBehaviour
    {
        public GameObject Prefab { get; set; }
        public bool InPool { get; set; }
    }
}
