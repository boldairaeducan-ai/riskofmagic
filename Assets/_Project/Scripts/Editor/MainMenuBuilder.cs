using RiskOfMagic.Flow;
using RiskOfMagic.Networking;
using RiskOfMagic.UI;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using H = RiskOfMagic.Editor.HudBuilder;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Builds the MainMenu scene (first scene of the build): persistent NetworkManager, lobby prefab and the menu UI.
    /// Called by PrototypeSceneBuilder so both scenes share the same NetworkManager setup.
    /// </summary>
    public static class MainMenuBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string LobbyPrefabPath = "Assets/_Project/Prefabs/LobbyState.prefab";

        public static GameObject BuildLobbyPrefab()
        {
            var root = new GameObject("LobbyState");
            root.AddComponent<NetworkObject>();
            root.AddComponent<LobbyState>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, LobbyPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static void BuildScene(NetworkPrefabsList prefabList, GameObject lobbyPrefab)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            // The 3D set (own camera, lights, atmosphere) replaces the default camera and light.
            foreach (var go in scene.GetRootGameObjects())
                if (go.GetComponent<Camera>() != null || go.GetComponent<Light>() != null)
                    Object.DestroyImmediate(go);
            PrefabUtility.InstantiatePrefab(MenuBackdropBuilder.GetOrCreate(), scene);

            // Network (persists into the game scene)
            var netGo = new GameObject("NetworkManager");
            var manager = netGo.AddComponent<NetworkManager>();
            var transport = netGo.AddComponent<UnityTransport>();
            netGo.AddComponent<PersistentNetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = null; // avatars are spawned by the game scene (GameBootstrap)
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // UI (UI Toolkit): layout in UI/Menus/MainMenu.uxml, logic in MainMenuController.
            var ui = MenuUiBuilder.EnsureAssets();
            var menuGo = new GameObject("MainMenu");
            var document = menuGo.AddComponent<UnityEngine.UIElements.UIDocument>();
            document.panelSettings = ui.panelSettings;
            document.visualTreeAsset = ui.mainMenu;
            var controller = menuGo.AddComponent<MainMenuController>();
            H.Props(controller, so =>
            {
                so.FindProperty("lobbyPrefab").objectReferenceValue = lobbyPrefab.GetComponent<LobbyState>();
                so.FindProperty("assets").objectReferenceValue = ui;
            });

            PostProcessBuilder.AddVolumes(scene, "MainMenu");
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
    }
}
