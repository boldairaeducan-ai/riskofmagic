using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RiskOfMagic.Maps;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Tools → Risk of Magic → Maps → Generate Map Skeletons: 5 playable greybox maps (placeholders only) with terrain or
    /// blocks, layout variants, SpawnSpots, baked NavMesh, atmosphere. Meant to be replaced by real level design:
    /// existing scenes are only overwritten after confirmation.
    /// </summary>
    public static class MapSkeletons
    {
        private const string MapsFolder = "Assets/_Project/Scenes/Maps";
        private const string TerrainFolder = MapsFolder + "/Terrain";

        private static readonly string[] SceneNames =
        {
            "Map_GrimForest", "Map_Necropolis", "Map_MountainPeak", "Map_VolcanoTomb", "Map_ChaosSanctum"
        };

        [MenuItem("Tools/Risk of Magic/Maps/Generate Map Skeletons")]
        public static void GenerateAll()
        {
            if (!MapTools.PrefabsReady(out _, out _, out _, out _))
            {
                EditorUtility.DisplayDialog("Map skeletons", "Run Tools → Risk of Magic → Build Prototype Scene first (prefabs missing).", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var specs = new[] { GrimForest(), Necropolis(), MountainPeak(), VolcanoTomb(), ChaosSanctum() };
            var existing = specs.Where(s => File.Exists(MapTools.ScenePathFor(s.SceneName))).Select(s => s.SceneName).ToList();
            bool overwrite = existing.Count == 0 || EditorUtility.DisplayDialog("Map skeletons",
                $"Ces cartes existent déjà :\n{string.Join("\n", existing)}\n\nLes écraser (level design perdu) ?", "Écraser", "Garder les existantes");

            Directory.CreateDirectory(TerrainFolder);
            foreach (var spec in specs)
            {
                if (!overwrite && existing.Contains(spec.SceneName))
                    continue;
                MapTools.CreateMapScene(spec);
            }

            if (MapTools.TrySetDefaultRotation(SceneNames))
                Debug.Log("[Risk of Magic] MapRotation set: stage 1 Grim Forest, 2 Necropolis, 3 Mountain Peak, 4 Volcano Tomb, 5 Chaos Sanctum (final).");
            else
                Debug.Log($"[Risk of Magic] MapRotation already customized: add {string.Join(", ", SceneNames)} to Configs/MapRotation yourself.");
            EditorSceneManager.OpenScene(MapTools.ScenePathFor(SceneNames[0]));
        }

        // ================================================================ Grim Forest

        private static MapTools.MapSpec GrimForest()
        {
            Terrain terrain = null;
            return new MapTools.MapSpec
            {
                SceneName = "Map_GrimForest",
                BoundsCenter = new Vector3(0f, 15f, 0f),
                BoundsSize = new Vector3(196f, 40f, 196f),
                PlayerSpawn = new Vector3(-80f, 0f, -80f),
                PlayerSpawnYaw = 45f,
                BuildLevel = level =>
                {
                    var rng = new Random(101);
                    terrain = CreateTerrain(level, "Map_GrimForest", new Vector3(220f, 20f, 220f), new Color(0.22f, 0.3f, 0.18f),
                        (u, v) => 0.12f + 0.12f * Mathf.PerlinNoise(u * 3f, v * 3f) + 0.05f * Mathf.PerlinNoise(u * 9f + 7f, v * 9f + 3f));

                    var clearings = new[]
                    {
                        (c: new Vector2(-80f, -80f), r: 14f), (c: new Vector2(0f, 0f), r: 20f), (c: new Vector2(60f, 50f), r: 16f),
                        (c: new Vector2(-60f, 55f), r: 16f), (c: new Vector2(65f, -45f), r: 16f)
                    };
                    bool InClearing(float x, float z, float margin) => clearings.Any(k => (new Vector2(x, z) - k.c).magnitude < k.r + margin);

                    var trunk = M("ForestTrunk", new Color(0.25f, 0.18f, 0.12f));
                    var leaves = M("ForestLeaves", new Color(0.12f, 0.25f, 0.12f));
                    var stone = M("ForestRuin", new Color(0.45f, 0.45f, 0.42f));

                    // Dense forest: trunks block sight lines (colliders), canopies only block the view.
                    var forest = Group(level, "Forest");
                    for (int i = 0; i < 340; i++)
                    {
                        float x = Range(rng, -97f, 97f), z = Range(rng, -97f, 97f);
                        if (InClearing(x, z, 2f))
                            continue;
                        Tree(forest, terrain, x, z, rng, trunk, leaves);
                    }

                    // Optional thickets closing some paths (variants).
                    for (int t = 0; t < 3; t++)
                    {
                        var center = new Vector2(Range(rng, -50f, 50f), Range(rng, -50f, 50f));
                        if (InClearing(center.x, center.y, 8f))
                            continue;
                        Variants(level, $"Thicket_{t}", true, option =>
                        {
                            for (int i = 0; i < 14; i++)
                            {
                                float x = center.x + Range(rng, -9f, 9f), z = center.y + Range(rng, -9f, 9f);
                                if (!InClearing(x, z, 1f))
                                    Tree(option, terrain, x, z, rng, trunk, leaves);
                            }
                        });
                    }

                    // Ruins: central tower intact or collapsed, broken walls in every clearing.
                    Vector3 c0 = OnTerrain(terrain, 0f, 0f);
                    Variants(level, "CentralRuins", false,
                        option =>
                        {
                            Block(option, "Tower", c0 + new Vector3(0f, 7f, 0f), new Vector3(8f, 14f, 8f), stone);
                            Ring(option, "Column", c0, 14f, 8, (p, i) => Cylinder(option, $"Column_{i}", p, 1.2f, 5f, stone));
                        },
                        option =>
                        {
                            Block(option, "Rubble_A", c0 + new Vector3(2f, 1f, 1f), new Vector3(6f, 2f, 4f), stone, 20f);
                            Block(option, "Rubble_B", c0 + new Vector3(-3f, 0.8f, -2f), new Vector3(4f, 1.6f, 5f), stone, -35f);
                            Ring(option, "Column", c0, 14f, 8, (p, i) => Cylinder(option, $"Column_{i}", p, 1.2f, i % 2 == 0 ? 5f : 2f, stone));
                        });
                    for (int k = 2; k < clearings.Length; k++)
                        RuinCluster(level, $"Ruins_{k}", terrain, clearings[k].c, clearings[k].r * 0.6f, rng, stone);

                    var spots = Group(level, "SpawnSpots");
                    Spot(spots, SpawnSpotType.Teleporter, OnTerrain(terrain, 60f, 50f));
                    Spot(spots, SpawnSpotType.Teleporter, OnTerrain(terrain, -60f, 55f));
                    Spot(spots, SpawnSpotType.Teleporter, OnTerrain(terrain, 65f, -45f));
                    Spot(spots, SpawnSpotType.Merchant, OnTerrain(terrain, 0f, -8f));
                    for (int k = 1; k < clearings.Length; k++)
                        for (int i = 0; i < 3; i++)
                        {
                            float angle = (i * 120f + k * 37f) * Mathf.Deg2Rad;
                            var c = clearings[k].c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (clearings[k].r * 0.8f);
                            Spot(spots, SpawnSpotType.Interactable, OnTerrain(terrain, c.x, c.y));
                        }
                },
                Atmosphere = _ => Atmosphere(new Color(0.33f, 0.4f, 0.36f), 0.018f, new Color(0.9f, 0.85f, 0.7f), 0.8f, new Color(0.25f, 0.3f, 0.28f))
            }.WithTerrainSpawn(() => terrain);
        }

        private static void Tree(Transform parent, Terrain terrain, float x, float z, Random rng, Material trunk, Material leaves)
        {
            Vector3 ground = OnTerrain(terrain, x, z);
            float height = Range(rng, 9f, 14f);
            var tree = Group(parent, "Tree");
            Cylinder(tree, "Trunk", ground - Vector3.up * 0.3f, Range(rng, 0.9f, 1.4f), height, trunk);
            var canopy = Sphere(tree, "Canopy", ground + Vector3.up * height, Range(rng, 5f, 8f), leaves);
            Object.DestroyImmediate(canopy.GetComponent<Collider>());
        }

        private static void RuinCluster(Transform level, string name, Terrain terrain, Vector2 center, float radius, Random rng, Material stone)
        {
            Variants(level, name, false,
                option => RuinWalls(option, terrain, center, radius, rng, stone, 5),
                option => RuinWalls(option, terrain, center, radius, rng, stone, 3));
        }

        private static void RuinWalls(Transform parent, Terrain terrain, Vector2 center, float radius, Random rng, Material stone, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Range(rng, 0f, 360f) * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Range(rng, 2f, radius);
                float height = Range(rng, 1.5f, 5f);
                Block(parent, $"Wall_{i}", OnTerrain(terrain, p.x, p.y) + Vector3.up * (height * 0.5f - 0.3f),
                    new Vector3(Range(rng, 4f, 9f), height, 1.2f), stone, Range(rng, 0f, 180f));
            }
        }

        // ================================================================ Necropolis

        private static MapTools.MapSpec Necropolis()
        {
            return new MapTools.MapSpec
            {
                SceneName = "Map_Necropolis",
                BoundsCenter = new Vector3(0f, 14f, 0f),
                BoundsSize = new Vector3(196f, 36f, 196f),
                PlayerSpawn = new Vector3(0f, 0f, -26f),
                BuildLevel = level =>
                {
                    var rng = new Random(202);
                    var floor = M("NecroFloor", new Color(0.2f, 0.19f, 0.22f));
                    var wall = M("NecroWall", new Color(0.32f, 0.3f, 0.34f));
                    var tomb = M("NecroTomb", new Color(0.4f, 0.38f, 0.42f));

                    Block(level, "Ground", new Vector3(0f, -0.5f, 0f), new Vector3(200f, 1f, 200f), floor);
                    var outer = Group(level, "OuterWalls");
                    Block(outer, "North", new Vector3(0f, 15f, 100f), new Vector3(202f, 30f, 2f), wall);
                    Block(outer, "South", new Vector3(0f, 15f, -100f), new Vector3(202f, 30f, 2f), wall);
                    Block(outer, "East", new Vector3(100f, 15f, 0f), new Vector3(2f, 30f, 202f), wall);
                    Block(outer, "West", new Vector3(-100f, 15f, 0f), new Vector3(2f, 30f, 202f), wall);

                    // Open central plaza (breathing room) with low tombs as cover.
                    var plaza = Group(level, "CentralPlaza");
                    Ring(plaza, "Obelisk", Vector3.zero, 18f, 4, (p, i) => Block(plaza, $"Obelisk_{i}", p + Vector3.up * 4f, new Vector3(2f, 8f, 2f), tomb));
                    for (int i = 0; i < 6; i++)
                        Block(plaza, $"Sarcophagus_{i}", new Vector3(Range(rng, -12f, 12f), 0.6f, Range(rng, -12f, 12f)), new Vector3(2.4f, 1.2f, 1.2f), tomb, Range(rng, 0f, 180f));

                    // NE / SW: mausoleums (walkable roofs at 8 m) with towers (16 m) — vertical layers.
                    Mausoleum(level, "Mausoleum_NE", new Vector3(50f, 0f, 50f), wall, tomb);
                    Mausoleum(level, "Mausoleum_SW", new Vector3(-50f, 0f, -50f), wall, tomb);

                    // Bridge between the two towers at 16 m: full, broken (leap) or none.
                    Vector3 from = new(37f, 16f, 37f), to = new(-37f, 16f, -37f);
                    Variants(level, "TowerBridge", true,
                        option => Bridge(option, "Bridge", from, to, 5f, wall),
                        option =>
                        {
                            Vector3 mid = (from + to) * 0.5f, dir = (to - from).normalized;
                            Bridge(option, "BridgeA", from, mid - dir * 4f, 5f, wall);
                            Bridge(option, "BridgeB", mid + dir * 4f, to, 5f, wall);
                        });

                    // NW / SE: catacomb corridors (walls 7 m, 6 m wide passages).
                    Catacombs(level, "Catacombs_NW", new Vector2(-95f, 25f), new Vector2(-25f, 95f), rng, wall);
                    Catacombs(level, "Catacombs_SE", new Vector2(25f, -95f), new Vector2(95f, -25f), rng, wall);

                    var spots = Group(level, "SpawnSpots");
                    Spot(spots, SpawnSpotType.Teleporter, new Vector3(44f, 16f, 44f));
                    Spot(spots, SpawnSpotType.Teleporter, new Vector3(-44f, 16f, -44f));
                    Spot(spots, SpawnSpotType.Teleporter, new Vector3(-60f, 0f, 55f));   // between catacomb wall rows
                    Spot(spots, SpawnSpotType.Teleporter, new Vector3(60f, 0f, -65f));
                    Spot(spots, SpawnSpotType.Merchant, new Vector3(0f, 0f, 14f));
                    foreach (var p in new[]
                             {
                                 new Vector3(62f, 8f, 62f), new Vector3(-62f, 8f, -62f), new Vector3(62f, 8f, 34f), new Vector3(-34f, 8f, -62f),
                                 new Vector3(-80f, 0f, 80f), new Vector3(-40f, 0f, 40f), new Vector3(80f, 0f, -80f), new Vector3(40f, 0f, -40f),
                                 new Vector3(-15f, 0f, 0f), new Vector3(15f, 0f, 0f), new Vector3(85f, 0f, 85f), new Vector3(-85f, 0f, -85f)
                             })
                        Spot(spots, SpawnSpotType.Interactable, p);
                },
                Atmosphere = _ => Atmosphere(new Color(0.22f, 0.2f, 0.28f), 0.012f, new Color(0.7f, 0.7f, 0.9f), 0.6f, new Color(0.2f, 0.2f, 0.26f))
            };
        }

        private static void Mausoleum(Transform level, string name, Vector3 center, Material wall, Material tomb)
        {
            var root = Group(level, name);
            // Roof at 8 m (solid block = building), ramps from the ground on two sides.
            Block(root, "Building", center + new Vector3(0f, 4f, 0f), new Vector3(44f, 8f, 44f), wall);
            float half = 22f, sx = Mathf.Sign(center.x), sz = Mathf.Sign(center.z);
            Ramp(root, "RampX", center + new Vector3(-sx * (half + 20f), 0f, -sz * 10f), center + new Vector3(-sx * half, 8f, -sz * 10f), 6f, tomb);
            Ramp(root, "RampZ", center + new Vector3(sx * 10f, 0f, -sz * (half + 20f)), center + new Vector3(sx * 10f, 8f, -sz * half), 6f, tomb);
            // Tower 16 m on the inner corner of the roof, ramp from the roof.
            Vector3 tower = center - new Vector3(sx * 6f, 0f, sz * 6f);
            Block(root, "Tower", tower + new Vector3(0f, 12f, 0f), new Vector3(14f, 8f, 14f), wall);
            Ramp(root, "TowerRamp", tower + new Vector3(sx * 27f, 8f, 0f), tower + new Vector3(sx * 7f, 16f, 0f), 5f, tomb);
            // Crenels on the outer roof edge (cover), away from the ramps.
            for (int i = 0; i < 6; i++)
                Block(root, $"Crenel_{i}", center + new Vector3(-half + 4f + i * 7.5f, 9f, sz * (half - 1f)), new Vector3(3f, 2f, 1f), tomb);
        }

        private static void Catacombs(Transform level, string name, Vector2 min, Vector2 max, Random rng, Material wall)
        {
            var root = Group(level, name);
            const float cell = 12f, height = 7f, thickness = 1.5f;
            int row = 0;
            for (float z = min.y + cell; z < max.y; z += cell, row++)
            {
                // Rows of wall segments with 6 m gaps, offset every other row: connected corridors.
                float x = min.x + (row % 2 == 0 ? 0f : cell * 0.5f);
                while (x < max.x)
                {
                    float length = Mathf.Min(cell + Range(rng, 0f, 6f), max.x - x);
                    if (length > 3f)
                        Block(root, "Wall", new Vector3(x + length * 0.5f, height * 0.5f, z), new Vector3(length, height, thickness), wall);
                    x += length + 6f;
                }
            }
            // Optional dead ends / shortcuts (variants).
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(Range(rng, min.x + 8f, max.x - 8f), height * 0.5f, Range(rng, min.y + 8f, max.y - 8f));
                Variants(root, $"Gate_{i}", true, option => Block(option, "GateWall", p, new Vector3(thickness, height, cell - 2f), wall));
            }
        }

        private static void Bridge(Transform parent, string name, Vector3 from, Vector3 to, float width, Material material)
        {
            Vector3 dir = to - from;
            var bridge = Block(parent, name, (from + to) * 0.5f - Vector3.up * 0.4f, new Vector3(width, 0.8f, dir.magnitude), material);
            bridge.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z));
        }

        // ================================================================ Mountain Peak

        private static MapTools.MapSpec MountainPeak()
        {
            Terrain terrain = null;
            return new MapTools.MapSpec
            {
                SceneName = "Map_MountainPeak",
                BoundsCenter = new Vector3(0f, 30f, 0f),
                BoundsSize = new Vector3(216f, 70f, 216f),
                PlayerSpawn = new Vector3(0f, 0f, -98f),
                BuildLevel = level =>
                {
                    var rng = new Random(303);
                    terrain = CreateTerrain(level, "Map_MountainPeak", new Vector3(240f, 60f, 240f), new Color(0.75f, 0.78f, 0.82f), (u, v) =>
                    {
                        float r = new Vector2(u - 0.5f, v - 0.5f).magnitude / 0.46f;
                        float mountain = 0.6f * (1f - Mathf.SmoothStep(0.18f, 1f, r));
                        return 0.03f + mountain + 0.035f * Mathf.PerlinNoise(u * 8f + 3f, v * 8f + 9f);
                    });
                    var stone = M("PeakTemple", new Color(0.6f, 0.58f, 0.52f));
                    var rock = M("PeakRock", new Color(0.4f, 0.42f, 0.46f));

                    // Temple on the summit plateau.
                    Vector3 summit = OnTerrain(terrain, 0f, 0f);
                    var temple = Group(level, "Temple");
                    Block(temple, "Platform", summit + new Vector3(0f, 0.15f, 0f), new Vector3(36f, 0.6f, 36f), stone);
                    Block(temple, "Altar", summit + new Vector3(0f, 1.2f, 0f), new Vector3(4f, 1.8f, 2f), stone);
                    Variants(temple, "TempleState", false,
                        option => Ring(option, "Column", summit, 15f, 12, (p, i) => Cylinder(option, $"Column_{i}", p, 1.6f, 9f, stone)),
                        option =>
                        {
                            Ring(option, "Column", summit, 15f, 12, (p, i) =>
                            {
                                if (i % 2 == 0)
                                    Cylinder(option, $"Column_{i}", p, 1.6f, 9f, stone);
                                else
                                    Block(option, $"Fallen_{i}", p + Vector3.up * 0.8f, new Vector3(1.6f, 1.6f, 7f), stone, i * 30f);
                            });
                        });

                    // Boulders on the slopes (cover), some groups optional.
                    var rocks = Group(level, "Boulders");
                    for (int i = 0; i < 45; i++)
                        Boulder(rocks, terrain, rng, rock, 25f, 100f);
                    for (int g = 0; g < 4; g++)
                    {
                        int group = g;
                        Variants(level, $"RockField_{g}", true, option =>
                        {
                            for (int i = 0; i < 8; i++)
                                Boulder(option, terrain, rng, rock, 30f + group * 15f, 45f + group * 15f);
                        });
                    }

                    var spots = Group(level, "SpawnSpots");
                    Spot(spots, SpawnSpotType.Teleporter, summit + new Vector3(0f, 0.45f, 8f));
                    Spot(spots, SpawnSpotType.Teleporter, OnTerrain(terrain, 70f, 10f));
                    Spot(spots, SpawnSpotType.Teleporter, OnTerrain(terrain, -70f, 25f));
                    Spot(spots, SpawnSpotType.Merchant, OnTerrain(terrain, 12f, -60f));
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = i * 30f * Mathf.Deg2Rad;
                        float radius = i % 2 == 0 ? 45f : 75f;
                        Spot(spots, SpawnSpotType.Interactable, OnTerrain(terrain, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
                    }
                },
                Atmosphere = level =>
                {
                    Atmosphere(new Color(0.82f, 0.86f, 0.92f), 0.004f, new Color(0.95f, 0.97f, 1f), 1.1f, new Color(0.45f, 0.5f, 0.58f));
                    var weather = new GameObject("Weather_Blizzard");
                    var blizzard = weather.AddComponent<BlizzardWeather>();
                    var snow = new GameObject("Snow").AddComponent<ParticleSystem>();
                    snow.transform.SetParent(weather.transform);
                    var main = snow.main;
                    main.startLifetime = 3f;
                    main.startSpeed = 6f;
                    main.startSize = 0.12f;
                    main.maxParticles = 4000;
                    main.simulationSpace = ParticleSystemSimulationSpace.World;
                    var shape = snow.shape;
                    shape.shapeType = ParticleSystemShapeType.Box;
                    shape.scale = new Vector3(40f, 1f, 40f);
                    shape.rotation = new Vector3(90f, 0f, 0f);
                    var velocity = snow.velocityOverLifetime;
                    velocity.enabled = true;
                    velocity.space = ParticleSystemSimulationSpace.World;
                    velocity.x = new ParticleSystem.MinMaxCurve(6f, 10f);
                    velocity.y = new ParticleSystem.MinMaxCurve(-6f, -4f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-2f, 2f);
                    snow.GetComponent<ParticleSystemRenderer>().sharedMaterial = M("Snow", Color.white, unlit: true);
                    var so = new SerializedObject(blizzard);
                    so.FindProperty("snow").objectReferenceValue = snow;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }.WithTerrainSpawn(() => terrain);
        }

        private static void Boulder(Transform parent, Terrain terrain, Random rng, Material rock, float minRadius, float maxRadius)
        {
            float angle = Range(rng, 0f, 360f) * Mathf.Deg2Rad, radius = Range(rng, minRadius, maxRadius);
            float x = Mathf.Cos(angle) * radius, z = Mathf.Sin(angle) * radius;
            if (z < -80f && Mathf.Abs(x) < 15f)
                return; // keep the spawn clear
            float size = Range(rng, 2.5f, 7f);
            Sphere(parent, "Boulder", OnTerrain(terrain, x, z) + Vector3.up * (size * 0.25f), size, rock);
        }

        // ================================================================ Tomb of the Volcano God

        private static MapTools.MapSpec VolcanoTomb()
        {
            Terrain terrain = null;
            return new MapTools.MapSpec
            {
                SceneName = "Map_VolcanoTomb",
                BoundsCenter = new Vector3(0f, 15f, 0f),
                BoundsSize = new Vector3(196f, 40f, 196f),
                PlayerSpawn = new Vector3(-82f, 0f, -82f),
                PlayerSpawnYaw = 45f,
                BuildLevel = level =>
                {
                    var rng = new Random(404);
                    // Lava basins carved in the rolling terrain (not on the spawn nor the temple).
                    var basins = new List<(Vector2 c, float r)>();
                    for (int attempt = 0; attempt < 60 && basins.Count < 7; attempt++)
                    {
                        var c = new Vector2(Range(rng, -85f, 85f), Range(rng, -85f, 85f));
                        float r = Range(rng, 9f, 16f);
                        if ((c - new Vector2(-82f, -82f)).magnitude < r + 22f || c.magnitude < r + 34f
                            || basins.Any(b => (b.c - c).magnitude < b.r + r + 10f))
                            continue;
                        basins.Add((c, r));
                    }
                    const float worldSize = 220f, worldHeight = 18f;
                    float Hills(float u, float v) => 0.3f + 0.22f * Mathf.PerlinNoise(u * 4f + 1f, v * 4f + 5f) + 0.08f * Mathf.PerlinNoise(u * 11f, v * 11f);
                    const float basinDepth = 1.4f / worldHeight;
                    terrain = CreateTerrain(level, "Map_VolcanoTomb", new Vector3(worldSize, worldHeight, worldSize), new Color(0.22f, 0.17f, 0.15f), (u, v) =>
                    {
                        float h = Hills(u, v);
                        var p = new Vector2((u - 0.5f) * worldSize, (v - 0.5f) * worldSize);
                        foreach (var basin in basins)
                        {
                            float d = (p - basin.c).magnitude;
                            Vector2 cu = basin.c / worldSize + new Vector2(0.5f, 0.5f);
                            float floor = Hills(cu.x, cu.y) - basinDepth;
                            float t = Mathf.InverseLerp(basin.r + 4f, basin.r, d); // 0 outside the rim, 1 inside
                            h = Mathf.Lerp(h, Mathf.Min(h, floor), Mathf.SmoothStep(0f, 1f, t));
                        }
                        return h;
                    });

                    var lavaMat = M("Lava", new Color(1f, 0.35f, 0.05f), unlit: true);
                    var obsidian = M("Obsidian", new Color(0.12f, 0.1f, 0.12f));
                    var temple = M("VolcanoTemple", new Color(0.35f, 0.25f, 0.2f));

                    var lava = Group(level, "Lava");
                    for (int i = 0; i < basins.Count; i++)
                    {
                        var (c, r) = basins[i];
                        Vector3 floor = OnTerrain(terrain, c.x, c.y);
                        var pool = Cylinder(lava, $"LavaPool_{i}", floor + Vector3.up * 0.25f, r * 2f, 0.1f, lavaMat);
                        Object.DestroyImmediate(pool.GetComponent<Collider>());
                        var hazard = new GameObject($"LavaHazard_{i}").AddComponent<HazardZone>();
                        hazard.transform.SetParent(lava, false);
                        hazard.transform.position = floor;
                        var so = new SerializedObject(hazard);
                        so.FindProperty("shape").enumValueIndex = (int)HazardZone.ZoneShape.Cylinder;
                        so.FindProperty("size").vector3Value = new Vector3(r * 2f, 2.5f, r * 2f);
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }

                    // Stepped temple of the volcano god at the center: two terraces, long ramps (16°) up to the top.
                    Vector3 c0 = OnTerrain(terrain, 0f, 0f);
                    var tomb = Group(level, "Temple");
                    Block(tomb, "Step1", c0 + new Vector3(0f, 1.2f, 0f), new Vector3(30f, 3f, 30f), temple);
                    Block(tomb, "Step2", c0 + new Vector3(0f, 4.2f, 0f), new Vector3(18f, 3f, 18f), temple);
                    var statue = Group(tomb, "Statue");
                    Cylinder(statue, "Body", c0 + new Vector3(0f, 5.7f, 0f), 3f, 7f, obsidian);
                    Sphere(statue, "Head", c0 + new Vector3(0f, 13.5f, 0f), 3.5f, obsidian);
                    Variants(tomb, "TempleRamps", false,
                        option =>
                        {
                            Ramp(option, "RampSouth", c0 + new Vector3(0f, -0.3f, -30f), c0 + new Vector3(0f, 5.7f, -9f), 5f, temple);
                            Ramp(option, "RampNorth", c0 + new Vector3(0f, -0.3f, 30f), c0 + new Vector3(0f, 5.7f, 9f), 5f, temple);
                        },
                        option =>
                        {
                            Ramp(option, "RampEast", c0 + new Vector3(30f, -0.3f, 0f), c0 + new Vector3(9f, 5.7f, 0f), 5f, temple);
                            Ramp(option, "RampWest", c0 + new Vector3(-30f, -0.3f, 0f), c0 + new Vector3(-9f, 5.7f, 0f), 5f, temple);
                        });

                    // Obsidian spires break the sight lines.
                    var spires = Group(level, "Spires");
                    for (int i = 0; i < 40; i++)
                    {
                        float x = Range(rng, -95f, 95f), z = Range(rng, -95f, 95f);
                        var p2 = new Vector2(x, z);
                        if (p2.magnitude < 34f || (p2 - new Vector2(-82f, -82f)).magnitude < 15f || basins.Any(b => (p2 - b.c).magnitude < b.r + 2f))
                            continue;
                        Cylinder(spires, "Spire", OnTerrain(terrain, x, z) - Vector3.up * 0.5f, Range(rng, 1.8f, 3.2f), Range(rng, 7f, 15f), obsidian);
                    }

                    var spots = Group(level, "SpawnSpots");
                    Spot(spots, SpawnSpotType.Teleporter, c0 + new Vector3(0f, 5.7f, 5f));
                    Spot(spots, SpawnSpotType.Teleporter, OnTerrain(terrain, 70f, 60f));
                    Spot(spots, SpawnSpotType.Teleporter, OnTerrain(terrain, -60f, 70f));
                    Spot(spots, SpawnSpotType.Merchant, OnTerrain(terrain, -40f, -25f));
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = (i * 30f + 15f) * Mathf.Deg2Rad;
                        float radius = i % 2 == 0 ? 40f : 72f;
                        var p2 = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                        if (basins.Any(b => (p2 - b.c).magnitude < b.r + 3f))
                            continue;
                        Spot(spots, SpawnSpotType.Interactable, OnTerrain(terrain, p2.x, p2.y));
                    }
                },
                Atmosphere = _ => Atmosphere(new Color(0.45f, 0.22f, 0.12f), 0.01f, new Color(1f, 0.6f, 0.4f), 0.9f, new Color(0.35f, 0.2f, 0.15f))
            }.WithTerrainSpawn(() => terrain);
        }

        // ================================================================ Chaos Sanctum (final stage)

        private static MapTools.MapSpec ChaosSanctum()
        {
            return new MapTools.MapSpec
            {
                SceneName = "Map_ChaosSanctum",
                BoundsCenter = new Vector3(0f, 8f, 0f),
                BoundsSize = new Vector3(140f, 24f, 140f),
                PlayerSpawn = new Vector3(0f, 0f, -55f),
                BuildLevel = level =>
                {
                    var rng = new Random(505);
                    var floor = M("SanctumFloor", new Color(0.18f, 0.12f, 0.22f));
                    var stone = M("SanctumStone", new Color(0.3f, 0.22f, 0.36f));
                    var glow = M("SanctumGlow", new Color(0.8f, 0.2f, 0.7f), unlit: true);

                    Cylinder(level, "Arena", new Vector3(0f, -2f, 0f), 140f, 2f, floor);
                    Ring(level, "OuterWall", Vector3.zero, 70f, 28, (p, i) =>
                    {
                        var wall = Block(level, $"OuterWall_{i}", p + Vector3.up * 6f, new Vector3(17f, 12f, 2f), stone);
                        wall.transform.rotation = Quaternion.LookRotation(-p.normalized);
                    });
                    var dais = Cylinder(level, "Dais", new Vector3(0f, 0f, 0f), 24f, 0.25f, stone);
                    Cylinder(level, "DaisRune", new Vector3(0f, 0.26f, 0f), 18f, 0.02f, glow).GetComponent<Collider>().enabled = false;
                    Ring(level, "Pillar", Vector3.zero, 40f, 8, (p, i) => Cylinder(level, $"Pillar_{i}", p, 3f, 14f, stone));
                    for (int g = 0; g < 4; g++)
                    {
                        float angle = (g * 90f + 45f) * Mathf.Deg2Rad;
                        var center = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 25f;
                        Variants(level, $"Cover_{g}", true, option =>
                        {
                            for (int i = 0; i < 3; i++)
                                Block(option, $"Block_{i}", center + new Vector3(Range(rng, -4f, 4f), 1.2f, Range(rng, -4f, 4f)),
                                    new Vector3(Range(rng, 2f, 4f), 2.4f, Range(rng, 2f, 4f)), stone, Range(rng, 0f, 90f));
                        });
                    }
                    _ = dais;

                    var spots = Group(level, "SpawnSpots");
                    Spot(spots, SpawnSpotType.Teleporter, new Vector3(0f, 0.25f, 4f));
                    Spot(spots, SpawnSpotType.Merchant, new Vector3(-20f, 0f, -48f));
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = (i * 45f + 22.5f) * Mathf.Deg2Rad;
                        Spot(spots, SpawnSpotType.Interactable, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 52f);
                    }
                },
                Atmosphere = _ => Atmosphere(new Color(0.16f, 0.08f, 0.2f), 0.008f, new Color(0.85f, 0.5f, 1f), 0.7f, new Color(0.2f, 0.12f, 0.25f))
            };
        }

        // ================================================================ Helpers

        /// <summary>Terrain maps: put the player spawn on the terrain surface once the terrain exists.</summary>
        private static MapTools.MapSpec WithTerrainSpawn(this MapTools.MapSpec spec, Func<Terrain> terrain)
        {
            var build = spec.BuildLevel;
            spec.BuildLevel = level =>
            {
                build(level);
                var t = terrain();
                if (t != null)
                    spec.PlayerSpawn = OnTerrain(t, spec.PlayerSpawn.x, spec.PlayerSpawn.z) + Vector3.up * 0.2f;
            };
            return spec;
        }

        private static Terrain CreateTerrain(Transform parent, string mapName, Vector3 size, Color color, Func<float, float, float> height01)
        {
            const int resolution = 257;
            var data = new TerrainData { heightmapResolution = resolution, size = size };
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                    heights[z, x] = Mathf.Clamp01(height01(x / (resolution - 1f), z / (resolution - 1f)));
            data.SetHeights(0, 0, heights);

            string dataPath = $"{TerrainFolder}/{mapName}_TerrainData.asset";
            string texturePath = $"{TerrainFolder}/{mapName}_Ground.asset";
            string layerPath = $"{TerrainFolder}/{mapName}_Ground.terrainlayer";
            AssetDatabase.DeleteAsset(dataPath);
            AssetDatabase.DeleteAsset(texturePath);
            AssetDatabase.DeleteAsset(layerPath);

            var texture = new Texture2D(4, 4);
            texture.SetPixels(Enumerable.Repeat(color, 16).ToArray());
            texture.Apply();
            AssetDatabase.CreateAsset(texture, texturePath);
            var layer = new TerrainLayer { diffuseTexture = texture, tileSize = new Vector2(10f, 10f) };
            AssetDatabase.CreateAsset(layer, layerPath);
            data.terrainLayers = new[] { layer };
            AssetDatabase.CreateAsset(data, dataPath);

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-size.x * 0.5f, 0f, -size.z * 0.5f);
            var terrain = go.GetComponent<Terrain>();
            terrain.Flush();
            return terrain;
        }

        private static Vector3 OnTerrain(Terrain terrain, float x, float z)
        {
            var p = new Vector3(x, 0f, z);
            p.y = terrain.SampleHeight(p) + terrain.transform.position.y;
            return p;
        }

        private static void Atmosphere(Color fogColor, float fogDensity, Color lightColor, float lightIntensity, Color ambient)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.type != LightType.Directional)
                    continue;
                light.color = lightColor;
                light.intensity = lightIntensity;
            }
        }

        private static Material M(string name, Color color, bool unlit = false) => PrototypeSceneBuilder.Mat("M_Map_" + name, color, unlit);

        private static Transform Group(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void Variants(Transform parent, string name, bool allowNone, params Action<Transform>[] options)
        {
            var group = Group(parent, name);
            var variant = group.gameObject.AddComponent<LayoutVariantGroup>();
            if (allowNone)
            {
                var so = new SerializedObject(variant);
                so.FindProperty("allowNone").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            for (int i = 0; i < options.Length; i++)
                options[i](Group(group, $"Option{(char)('A' + i)}"));
        }

        private static GameObject Block(Transform parent, string name, Vector3 center, Vector3 size, Material material, float yaw = 0f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Cylinder standing on basePosition.</summary>
        private static GameObject Cylinder(Transform parent, string name, Vector3 basePosition, float diameter, float height, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = basePosition + Vector3.up * (height * 0.5f);
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static GameObject Sphere(Transform parent, string name, Vector3 center, float diameter, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = Vector3.one * diameter;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Walkable slab from the top of `from` to the top of `to` (keep it under ~30°).</summary>
        private static GameObject Ramp(Transform parent, string name, Vector3 from, Vector3 to, float width, Material material)
        {
            const float thickness = 0.6f;
            Vector3 dir = to - from;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(dir);
            go.transform.position = (from + to) * 0.5f - go.transform.up * (thickness * 0.5f);
            go.transform.localScale = new Vector3(width, thickness, dir.magnitude + 0.5f);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        private static void Ring(Transform parent, string name, Vector3 center, float radius, int count, Action<Vector3, int> place)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                place(center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius, i);
            }
        }

        private static void Spot(Transform parent, SpawnSpotType type, Vector3 position) => MapTools.AddSpot(parent.gameObject, type, position);

        private static float Range(Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
