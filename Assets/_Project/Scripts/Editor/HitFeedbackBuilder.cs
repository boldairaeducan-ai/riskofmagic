using System.IO;
using RiskOfMagic.Feedback;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RiskOfMagic.Editor
{
    /// <summary>Creates Resources/HitFeedback.asset once (tuning, never overwritten) and its additive spark material.</summary>
    public static class HitFeedbackBuilder
    {
        private const string ConfigPath = "Assets/_Project/Resources/HitFeedback.asset";
        private const string MaterialPath = "Assets/_Project/Materials/M_HitSpark.mat";

        public static void Ensure()
        {
            var config = AssetDatabase.LoadAssetAtPath<HitFeedbackConfig>(ConfigPath);
            if (config == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
                config = ScriptableObject.CreateInstance<HitFeedbackConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            if (config.sparkMaterial == null)
            {
                config.sparkMaterial = SparkMaterial(config.sparkGlow);
                EditorUtility.SetDirty(config);
            }
        }

        private static Material SparkMaterial(float glow)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
                return material;
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = Shader.Find(urp ? "Universal Render Pipeline/Particles/Unlit" : "Legacy Shaders/Particles/Additive");
            material = new Material(shader) { name = "M_HitSpark" };
            var texture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            var color = new Color(glow, glow, glow, 1f); // HDR: above the bloom threshold
            if (urp)
            {
                material.SetFloat("_Surface", 1f);   // transparent
                material.SetFloat("_Blend", 2f);     // additive
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.One);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)RenderQueue.Transparent;
                if (texture != null)
                    material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", color);
            }
            else
            {
                if (texture != null)
                    material.mainTexture = texture;
                material.SetColor("_TintColor", color * 0.5f);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
