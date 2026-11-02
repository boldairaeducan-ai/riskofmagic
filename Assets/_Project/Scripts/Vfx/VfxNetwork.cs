using RiskOfMagic.Core;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Vfx
{
    /// <summary>
    /// Scene singleton replicating server-triggered effects (item procs, explosions).
    /// Prefabs must be registered in GameCatalog.effects.
    /// </summary>
    public class VfxNetwork : NetworkBehaviour
    {
        public static VfxNetwork Instance { get; private set; }

        public override void OnNetworkSpawn() => Instance = this;

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Server only.</summary>
        public static void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale = 1f)
        {
            if (prefab == null)
                return;
            if (TryGetIndex(prefab, out ushort index))
                Instance.SpawnRpc(index, position, rotation, scale);
            else
                VfxUtility.Spawn(prefab, position, rotation, scale);
        }

        /// <summary>Server only.</summary>
        public static void SpawnLine(GameObject prefab, Vector3[] points)
        {
            if (prefab == null || points.Length < 2)
                return;
            if (TryGetIndex(prefab, out ushort index))
                Instance.SpawnLineRpc(index, points);
            else
                VfxUtility.SpawnLine(prefab, points);
        }

        private static bool TryGetIndex(GameObject prefab, out ushort index)
        {
            index = 0;
            if (Instance == null || !Instance.IsSpawned)
                return false;
            int i = GameCatalog.Instance.IndexOfEffect(prefab);
            if (i < 0)
            {
                Debug.LogWarning($"[VfxNetwork] {prefab.name} is not in the GameCatalog effects, only the server will see it.");
                return false;
            }
            index = (ushort)i;
            return true;
        }

        [Rpc(SendTo.Everyone)]
        private void SpawnRpc(ushort index, Vector3 position, Quaternion rotation, float scale)
            => VfxUtility.Spawn(GameCatalog.Instance.GetEffect(index), position, rotation, scale);

        [Rpc(SendTo.Everyone)]
        private void SpawnLineRpc(ushort index, Vector3[] points)
            => VfxUtility.SpawnLine(GameCatalog.Instance.GetEffect(index), points);
    }
}
