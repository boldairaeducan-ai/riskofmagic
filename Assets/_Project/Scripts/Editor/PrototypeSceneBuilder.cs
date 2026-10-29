using System.IO;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Networking;
using RiskOfMagic.Player;
using Unity.AI.Navigation;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// One-click prototype: configs, player/enemy prefabs, test level with baked NavMesh and NetworkManager.
    /// Re-running overwrites the generated assets.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string ConfigFolder = Root + "/Configs";
        private const string PrefabFolder = Root + "/Prefabs";
        private const string MaterialFolder = Root + "/Materials";
        private const string SceneFolder = Root + "/Scenes";
        private const string ScenePath = SceneFolder + "/Prototype.unity";

        [MenuItem("Tools/Risk of Magic/Build Prototype Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (var folder in new[] { ConfigFolder, PrefabFolder, MaterialFolder, SceneFolder })
                Directory.CreateDirectory(folder);

            var movement = LoadOrCreateAsset<MovementConfig>(ConfigFolder + "/MovementConfig_Default.asset");
            var ghoulDef = LoadOrCreateAsset<EnemyDefinition>(ConfigFolder + "/Enemy_Ghoul.asset");
            ghoulDef.displayName = "Ghoul";
            EditorUtility.SetDirty(ghoulDef);

            var playerMat = CreateMaterial("M_Player", new Color(0.25f, 0.5f, 0.9f));
            var enemyMat = CreateMaterial("M_Enemy", new Color(0.75f, 0.2f, 0.2f));
            var groundMat = CreateMaterial("M_Ground", new Color(0.35f, 0.4f, 0.3f));

            var playerPrefab = BuildPlayerPrefab(movement, playerMat);
            var enemyPrefab = BuildEnemyPrefab(ghoulDef, enemyMat);

            string prefabListPath = ConfigFolder + "/NetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(prefabListPath);
            var prefabList = LoadOrCreateAsset<NetworkPrefabsList>(prefabListPath);
            prefabList.Add(new NetworkPrefab { Prefab = enemyPrefab });
            EditorUtility.SetDirty(prefabList);

            BuildScene(playerPrefab, enemyPrefab, prefabList, groundMat);
            AssetDatabase.SaveAssets();
            Debug.Log("[Risk of Magic] Prototype scene built. Press Play, then Host.");
        }

        private static GameObject BuildPlayerPrefab(MovementConfig movement, Material material)
        {
            var root = new GameObject("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<OwnerNetworkTransform>();
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;

            var health = root.AddComponent<Health>();
            SetProperties(health, so =>
            {
                so.FindProperty("baseMaxHealth").floatValue = 100f;
                so.FindProperty("team").enumValueIndex = (int)Team.Player;
            });

            root.AddComponent<PlayerInputHandler>();
            var motor = root.AddComponent<PlayerMotor>();
            SetProperties(motor, so => so.FindProperty("config").objectReferenceValue = movement);

            var body = CreateVisual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), material);
            body.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;

            var pivot = new GameObject("CameraPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(0f, 1.6f, 0f);

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(pivot, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.fieldOfView = 80f;
            cam.depth = 10f;
            cam.enabled = false;
            camGo.AddComponent<AudioListener>().enabled = false;

            var look = root.AddComponent<PlayerLook>();
            SetProperties(look, so =>
            {
                so.FindProperty("cameraPivot").objectReferenceValue = pivot;
                so.FindProperty("playerCamera").objectReferenceValue = cam;
            });

            root.AddComponent<PlayerAvatar>();
            root.AddComponent<DebugHitscanWeapon>();

            return SavePrefab(root, PrefabFolder + "/Player.prefab");
        }

        private static GameObject BuildEnemyPrefab(EnemyDefinition definition, Material material)
        {
            var root = new GameObject("Enemy_Ghoul");
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.8f, 0f);
            capsule.height = 1.6f;
            capsule.radius = 0.45f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f;
            agent.height = 2.1f;
            agent.enabled = false;

            root.AddComponent<NetworkObject>();
            var netTransform = root.AddComponent<NetworkTransform>();
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;

            var health = root.AddComponent<Health>();
            SetProperties(health, so => so.FindProperty("team").enumValueIndex = (int)Team.Enemy);

            var actor = root.AddComponent<EnemyActor>();
            SetProperties(actor, so => so.FindProperty("definition").objectReferenceValue = definition);

            var attack = root.AddComponent<EnemyMeleeAttack>();
            var brain = root.AddComponent<EnemyBrain>();
            SetProperties(brain, so => so.FindProperty("attack").objectReferenceValue = attack);

            CreateVisual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.8f, 0.9f), material);

            // Head keeps its collider and becomes a weak point.
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            head.transform.localScale = Vector3.one * 0.6f;
            head.GetComponent<Renderer>().sharedMaterial = material;
            var hitbox = head.AddComponent<Hitbox>();
            SetProperties(hitbox, so =>
            {
                so.FindProperty("owner").objectReferenceValue = health;
                so.FindProperty("damageMultiplier").floatValue = 1.5f;
                so.FindProperty("isWeakPoint").boolValue = true;
            });

            return SavePrefab(root, PrefabFolder + "/Enemy_Ghoul.prefab");
        }

        private static void BuildScene(GameObject playerPrefab, GameObject enemyPrefab, NetworkPrefabsList prefabList, Material groundMat)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                Object.DestroyImmediate(mainCam.GetComponent<AudioListener>());
                mainCam.transform.SetPositionAndRotation(new Vector3(0f, 25f, -30f), Quaternion.Euler(40f, 0f, 0f));
            }

            // Level
            var level = new GameObject("Level");
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(level.transform);
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var random = new System.Random(42);
            for (int i = 0; i < 12; i++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Rock_{i}";
                block.transform.SetParent(level.transform);
                float x = (float)(random.NextDouble() * 70 - 35);
                float z = (float)(random.NextDouble() * 70 - 35);
                if (Mathf.Abs(x) < 6f && Mathf.Abs(z) < 6f)
                    x += 12f;
                var size = new Vector3(2f + (float)random.NextDouble() * 4f, 1f + (float)random.NextDouble() * 3f, 2f + (float)random.NextDouble() * 4f);
                block.transform.localScale = size;
                block.transform.position = new Vector3(x, size.y * 0.5f, z);
                block.GetComponent<Renderer>().sharedMaterial = groundMat;
            }
            foreach (var t in level.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string navPath = SceneFolder + "/Prototype_NavMesh.asset";
                AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            }

            // Spawn points
            var spawns = new GameObject("PlayerSpawns");
            for (int i = 0; i < 4; i++)
            {
                var point = new GameObject($"Spawn_{i}");
                point.transform.SetParent(spawns.transform);
                point.transform.position = new Vector3((i - 1.5f) * 2.5f, 0.1f, -3f);
                point.AddComponent<PlayerSpawnPoint>();
            }

            // In-scene enemies (spawned automatically by the server)
            for (int i = 0; i < 4; i++)
            {
                var enemy = (GameObject)PrefabUtility.InstantiatePrefab(enemyPrefab);
                float angle = i * Mathf.PI * 0.5f;
                enemy.transform.position = new Vector3(Mathf.Cos(angle) * 20f, 0f, Mathf.Sin(angle) * 20f + 10f);
            }

            // Network
            var netGo = new GameObject("NetworkManager");
            var manager = netGo.AddComponent<NetworkManager>();
            var transport = netGo.AddComponent<UnityTransport>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            if (!manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(prefabList))
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            netGo.AddComponent<NetworkDebugPanel>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        private static GameObject CreateVisual(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            var shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : Shader.Find("Standard");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void SetProperties(Object target, System.Action<SerializedObject> edit)
        {
            var so = new SerializedObject(target);
            edit(so);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
