using System.Collections.Generic;
using System.IO;
using System.Reflection;
using RiskOfMagic.Audio;
using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Creates the audio entry points: Resources/AudioConfig and one empty SoundEvent asset per sound field found on
    /// configs (skills, projectiles, enemies, characters, items, equipment) and prefabs (EffectSound, interactables).
    /// Slots live at deterministic paths (Audio/Sounds/&lt;folder&gt;/&lt;owner&gt;_&lt;field&gt;.asset): they are never
    /// overwritten and are re-linked after each prototype rebuild. Plug clips into them; empty slots stay silent.
    /// </summary>
    public static class AudioSlots
    {
        private const string Root = "Assets/_Project";
        public const string ConfigPath = Root + "/Resources/AudioConfig.asset";
        public const string SoundsFolder = Root + "/Audio/Sounds";
        private static readonly string[] ScannedFolders = { Root + "/Configs", Root + "/Prefabs" };

        [MenuItem("Tools/Risk of Magic/Create Audio Slots")]
        public static void CreateAll()
        {
            int created = 0;
            var config = LoadOrCreateConfig();
            created += Fill(config, "Global", "");

            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", ScannedFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadMainAssetAtPath(path) as ScriptableObject;
                if (asset != null && asset is not SoundEvent)
                    created += Fill(asset, FolderName(path), asset.name + "_");
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", ScannedFolders))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = PrefabUtility.LoadPrefabContents(path);
                int count = 0;
                foreach (var component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (component == null)
                        continue;
                    string owner = component.gameObject == prefab ? prefab.name : $"{prefab.name}.{component.gameObject.name}";
                    count += Fill(component, FolderName(path), owner + "_");
                }
                if (count > 0)
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                PrefabUtility.UnloadPrefabContents(prefab);
                created += count;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Risk of Magic] Audio slots ready ({created} linked). Plug AudioClips into {SoundsFolder} and {ConfigPath}.");
        }

        private static AudioConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<AudioConfig>(ConfigPath);
            if (config != null)
                return config;
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
            config = ScriptableObject.CreateInstance<AudioConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            return config;
        }

        /// <summary>Links every null SoundEvent field of target to its slot asset. Returns the number of links made.</summary>
        private static int Fill(Object target, string folder, string prefix)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.GetIterator();
            int linked = 0;
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference || property.type != "PPtr<$SoundEvent>"
                    || property.objectReferenceValue != null || property.depth > 0)
                    continue;
                var defaults = FindField(target.GetType(), property.name)?.GetCustomAttribute<SoundSlotAttribute>();
                if (defaults != null && defaults.Optional)
                    continue;
                property.objectReferenceValue = LoadOrCreateSlot($"{SoundsFolder}/{folder}/{prefix}{property.name}.asset", defaults);
                linked++;
            }
            if (linked > 0)
            {
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(target);
            }
            return linked;
        }

        private static SoundEvent LoadOrCreateSlot(string path, SoundSlotAttribute defaults)
        {
            var existing = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
            if (existing != null)
                return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sound = ScriptableObject.CreateInstance<SoundEvent>();
            if (defaults != null)
            {
                sound.spatial = defaults.Spatial;
                sound.category = defaults.Category;
                sound.cooldown = defaults.Cooldown;
                sound.maxSimultaneous = defaults.MaxSimultaneous;
                sound.maxDistance = defaults.MaxDistance;
            }
            AssetDatabase.CreateAsset(sound, path);
            return sound;
        }

        private static FieldInfo FindField(System.Type type, string name)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (; type != null; type = type.BaseType)
            {
                var field = type.GetField(name, flags);
                if (field != null)
                    return field;
            }
            return null;
        }

        private static string FolderName(string assetPath) => Path.GetFileName(Path.GetDirectoryName(assetPath));
    }
}
