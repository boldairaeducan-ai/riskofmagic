using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Level blockout kit: prefabs at gameplay metrics (jump 1.6 m, player 1.8 m, 30° max slopes) with a 1 m checker
    /// in world-size UVs, and a palette window that drops them on the grid under the map's "Level" object.
    /// </summary>
    public static class BlockoutKit
    {
        public const string Folder = "Assets/_Project/Blockout";
        private const string MeshFolder = Folder + "/Meshes";
        private const string MaterialFolder = Folder + "/Materials";
        public const string PrefabFolder = Folder + "/Prefabs";

        public enum Kind { Floor, Wall, Cover, Ramp, Platform, Prop }

        public sealed class Piece
        {
            public string Name;
            public Kind Kind;
            public string Description;
            public Action<MeshBuilder> Build;
        }

        /// <summary>The catalogue. Origins: floors = top center (y 0 = walkable), everything else = bottom center.</summary>
        public static readonly Piece[] Pieces =
        {
            new() { Name = "Floor_4x4", Kind = Kind.Floor, Description = "Sol 4 × 4 m (dessus à 0)", Build = b => b.Box(new Vector3(-2, -0.5f, -2), new Vector3(2, 0, 2)) },
            new() { Name = "Floor_8x8", Kind = Kind.Floor, Description = "Sol 8 × 8 m", Build = b => b.Box(new Vector3(-4, -0.5f, -4), new Vector3(4, 0, 4)) },
            new() { Name = "Floor_16x16", Kind = Kind.Floor, Description = "Sol 16 × 16 m", Build = b => b.Box(new Vector3(-8, -0.5f, -8), new Vector3(8, 0, 8)) },
            new() { Name = "Floor_32x32", Kind = Kind.Floor, Description = "Sol 32 × 32 m (grande zone)", Build = b => b.Box(new Vector3(-16, -1, -16), new Vector3(16, 0, 16)) },
            new() { Name = "Wall_4", Kind = Kind.Wall, Description = "Mur 4 m de long, 4 m de haut", Build = b => b.Box(new Vector3(-2, 0, -0.25f), new Vector3(2, 4, 0.25f)) },
            new() { Name = "Wall_8", Kind = Kind.Wall, Description = "Mur 8 m de long, 4 m de haut", Build = b => b.Box(new Vector3(-4, 0, -0.25f), new Vector3(4, 4, 0.25f)) },
            new() { Name = "Wall_8_High", Kind = Kind.Wall, Description = "Mur 8 × 8 m (limite, falaise)", Build = b => b.Box(new Vector3(-4, 0, -0.5f), new Vector3(4, 8, 0.5f)) },
            new() { Name = "Doorway_4", Kind = Kind.Wall, Description = "Mur 4 m avec passage 2,5 × 3 m", Build = b =>
            {
                b.Box(new Vector3(-2, 0, -0.25f), new Vector3(-1.25f, 4, 0.25f));
                b.Box(new Vector3(1.25f, 0, -0.25f), new Vector3(2, 4, 0.25f));
                b.Box(new Vector3(-1.25f, 3, -0.25f), new Vector3(1.25f, 4, 0.25f));
            } },
            new() { Name = "Cover_Low", Kind = Kind.Cover, Description = "Muret 2 m, 1,2 m de haut (couverture, sautable)", Build = b => b.Box(new Vector3(-1, 0, -0.3f), new Vector3(1, 1.2f, 0.3f)) },
            new() { Name = "Cover_Long", Kind = Kind.Cover, Description = "Muret 4 m, 1,2 m de haut", Build = b => b.Box(new Vector3(-2, 0, -0.3f), new Vector3(2, 1.2f, 0.3f)) },
            new() { Name = "Block_1_5", Kind = Kind.Cover, Description = "Bloc 2 × 1,5 × 2 m (hauteur max d'un saut)", Build = b => b.Box(new Vector3(-1, 0, -1), new Vector3(1, 1.5f, 1)) },
            new() { Name = "Pillar", Kind = Kind.Cover, Description = "Pilier 1 × 6 × 1 m", Build = b => b.Box(new Vector3(-0.5f, 0, -0.5f), new Vector3(0.5f, 6, 0.5f)) },
            new() { Name = "Ramp_2m", Kind = Kind.Ramp, Description = "Rampe 4 m de large, monte de 2 m sur 4 m (27°)", Build = b => b.Wedge(4, 2, 4) },
            new() { Name = "Ramp_4m", Kind = Kind.Ramp, Description = "Rampe 4 m de large, monte de 4 m sur 8 m (27°)", Build = b => b.Wedge(4, 4, 8) },
            new() { Name = "Ramp_8m", Kind = Kind.Ramp, Description = "Rampe 6 m de large, monte de 8 m sur 16 m (27°)", Build = b => b.Wedge(6, 8, 16) },
            new() { Name = "Stairs_2m", Kind = Kind.Ramp, Description = "Escalier 4 m de large, monte de 2 m (marches de 25 cm)", Build = b =>
            {
                for (int i = 0; i < 8; i++)
                    b.Box(new Vector3(-2, 0, i * 0.4f), new Vector3(2, (i + 1) * 0.25f, (i + 1) * 0.4f));
            } },
            new() { Name = "Platform_4x4_H2", Kind = Kind.Platform, Description = "Plateforme 4 × 4 m à 2 m (double saut / rampe)", Build = b => b.Box(new Vector3(-2, 0, -2), new Vector3(2, 2, 2)) },
            new() { Name = "Platform_8x8_H4", Kind = Kind.Platform, Description = "Plateforme 8 × 8 m à 4 m", Build = b => b.Box(new Vector3(-4, 0, -4), new Vector3(4, 4, 4)) },
            new() { Name = "Platform_8x8_H8", Kind = Kind.Platform, Description = "Plateforme 8 × 8 m à 8 m (hauteur)", Build = b => b.Box(new Vector3(-4, 0, -4), new Vector3(4, 8, 4)) },
            new() { Name = "Bridge_4x16", Kind = Kind.Platform, Description = "Pont 4 × 16 m, 0,5 m d'épaisseur (dessus à 0)", Build = b => b.Box(new Vector3(-2, -0.5f, -8), new Vector3(2, 0, 8)) },
            new() { Name = "Rock_Small", Kind = Kind.Prop, Description = "Rocher 1,5 m (obstacle)", Build = b => b.Box(new Vector3(-0.8f, 0, -0.7f), new Vector3(0.8f, 1.5f, 0.7f)) },
            new() { Name = "Rock_Large", Kind = Kind.Prop, Description = "Rocher 4 m (bloque la vue)", Build = b => b.Box(new Vector3(-2, 0, -1.8f), new Vector3(2, 4, 1.8f)) },
        };

        private static readonly Dictionary<Kind, Color> Colors = new()
        {
            { Kind.Floor, new Color(0.55f, 0.55f, 0.58f) },
            { Kind.Wall, new Color(0.78f, 0.78f, 0.8f) },
            { Kind.Cover, new Color(0.95f, 0.6f, 0.25f) },
            { Kind.Ramp, new Color(0.35f, 0.6f, 0.95f) },
            { Kind.Platform, new Color(0.4f, 0.8f, 0.45f) },
            { Kind.Prop, new Color(0.6f, 0.5f, 0.42f) },
        };

        [MenuItem("Tools/Risk of Magic/Blockout/Rebuild Kit")]
        public static void Rebuild()
        {
            foreach (string folder in new[] { MeshFolder, MaterialFolder, PrefabFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();
            var checker = CheckerTexture();
            var materials = new Dictionary<Kind, Material>();
            foreach (var pair in Colors)
                materials[pair.Key] = Material(pair.Key.ToString(), pair.Value, checker);

            foreach (var piece in Pieces)
            {
                var builder = new MeshBuilder();
                piece.Build(builder);
                var mesh = SaveMesh(builder.ToMesh(piece.Name), $"{MeshFolder}/{piece.Name}.asset");
                var go = new GameObject(piece.Name);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = materials[piece.Kind];
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabFolder}/{piece.Name}.prefab");
                Object.DestroyImmediate(go);
            }
            ScalePlayer(materials[Kind.Cover]);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Blockout] Kit ready in {PrefabFolder} ({Pieces.Length} pieces). Window: Tools → Risk of Magic → Blockout → Palette.");
        }

        public static GameObject Load(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
            if (prefab == null)
            {
                Rebuild();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{name}.prefab");
            }
            return prefab;
        }

        /// <summary>1.8 m silhouette to judge the scale (no collider, editor only).</summary>
        private static void ScalePlayer(Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Scale_Player";
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
            var root = new GameObject("Scale_Player");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            go.tag = "EditorOnly";
            root.tag = "EditorOnly";
            PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabFolder}/Scale_Player.prefab");
            Object.DestroyImmediate(root);
        }

        private static Texture2D CheckerTexture()
        {
            string path = $"{MaterialFolder}/T_BlockoutChecker.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null)
                return existing;
            const int size = 128; // 2 × 2 squares = 2 m (UVs are in meters / 2)
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool dark = (x < size / 2) ^ (y < size / 2);
                    bool line = x % (size / 2) == 0 || y % (size / 2) == 0;
                    float v = line ? 0.62f : dark ? 0.82f : 1f;
                    texture.SetPixel(x, y, new Color(v, v, v, 1f));
                }
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), path), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material Material(string name, Color color, Texture2D checker)
        {
            string path = $"{MaterialFolder}/M_Blockout_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            if (material == null)
            {
                material = new Material(Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.mainTexture = checker;
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", checker);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness"))
                material.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetUVs(0, mesh.uv);
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }

        /// <summary>Boxes and wedges with flat normals and UVs in meters (1 checker square = 1 m whatever the size).</summary>
        public sealed class MeshBuilder
        {
            private readonly List<Vector3> vertices = new();
            private readonly List<Vector3> normals = new();
            private readonly List<Vector2> uvs = new();
            private readonly List<int> triangles = new();

            /// <summary>Quad a-b-c-d counter-clockwise seen from the outside.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normal = Vector3.Cross(d - a, b - a).normalized; // Unity is left-handed
                Vector3 u = (b - a).normalized, v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                foreach (var p in new[] { a, b, c, d })
                {
                    vertices.Add(p);
                    normals.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) * 0.5f);
                }
                triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(c - a, b - a).normalized;
                Vector3 u = (b - a).normalized, v = Vector3.Cross(normal, u);
                int start = vertices.Count;
                foreach (var p in new[] { a, b, c })
                {
                    vertices.Add(p);
                    normals.Add(normal);
                    uvs.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, v)) * 0.5f);
                }
                triangles.AddRange(new[] { start, start + 2, start + 1 });
            }

            public void Box(Vector3 min, Vector3 max)
            {
                Vector3 p000 = new(min.x, min.y, min.z), p100 = new(max.x, min.y, min.z), p010 = new(min.x, max.y, min.z), p110 = new(max.x, max.y, min.z);
                Vector3 p001 = new(min.x, min.y, max.z), p101 = new(max.x, min.y, max.z), p011 = new(min.x, max.y, max.z), p111 = new(max.x, max.y, max.z);
                Quad(p010, p110, p111, p011); // top
                Quad(p001, p101, p100, p000); // bottom
                Quad(p000, p100, p110, p010); // front (-z)
                Quad(p101, p001, p011, p111); // back (+z)
                Quad(p001, p000, p010, p011); // left (-x)
                Quad(p100, p101, p111, p110); // right (+x)
            }

            /// <summary>Ramp rising along +z from the origin: width (x), rise (y), run (z).</summary>
            public void Wedge(float width, float rise, float run)
            {
                float h = width * 0.5f;
                Vector3 a = new(-h, 0, 0), b = new(h, 0, 0), c = new(h, 0, run), d = new(-h, 0, run);
                Vector3 e = new(h, rise, run), f = new(-h, rise, run);
                Quad(a, b, e, f);       // slope
                Quad(d, c, b, a);       // bottom
                Quad(c, d, f, e);       // back (+z)
                Triangle(b, c, e);      // right side
                Triangle(d, a, f);      // left side
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
