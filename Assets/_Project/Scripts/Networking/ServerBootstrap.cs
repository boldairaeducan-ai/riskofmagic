using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Networking
{
    /// <summary>
    /// Spawns run-wide networked systems when the server starts. Avoids in-scene placed NetworkObjects,
    /// whose GlobalObjectIdHash is only generated when edited by hand in the editor.
    /// Prefabs must be registered in the NetworkPrefabs list.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class ServerBootstrap : MonoBehaviour
    {
        [SerializeField] private List<NetworkObject> serverPrefabs = new();

        private NetworkManager manager;

        private void Awake() => manager = GetComponent<NetworkManager>();
        private void OnEnable() => manager.OnServerStarted += OnServerStarted;

        private void OnDisable()
        {
            if (manager != null)
                manager.OnServerStarted -= OnServerStarted;
        }

        private void OnServerStarted()
        {
            foreach (var prefab in serverPrefabs)
            {
                if (prefab == null)
                    continue;
                var instance = Instantiate(prefab);
                instance.name = prefab.name;
                instance.Spawn(true);
            }
        }
    }
}
