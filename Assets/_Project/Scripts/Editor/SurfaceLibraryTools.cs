using System.IO;
using RiskOfMagic.Audio;
using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>Creates Resources/SurfaceLibrary.asset and adds every TerrainLayer of the project, guessing its surface from its name.</summary>
    public static class SurfaceLibraryTools
    {
        private const string Path = "Assets/_Project/Resources/SurfaceLibrary.asset";

        [MenuItem("Tools/Risk of Magic/Audio/Update Surface Library")]
        public static void UpdateLibrary()
        {
            var library = AssetDatabase.LoadAssetAtPath<SurfaceLibrary>(Path);
            if (library == null)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                library = ScriptableObject.CreateInstance<SurfaceLibrary>();
                AssetDatabase.CreateAsset(library, Path);
            }
            int added = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:TerrainLayer"))
            {
                var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(AssetDatabase.GUIDToAssetPath(guid));
                if (layer == null || library.terrainLayers.Exists(e => e.layer == layer))
                    continue;
                library.terrainLayers.Add(new SurfaceLibrary.TerrainLayerEntry { layer = layer, surface = Guess(layer.name) });
                added++;
            }
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            Selection.activeObject = library;
            EditorGUIUtility.PingObject(library);
            Debug.Log($"[Audio] Surface library: {added} terrain layer(s) added. Check the guessed surfaces, add rock / bridge materials if needed.");
        }

        private static SurfaceType Guess(string name)
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("sand") || n.Contains("sable") || n.Contains("desert") || n.Contains("beach"))
                return SurfaceType.Sand;
            if (n.Contains("rock") || n.Contains("stone") || n.Contains("roche") || n.Contains("pierre") || n.Contains("cliff")
                || n.Contains("cobble") || n.Contains("ash") || n.Contains("basalt") || n.Contains("lava"))
                return SurfaceType.Rock;
            if (n.Contains("water") || n.Contains("eau") || n.Contains("mud") || n.Contains("swamp") || n.Contains("boue"))
                return SurfaceType.Water;
            return SurfaceType.Dirt; // grass, ground, soil, snow...
        }
    }
}
