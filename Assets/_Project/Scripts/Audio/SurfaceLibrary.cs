using System;
using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// Which surface each terrain layer / texture / material sounds like (Resources/SurfaceLibrary.asset, created and
    /// completed by Tools → Risk of Magic → Audio → Update Surface Library: one entry per TerrainLayer found, guessed
    /// from its name; never overwrites an existing entry).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Audio/Surface Library", fileName = "SurfaceLibrary")]
    public class SurfaceLibrary : ScriptableObject
    {
        public const string ResourcePath = "SurfaceLibrary";

        [Serializable]
        public class TerrainLayerEntry
        {
            public TerrainLayer layer;
            public SurfaceType surface = SurfaceType.Dirt;
        }

        [Serializable]
        public class TextureEntry
        {
            [Tooltip("Matches a terrain layer's diffuse texture or a material's main texture.")] public Texture texture;
            public SurfaceType surface = SurfaceType.Rock;
        }

        [Serializable]
        public class MaterialEntry
        {
            public Material material;
            public SurfaceType surface = SurfaceType.Rock;
        }

        [Tooltip("Terrain layers (terrain painting).")] public List<TerrainLayerEntry> terrainLayers = new();
        [Tooltip("Textures (terrain layers or meshes).")] public List<TextureEntry> textures = new();
        [Tooltip("Mesh materials (rocks, bridges, floors).")] public List<MaterialEntry> materials = new();
        [Tooltip("Meshes without a match.")] public SurfaceType meshDefault = SurfaceType.Rock;
        [Tooltip("Terrains without a match.")] public SurfaceType terrainDefault = SurfaceType.Dirt;

        private static SurfaceLibrary instance;
        private static bool loaded;

        public static SurfaceLibrary Instance
        {
            get
            {
                if (!loaded)
                {
                    instance = Resources.Load<SurfaceLibrary>(ResourcePath);
                    loaded = true;
                }
                return instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            instance = null;
            loaded = false;
        }

        public bool TryGet(TerrainLayer layer, out SurfaceType surface)
        {
            surface = SurfaceType.Default;
            if (layer == null)
                return false;
            foreach (var entry in terrainLayers)
                if (entry.layer == layer)
                {
                    surface = entry.surface;
                    return true;
                }
            return TryGet(layer.diffuseTexture, out surface);
        }

        public bool TryGet(Texture texture, out SurfaceType surface)
        {
            surface = SurfaceType.Default;
            if (texture == null)
                return false;
            foreach (var entry in textures)
                if (entry.texture == texture)
                {
                    surface = entry.surface;
                    return true;
                }
            return false;
        }

        public bool TryGet(Material material, out SurfaceType surface)
        {
            surface = SurfaceType.Default;
            if (material == null)
                return false;
            foreach (var entry in materials)
                if (entry.material == material)
                {
                    surface = entry.surface;
                    return true;
                }
            return TryGet(material.mainTexture, out surface);
        }
    }
}
