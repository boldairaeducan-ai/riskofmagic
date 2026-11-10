using System.Collections.Generic;
using System.IO;
using System.Linq;
using RiskOfMagic.Flow;
using RiskOfMagic.Maps;
using RiskOfMagic.Networking;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace RiskOfMagic.Editor
{
    /// <summary>Map authoring: new map scene from a template, validation of the open map, map rotation asset.</summary>
    public static class MapTools
    {
        private const string MapsFolder = "Assets/_Project/Scenes/Maps";
        private const string RotationPath = "Assets/_Project/Configs/MapRotation.asset";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Player.prefab";
        private const string GameSystemsPrefabPath = "Assets/_Project/Prefabs/GameSystems.prefab";
        private const string NetworkPrefabsPath = "Assets/_Project/Configs/NetworkPrefabs.asset";

        /// <summary>Creates the rotation (3 stages using the Prototype map) if missing; never overwrites it.</summary>
        public static MapRotation LoadOrCreateRotation()
        {
            var rotation = AssetDatabase.LoadAssetAtPath<MapRotation>(RotationPath);
            if (rotation != null)
                return rotation;
            rotation = ScriptableObject.CreateInstance<MapRotation>();
            for (int i = 0; i < 3; i++)
                rotation.stages.Add(new MapRotation.StagePool { scenes = { GameFlow.GameScene } });
            AssetDatabase.CreateAsset(rotation, RotationPath);
            return rotation;
        }

        /// <summary>Everything a generated map scene needs; geometry comes from BuildLevel (under the Level root).</summary>
        internal sealed class MapSpec
        {
            public string SceneName;
            public Vector3 BoundsCenter = new(0f, 10f, 0f);
            public Vector3 BoundsSize = new(110f, 40f, 110f);
            /// <summary>Center of the 4 player spawns (ground position).</summary>
            public Vector3 PlayerSpawn = new(0f, 0f, -3f);
            public float PlayerSpawnYaw;
            /// <summary>Builds the walkable geometry, variants and SpawnSpots under the Level root.</summary>
            public System.Action<Transform> BuildLevel;
            /// <summary>Optional: lighting, fog, weather (scene is active).</summary>
            public System.Action<Transform> Atmosphere;
        }

        internal static bool PrefabsReady(out GameObject player, out GameObject systems, out NetworkPrefabsList prefabList, out GameObject hud)
        {
            player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            systems = AssetDatabase.LoadAssetAtPath<GameObject>(GameSystemsPrefabPath);
            prefabList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsPath);
            hud = AssetDatabase.LoadAssetAtPath<GameObject>(HudBuilder.PrefabPath);
            return player != null && systems != null && prefabList != null && hud != null;
        }

        internal static string ScenePathFor(string sceneName) => $"{MapsFolder}/{sceneName}.unity";

        [MenuItem("Tools/Risk of Magic/Maps/New Map Scene")]
        public static void CreateMap()
        {
            if (!PrefabsReady(out _, out _, out _, out _))
            {
                EditorUtility.DisplayDialog("New Map", "Run Tools → Risk of Magic → Build Prototype Scene first (prefabs missing).", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Directory.CreateDirectory(MapsFolder);
            string path = AssetDatabase.GenerateUniqueAssetPath($"{MapsFolder}/Map_New.unity");
            CreateMapScene(new MapSpec
            {
                SceneName = Path.GetFileNameWithoutExtension(path),
                BuildLevel = level =>
                {
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    ground.name = "Ground";
                    ground.transform.SetParent(level);
                    ground.transform.localScale = new Vector3(12f, 1f, 12f);

                    var variants = new GameObject("Variants_Example");
                    variants.transform.SetParent(level);
                    variants.AddComponent<LayoutVariantGroup>();
                    for (int i = 0; i < 2; i++)
                    {
                        var option = new GameObject(i == 0 ? "OptionA" : "OptionB");
                        option.transform.SetParent(variants.transform);
                        var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        block.transform.SetParent(option.transform);
                        block.transform.position = new Vector3(i == 0 ? -15f : 15f, 1.5f, 15f);
                        block.transform.localScale = new Vector3(6f, 3f, 6f);
                    }

                    var spots = new GameObject("SpawnSpots");
                    spots.transform.SetParent(level);
                    AddSpot(spots, SpawnSpotType.Teleporter, new Vector3(-40f, 0f, 40f));
                    AddSpot(spots, SpawnSpotType.Teleporter, new Vector3(40f, 0f, 40f));
                    AddSpot(spots, SpawnSpotType.Merchant, new Vector3(0f, 0f, 20f));
                }
            });
        }

        /// <summary>Creates, bakes, saves and adds to the build a map scene. Returns its path (null if prefabs are missing).</summary>
        internal static string CreateMapScene(MapSpec spec)
        {
            if (!PrefabsReady(out var player, out var systems, out var prefabList, out var hud))
                return null;
            Directory.CreateDirectory(MapsFolder);
            string path = ScenePathFor(spec.SceneName);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // Level root: everything walkable goes under it.
            var level = new GameObject("Level");
            var bounds = level.AddComponent<StageBounds>();
            var boundsSo = new SerializedObject(bounds);
            boundsSo.FindProperty("center").vector3Value = spec.BoundsCenter;
            boundsSo.FindProperty("size").vector3Value = spec.BoundsSize;
            boundsSo.ApplyModifiedPropertiesWithoutUndo();
            level.AddComponent<MapLayout>();
            level.AddComponent<MapLayoutClientSync>();
            spec.BuildLevel?.Invoke(level.transform);

            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

            var spawns = new GameObject("PlayerSpawns");
            spawns.transform.SetPositionAndRotation(spec.PlayerSpawn, Quaternion.Euler(0f, spec.PlayerSpawnYaw, 0f));
            for (int i = 0; i < 4; i++)
            {
                var point = new GameObject($"Spawn_{i}");
                point.transform.SetParent(spawns.transform, false);
                point.transform.localPosition = new Vector3((i - 1.5f) * 2.5f, 0.1f, 0f);
                point.AddComponent<PlayerSpawnPoint>();
            }

            var bootstrap = new GameObject("GameBootstrap").AddComponent<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("playerPrefab").objectReferenceValue = player.GetComponent<NetworkObject>();
            var list = so.FindProperty("serverPrefabs");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = systems.GetComponent<NetworkObject>();
            so.ApplyModifiedPropertiesWithoutUndo();

            // Direct testing from this scene (discarded when arriving from the menu / previous stage).
            var netGo = new GameObject("NetworkManager");
            var manager = netGo.AddComponent<NetworkManager>();
            var transport = netGo.AddComponent<UnityTransport>();
            netGo.AddComponent<PersistentNetworkManager>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = null;
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);

            PrefabUtility.InstantiatePrefab(hud);
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            spec.Atmosphere?.Invoke(level.transform);
            AddMapMusic(spec.SceneName);
            PostProcessBuilder.AddVolumes(scene, spec.SceneName);

            Physics.SyncTransforms();
            surface.BuildNavMesh();
            EditorSceneManager.SaveScene(scene, path);
            if (surface.navMeshData != null)
            {
                string navPath = Path.ChangeExtension(path, null) + "_NavMesh.asset";
                AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
                EditorSceneManager.SaveScene(scene, path);
            }

            var buildScenes = EditorBuildSettings.scenes.ToList();
            if (!buildScenes.Any(s => s.path == path))
            {
                buildScenes.Add(new EditorBuildSettingsScene(path, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }
            Debug.Log($"[Risk of Magic] Map created at {path} (in the build). Add '{spec.SceneName}' to {RotationPath} to play it.");
            return path;
        }

        private const string MusicFolder = "Assets/_Project/Audio/Music";

        /// <summary>SceneMusic pointing to the map's music asset (created empty once, never overwritten: plug the clips there).</summary>
        private static void AddMapMusic(string sceneName)
        {
            if (!AssetDatabase.IsValidFolder(MusicFolder))
            {
                Directory.CreateDirectory(MusicFolder);
                AssetDatabase.Refresh();
            }
            string path = $"{MusicFolder}/{sceneName}_Music.asset";
            var asset = AssetDatabase.LoadAssetAtPath<RiskOfMagic.Audio.MusicAsset>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<RiskOfMagic.Audio.MusicAsset>();
                AssetDatabase.CreateAsset(asset, path);
            }
            var music = new GameObject("SceneMusic").AddComponent<RiskOfMagic.Audio.SceneMusic>();
            var so = new SerializedObject(music);
            so.FindProperty("slot").enumValueIndex = (int)RiskOfMagic.Audio.MusicSlot.Stage;
            so.FindProperty("music").objectReferenceValue = asset;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Replaces the rotation pools if it still is the default one (Prototype only); returns false otherwise.</summary>
        internal static bool TrySetDefaultRotation(IList<string> scenePerStage)
        {
            var rotation = LoadOrCreateRotation();
            bool isDefault = rotation.stages.All(stage => stage.scenes.All(scene => scene == GameFlow.GameScene));
            if (!isDefault)
                return false;
            rotation.stages.Clear();
            foreach (string scene in scenePerStage)
                rotation.stages.Add(new MapRotation.StagePool { scenes = { scene } });
            EditorUtility.SetDirty(rotation);
            AssetDatabase.SaveAssets();
            return true;
        }

        [MenuItem("Tools/Risk of Magic/Maps/Validate Open Map")]
        public static void Validate()
        {
            var scene = EditorSceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var errors = new List<string>();
            var warnings = new List<string>();

            T[] All<T>() where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

            if (All<StageBounds>().Length != 1)
                errors.Add("Exactly one StageBounds is required (playable volume).");
            if (All<GameBootstrap>().Length != 1)
                errors.Add("Exactly one GameBootstrap is required.");
            if (All<PlayerSpawnPoint>().Length == 0)
                errors.Add("No PlayerSpawnPoint.");

            var surfaces = All<NavMeshSurface>();
            if (surfaces.Length == 0)
                errors.Add("No NavMeshSurface.");
            foreach (var surface in surfaces.Where(s => s.useGeometry != NavMeshCollectGeometry.PhysicsColliders))
                warnings.Add($"{surface.name}: NavMeshSurface should collect Physics Colliders (runtime rebake per layout).");

            if (All<MapLayout>().Length == 0 && All<LayoutVariantGroup>().Length > 0)
                errors.Add("LayoutVariantGroups found but no MapLayout.");
            foreach (var group in All<LayoutVariantGroup>().Where(g => g.transform.childCount < (g.AllowNone ? 1 : 2)))
                warnings.Add($"{group.name}: a variant group needs at least 2 children (or allowNone).");

            var spotsByType = All<SpawnSpot>().GroupBy(s => s.Type).ToDictionary(g => g.Key, g => g.Count());
            if (!spotsByType.ContainsKey(SpawnSpotType.Teleporter))
                warnings.Add("No Teleporter SpawnSpot: the teleporter will be placed randomly.");
            if (!spotsByType.ContainsKey(SpawnSpotType.Merchant))
                warnings.Add("No Merchant SpawnSpot: the merchant will be placed randomly.");

            if (!EditorBuildSettings.scenes.Any(s => s.path == scene.path && s.enabled))
                errors.Add("The scene is not in the build settings.");
            var rotation = AssetDatabase.LoadAssetAtPath<MapRotation>(RotationPath);
            if (rotation == null || !rotation.stages.Any(stage => stage.scenes.Contains(scene.name)))
                warnings.Add($"'{scene.name}' is not used by {RotationPath}.");

            foreach (var warning in warnings)
                Debug.LogWarning($"[Map] {warning}");
            foreach (var error in errors)
                Debug.LogError($"[Map] {error}");
            Debug.Log($"[Map] {scene.name}: {errors.Count} error(s), {warnings.Count} warning(s).");
        }

        internal static void AddSpot(GameObject parent, SpawnSpotType type, Vector3 position)
        {
            var spot = new GameObject($"Spot_{type}");
            spot.transform.SetParent(parent.transform);
            spot.transform.position = position;
            var so = new SerializedObject(spot.AddComponent<SpawnSpot>());
            so.FindProperty("type").enumValueIndex = (int)type;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
