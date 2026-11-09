using System.IO;
using RiskOfMagic.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Creates the main menu set (Prefabs/Menu/MenuBackdrop.prefab) once, with placeholder ruins around a campfire.
    /// Never overwritten: dress it freely (delete the prefab to get the placeholder back).
    /// </summary>
    public static class MenuBackdropBuilder
    {
        public const string Folder = "Assets/_Project/Prefabs/Menu";
        public const string PrefabPath = Folder + "/MenuBackdrop.prefab";

        public static GameObject GetOrCreate()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
                return existing;
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            var root = new GameObject("MenuBackdrop");
            var ground = Mat("M_Menu_Ground", new Color(0.16f, 0.15f, 0.14f));
            var stone = Mat("M_Menu_Stone", new Color(0.42f, 0.4f, 0.38f));
            var darkStone = Mat("M_Menu_DarkStone", new Color(0.25f, 0.24f, 0.26f));
            var wood = Mat("M_Menu_Wood", new Color(0.3f, 0.2f, 0.12f));
            var ember = Mat("M_Menu_Ember", new Color(1f, 0.45f, 0.1f), emissive: true);
            var rune = Mat("M_Menu_Rune", new Color(0.55f, 0.4f, 1f), emissive: true);

            var set = new GameObject("Set").transform;
            set.SetParent(root.transform, false);

            Part(PrimitiveType.Plane, "Ground", set, new Vector3(0f, 0f, 10f), Vector3.zero, new Vector3(8f, 1f, 8f), ground);

            // Ring of ruined pillars (varying heights), a broken arch at the back, scattered rocks.
            for (int i = 0; i < 9; i++)
            {
                float a = Mathf.Lerp(-150f, 150f, i / 8f) * Mathf.Deg2Rad;
                var position = new Vector3(Mathf.Sin(a) * 9f, 0f, 12f + Mathf.Cos(a) * 7f);
                float height = 2f + Mathf.PerlinNoise(i * 0.7f, 0.3f) * 5f;
                Part(PrimitiveType.Cylinder, $"Pillar{i}", set, position + Vector3.up * height * 0.5f, new Vector3(0f, i * 23f, (i % 3 - 1) * 4f),
                    new Vector3(0.9f, height * 0.5f, 0.9f), stone);
                Part(PrimitiveType.Cube, $"PillarBase{i}", set, position + Vector3.up * 0.2f, new Vector3(0f, i * 31f, 0f), new Vector3(1.4f, 0.4f, 1.4f), darkStone);
            }
            Part(PrimitiveType.Cube, "ArchLeft", set, new Vector3(-2.6f, 3f, 20f), Vector3.zero, new Vector3(1.2f, 6f, 1.2f), stone);
            Part(PrimitiveType.Cube, "ArchRight", set, new Vector3(2.6f, 2f, 20f), new Vector3(0f, 0f, -6f), new Vector3(1.2f, 4f, 1.2f), stone);
            Part(PrimitiveType.Cube, "ArchTop", set, new Vector3(-0.6f, 6.3f, 20f), new Vector3(0f, 0f, -8f), new Vector3(5f, 0.9f, 1.3f), stone);
            Part(PrimitiveType.Cube, "FallenBlock", set, new Vector3(3.5f, 0.4f, 17.5f), new Vector3(0f, 35f, 12f), new Vector3(2.4f, 0.8f, 1.2f), darkStone);

            // Rune obelisk with a faint glow.
            Part(PrimitiveType.Cube, "Obelisk", set, new Vector3(5.5f, 3.5f, 16f), new Vector3(0f, 20f, 0f), new Vector3(1f, 7f, 1f), darkStone);
            Part(PrimitiveType.Cube, "ObeliskRune", set, new Vector3(5.5f, 4.5f, 15.48f), new Vector3(0f, 20f, 0f), new Vector3(0.3f, 2.4f, 0.05f), rune);
            Light("RuneLight", set, new Vector3(5f, 4f, 14.5f), LightType.Point, new Color(0.6f, 0.45f, 1f), 2f, 7f);

            for (int i = 0; i < 12; i++)
            {
                var position = new Vector3(Random.Range(-12f, 12f), 0.2f, Random.Range(4f, 24f));
                if (Mathf.Abs(position.x) < 3f && position.z < 14f)
                    position.x += 5f * Mathf.Sign(position.x + 0.01f);
                float size = Random.Range(0.4f, 1.3f);
                Part(PrimitiveType.Sphere, $"Rock{i}", set, position, new Vector3(Random.value * 360f, Random.value * 360f, 0f),
                    new Vector3(size * 1.4f, size * 0.7f, size), darkStone);
            }

            // Campfire: logs, embers, flickering light.
            var fire = new GameObject("Campfire").transform;
            fire.SetParent(set, false);
            fire.localPosition = new Vector3(0f, 0f, 11f);
            for (int i = 0; i < 4; i++)
                Part(PrimitiveType.Cylinder, $"Log{i}", fire, new Vector3(0f, 0.15f, 0f), new Vector3(90f, i * 45f, 0f), new Vector3(0.18f, 0.6f, 0.18f), wood);
            Part(PrimitiveType.Sphere, "Embers", fire, new Vector3(0f, 0.25f, 0f), Vector3.zero, new Vector3(0.6f, 0.3f, 0.6f), ember);
            var fireLight = Light("FireLight", fire, new Vector3(0f, 1f, 0f), LightType.Point, new Color(1f, 0.55f, 0.2f), 4f, 12f);
            fireLight.gameObject.AddComponent<FlickerLight>();

            // Moonlight.
            var moon = Light("Moonlight", root.transform, new Vector3(0f, 10f, 0f), LightType.Directional, new Color(0.55f, 0.62f, 0.9f), 0.45f, 0f);
            moon.transform.rotation = Quaternion.Euler(35f, -140f, 0f);
            moon.shadows = LightShadows.Soft;

            // Camera: low, facing the fire, set off to the right so the menu (left) doesn't hide the scene.
            var focus = new GameObject("CameraFocus").transform;
            focus.SetParent(root.transform, false);
            focus.localPosition = new Vector3(-2.5f, 1.6f, 13f);
            var camGo = new GameObject("BackdropCamera");
            camGo.transform.SetParent(root.transform, false);
            camGo.transform.localPosition = new Vector3(-5.5f, 2.2f, 2f);
            camGo.transform.LookAt(focus);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            var backdrop = root.AddComponent<MenuBackdrop>();
            var so = new SerializedObject(backdrop);
            so.FindProperty("backdropCamera").objectReferenceValue = cam;
            so.FindProperty("focus").objectReferenceValue = focus;
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log($"[Risk of Magic] Menu backdrop created at {PrefabPath} (never overwritten: dress it as you like).");
            return prefab;
        }

        private static void Part(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 euler, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static Light Light(string name, Transform parent, Vector3 position, LightType type, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            if (range > 0f)
                light.range = range;
            return light;
        }

        /// <summary>Created once in the menu folder (never recolored afterwards).</summary>
        private static Material Mat(string name, Color color, bool emissive = false)
        {
            string path = $"{Folder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = emissive
                ? Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color")
                : Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
