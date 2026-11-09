using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Art direction post-processing (low-poly, colorful, high fantasy): a global grade shared by every scene, a mood
    /// profile per map and one for the main menu, plus the URP asset / renderer settings they rely on.
    /// Profiles live in Assets/_Project/Settings/PostProcess and are created once (tune them freely, never overwritten).
    /// </summary>
    public static class PostProcessBuilder
    {
        public const string Folder = "Assets/_Project/Settings/PostProcess";
        public const string GlobalPath = Folder + "/PP_Global.asset";
        private const string VolumeObjectName = "PostProcess";

        // Global grade (maps write absolute values: an override replaces the global one).
        private const float Exposure = 0.15f;
        private const float Contrast = 18f;
        private const float Saturation = 22f;
        private const float Temperature = 4f;

        // ---------------------------------------------------------------- Menu items

        [MenuItem("Tools/Risk of Magic/Post Process/Apply To All Scenes")]
        public static void ApplyToAllScenes()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            string current = SceneManager.GetActiveScene().path;
            foreach (string path in ScenePaths())
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                AddVolumes(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(current) && File.Exists(current))
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
            Debug.Log("[Risk of Magic] Post-process volumes added to every scene.");
        }

        [MenuItem("Tools/Risk of Magic/Post Process/Apply Recommended URP Settings")]
        public static void ConfigurePipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset asset)
            {
                Debug.LogWarning("[Risk of Magic] No URP asset in Graphics settings: post-processing needs URP.");
                return;
            }
            var so = new SerializedObject(asset);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", 4);                   // crisp flat-shaded edges
            Set(so, "m_ColorGradingMode", 1);       // HDR grading (smoother highlights before tonemapping)
            Set(so, "m_ColorGradingLutSize", 32);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_ShadowCascadeCount", 2);
            Set(so, "m_ShadowDistance", 80f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            var renderers = so.FindProperty("m_RendererDataList");
            for (int i = 0; renderers != null && i < renderers.arraySize; i++)
                if (renderers.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData renderer)
                    ConfigureRenderer(renderer);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Risk of Magic] URP settings applied to {asset.name} (MSAA 4x, HDR grading, soft shadows, Forward+, SSAO).");
        }

        // ---------------------------------------------------------------- Build integration

        /// <summary>First run: profiles + URP settings. Later runs only create what is missing.</summary>
        public static void EnsureSetup()
        {
            if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset)
                return;
            bool firstRun = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlobalPath) == null;
            GlobalProfile();
            if (firstRun)
                ConfigurePipeline();
        }

        /// <summary>Global volume + the scene's mood volume (menu, or Map_* by scene name). Replaces a previous one.</summary>
        public static void AddVolumes(Scene scene, string sceneName = null)
        {
            if (GraphicsSettings.defaultRenderPipeline is not UniversalRenderPipelineAsset)
                return;
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == VolumeObjectName)
                    Object.DestroyImmediate(root);

            var go = new GameObject(VolumeObjectName);
            SceneManager.MoveGameObjectToScene(go, scene);
            AddVolume(go, GlobalProfile(), 0f);

            var mood = MoodProfile(string.IsNullOrEmpty(sceneName) ? scene.name : sceneName);
            if (mood != null)
            {
                var child = new GameObject("Mood");
                child.transform.SetParent(go.transform, false);
                AddVolume(child, mood, 1f);
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static void AddVolume(GameObject go, VolumeProfile profile, float priority)
        {
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = priority;
            volume.sharedProfile = profile;
        }

        private static IEnumerable<string> ScenePaths()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project/Scenes" }))
                yield return AssetDatabase.GUIDToAssetPath(guid);
        }

        // ---------------------------------------------------------------- Profiles

        private static VolumeProfile GlobalProfile() => Profile(GlobalPath, profile =>
        {
            Add<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral); // keeps saturated colors (ACES washes them)

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(Exposure);
            color.contrast.Override(Contrast);
            color.saturation.Override(Saturation);

            Add<WhiteBalance>(profile).temperature.Override(Temperature);

            var split = Add<SplitToning>(profile); // cool violet shadows, warm golden highlights
            split.shadows.Override(new Color(0.32f, 0.36f, 0.66f));
            split.highlights.Override(new Color(1f, 0.86f, 0.62f));
            split.balance.Override(-15f);

            var bloom = Add<Bloom>(profile); // HDR spells and emissives only
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.65f);
            bloom.scatter.Override(0.62f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.88f));
            bloom.highQualityFiltering.Override(true);

            var vignette = Add<Vignette>(profile);
            vignette.color.Override(new Color(0.06f, 0.03f, 0.1f));
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
        });

        private static VolumeProfile MoodProfile(string sceneName)
        {
            if (sceneName == "MainMenu")
                return Profile($"{Folder}/PP_MainMenu.asset", MenuMood);
            if (!sceneName.StartsWith("Map_"))
                return null;
            string map = sceneName.Substring(4);
            return Profile($"{Folder}/PP_{sceneName}.asset", profile => MapMood(profile, map));
        }

        private static void MenuMood(VolumeProfile profile)
        {
            Grade(profile, temperature: 12f, saturation: 6f, exposure: 0.05f);
            var dof = Add<DepthOfField>(profile); // soft background behind the menu
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(14f);
            dof.gaussianEnd.Override(38f);
            dof.gaussianMaxRadius.Override(1f);
            dof.highQualitySampling.Override(true);
            var bloom = Add<Bloom>(profile);
            bloom.intensity.Override(0.9f);
            Vignette(profile, 0.34f, new Color(0.08f, 0.03f, 0.08f));
        }

        /// <summary>Per-map mood, as offsets from the global grade. Unknown maps get an empty profile to tune.</summary>
        private static void MapMood(VolumeProfile profile, string map)
        {
            switch (map)
            {
                case "GrimForest": // damp green, teal shadows
                    Grade(profile, temperature: -8f, tint: 8f, saturation: -6f, filter: new Color(0.93f, 1f, 0.95f));
                    Split(profile, new Color(0.22f, 0.4f, 0.36f), new Color(0.95f, 1f, 0.75f));
                    break;
                case "Necropolis": // cold moonlit violet, heavy edges
                    Grade(profile, temperature: -18f, saturation: -14f, contrast: 6f, filter: new Color(0.9f, 0.92f, 1.05f));
                    Split(profile, new Color(0.38f, 0.26f, 0.58f), new Color(0.82f, 0.9f, 1f));
                    Vignette(profile, 0.34f, new Color(0.05f, 0.02f, 0.1f));
                    break;
                case "MountainPeak": // bright, crisp, glittering snow
                    Grade(profile, temperature: -12f, exposure: 0.25f, saturation: 4f);
                    var snowBloom = Add<Bloom>(profile);
                    snowBloom.threshold.Override(0.95f);
                    snowBloom.intensity.Override(0.9f);
                    break;
                case "VolcanoTomb": // ember heat, glowing lava
                    Grade(profile, temperature: 25f, saturation: 6f, contrast: 4f, filter: new Color(1.05f, 0.92f, 0.85f));
                    Split(profile, new Color(0.35f, 0.18f, 0.2f), new Color(1f, 0.7f, 0.4f));
                    Add<Bloom>(profile).intensity.Override(1.1f);
                    break;
                case "ChaosSanctum": // unstable magenta rift
                    Grade(profile, temperature: -4f, saturation: 12f, contrast: 6f, filter: new Color(1f, 0.9f, 1.06f));
                    Split(profile, new Color(0.35f, 0.15f, 0.45f), new Color(1f, 0.75f, 0.95f));
                    Add<ChromaticAberration>(profile).intensity.Override(0.12f);
                    Vignette(profile, 0.3f, new Color(0.15f, 0f, 0.12f));
                    break;
            }
        }

        /// <summary>Map grade written as absolute values (global + offset).</summary>
        private static void Grade(VolumeProfile profile, float temperature = 0f, float tint = 0f, float saturation = 0f, float contrast = 0f,
            float exposure = 0f, Color? filter = null)
        {
            var color = Add<ColorAdjustments>(profile);
            if (saturation != 0f)
                color.saturation.Override(Saturation + saturation);
            if (contrast != 0f)
                color.contrast.Override(Contrast + contrast);
            if (exposure != 0f)
                color.postExposure.Override(Exposure + exposure);
            if (filter.HasValue)
                color.colorFilter.Override(filter.Value);
            var balance = Add<WhiteBalance>(profile);
            if (temperature != 0f)
                balance.temperature.Override(Temperature + temperature);
            if (tint != 0f)
                balance.tint.Override(tint);
        }

        private static void Split(VolumeProfile profile, Color shadows, Color highlights)
        {
            var split = Add<SplitToning>(profile);
            split.shadows.Override(shadows);
            split.highlights.Override(highlights);
        }

        private static void Vignette(VolumeProfile profile, float intensity, Color color)
        {
            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(intensity);
            vignette.color.Override(color);
        }

        /// <summary>Loads the profile, or creates it once with <paramref name="fill"/>.</summary>
        private static VolumeProfile Profile(string path, Action<VolumeProfile> fill)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile != null)
                return profile;
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            fill(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>Component stored as a sub-asset (a bare profile.Add would not persist).</summary>
        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
                return existing;
            var component = profile.Add<T>();
            component.name = typeof(T).Name;
            component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
            return component;
        }

        // ---------------------------------------------------------------- Renderer

        private static void ConfigureRenderer(UniversalRendererData renderer)
        {
            var so = new SerializedObject(renderer);
            Set(so, "m_RenderingMode", 2); // Forward+: many point lights (spells, fires, runes)
            so.ApplyModifiedPropertiesWithoutUndo();

            // SSAO grounds low-poly shapes. The feature class is internal: found by name.
            var ssaoType = Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (ssaoType != null && !renderer.rendererFeatures.Exists(f => f != null && f.GetType() == ssaoType))
            {
                var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(ssaoType);
                feature.name = "SSAO";
                var featureSo = new SerializedObject(feature);
                Set(featureSo, "m_Settings.Intensity", 0.7f);
                Set(featureSo, "m_Settings.Radius", 0.35f);
                Set(featureSo, "m_Settings.DirectLightingStrength", 0.25f);
                Set(featureSo, "m_Settings.Falloff", 60f);
                featureSo.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
                {
                    var map = new SerializedObject(renderer);
                    var ids = map.FindProperty("m_RendererFeatureMap");
                    if (ids != null)
                    {
                        ids.InsertArrayElementAtIndex(ids.arraySize);
                        ids.GetArrayElementAtIndex(ids.arraySize - 1).longValue = localId;
                        map.ApplyModifiedPropertiesWithoutUndo();
                    }
                }
            }
            EditorUtility.SetDirty(renderer);
        }

        private static void Set(SerializedObject so, string path, object value)
        {
            var property = so.FindProperty(path);
            if (property == null)
            {
                Debug.LogWarning($"[Risk of Magic] {so.targetObject.name}: property {path} not found (URP version?).");
                return;
            }
            switch (value)
            {
                case bool b: property.boolValue = b; break;
                case int i: property.intValue = i; break; // enum value (not index) for enum fields
                case float f: property.floatValue = f; break;
            }
        }
    }
}
