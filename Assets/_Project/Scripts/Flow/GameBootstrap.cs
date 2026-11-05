using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskOfMagic.Flow
{
    /// <summary>
    /// Map scene entry point (server). First stage: spawns the run systems and one player object per client,
    /// all persistent across map scenes. Later stages (new map scene): tells the StageManager the map is ready.
    /// Works when loaded from the lobby/previous stage (waits for every client to finish loading) and when
    /// hosting directly from this scene (debug).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        [SerializeField] private List<NetworkObject> serverPrefabs = new();
        [SerializeField, Tooltip("Safety net if the scene load event was missed.")] private float startTimeout = 8f;

        private NetworkManager manager;
        private bool started;
        private bool loadEventSubscribed;

        // Awake: when only the host has to load, Netcode completes the load event before Start runs
        // (it was missed and the stage only started after startTimeout).
        private void Awake()
        {
            manager = NetworkManager.Singleton;
            if (manager != null && manager.IsListening && manager.IsServer && manager.SceneManager != null)
            {
                manager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
                loadEventSubscribed = true;
            }
        }

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

            // Loaded from the lobby / previous stage: wait until every client has loaded the scene.
            if (!loadEventSubscribed)
            {
                manager.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
                loadEventSubscribed = true;
            }
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

            foreach (ulong clientId in manager.ConnectedClientsIds)
                SpawnPlayer(clientId);

            if (RiskOfMagic.Run.StageManager.Instance != null)
            {
                RiskOfMagic.Run.StageManager.Instance.OnMapSceneReady(); // run systems survived the scene change
                return;
            }
            foreach (var prefab in serverPrefabs)
            {
                if (prefab == null)
                    continue;
                var instance = Instantiate(prefab);
                instance.name = prefab.name;
                instance.Spawn(false); // kept across map scenes
            }
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
            Instantiate(playerPrefab).SpawnAsPlayerObject(clientId, false); // inventory etc. kept across map scenes
        }
    }
}
