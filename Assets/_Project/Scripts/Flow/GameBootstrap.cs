using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskOfMagic.Flow
{
    /// <summary>
    /// Game scene entry point (server): spawns run systems and one player object per connected client.
    /// Works when the scene is loaded from the lobby (waits for every client to finish loading) and when
    /// hosting directly from this scene (debug).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private List<NetworkObject> serverPrefabs = new();
        [SerializeField, Tooltip("Safety net if the scene load event was missed.")] private float startTimeout = 8f;

        private NetworkManager manager;
        private bool started;

        private IEnumerator Start()
        {
            manager = NetworkManager.Singleton;
            if (manager == null)
                yield break;

            manager.OnClientConnectedCallback += OnClientConnected;
            if (!manager.IsListening)
            {
                manager.OnServerStarted += Begin; // debug: Host pressed in this scene
                yield break;
            }
            if (!manager.IsServer)
                yield break;

            // Loaded from the lobby: wait until every client has loaded the scene.
            manager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
            yield return new WaitForSeconds(startTimeout);
            Begin();
        }

        private void OnDestroy()
        {
            if (manager == null)
                return;
            manager.OnClientConnectedCallback -= OnClientConnected;
            manager.OnServerStarted -= Begin;
            if (manager.SceneManager != null)
                manager.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
        }

        private void OnLoadEventCompleted(string sceneName, LoadSceneMode mode, List<ulong> completed, List<ulong> timedOut)
        {
            if (sceneName == gameObject.scene.name)
                Begin();
        }

        private void Begin()
        {
            if (started || manager == null || !manager.IsServer)
                return;
            started = true;

            foreach (var prefab in serverPrefabs)
            {
                if (prefab == null)
                    continue;
                var instance = Instantiate(prefab);
                instance.name = prefab.name;
                instance.Spawn(true);
            }
            foreach (ulong clientId in manager.ConnectedClientsIds)
                SpawnPlayer(clientId);
        }

        private void OnClientConnected(ulong clientId)
        {
            if (started && manager.IsServer)
                SpawnPlayer(clientId);
        }

        private void SpawnPlayer(ulong clientId)
        {
            if (playerPrefab == null || (manager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null))
                return;
            Instantiate(playerPrefab).SpawnAsPlayerObject(clientId, true);
        }
    }
}
