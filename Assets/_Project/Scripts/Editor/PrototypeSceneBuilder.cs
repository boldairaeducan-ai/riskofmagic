using System.Collections.Generic;
using System.IO;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Core;
using RiskOfMagic.Economy;
using RiskOfMagic.Enemies;
using RiskOfMagic.Interaction;
using RiskOfMagic.Networking;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Skills;
using RiskOfMagic.Skills.Types;
using RiskOfMagic.UI;
using RiskOfMagic.Vfx;
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
    /// One-click prototype: characters, skills, projectiles, VFX, enemies, director and a test level.
    /// Re-running updates the generated assets in place (tweaks made in the inspector are overwritten).
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string Root = "Assets/_Project";
        private const string Configs = Root + "/Configs";
        private const string SkillsFolder = Configs + "/Skills";
        private const string CharactersFolder = Configs + "/Characters";
        private const string ProjectilesFolder = Configs + "/Projectiles";
        private const string EnemiesFolder = Configs + "/Enemies";
        private const string Prefabs = Root + "/Prefabs";
        private const string VfxFolder = Prefabs + "/Vfx";
        private const string Materials = Root + "/Materials";
        private const string Scenes = Root + "/Scenes";
        private const string ResourcesFolder = Root + "/Resources";
        private const string ScenePath = Scenes + "/Prototype.unity";

        private sealed class VfxSet
        {
            public GameObject Impact, FrostBurst, Area, Swing, Lightning, Blink;
        }

        private sealed class ProjectileSet
        {
            public ProjectileDefinition Arrow, PiercingArrow, FireBolt, FrostOrb, EnemyBolt;
            public List<ProjectileDefinition> All => new() { Arrow, PiercingArrow, FireBolt, FrostOrb, EnemyBolt };
        }

        [MenuItem("Tools/Risk of Magic/Build Prototype Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            foreach (var folder in new[] { SkillsFolder, CharactersFolder, ProjectilesFolder, EnemiesFolder, VfxFolder, Materials, Scenes, ResourcesFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            var vfx = BuildVfx();
            var projectiles = BuildProjectiles(vfx);
            var characters = BuildCharacters(projectiles, vfx);

            var catalog = Asset<GameCatalog>(ResourcesFolder + "/GameCatalog.asset");
            catalog.EditorSet(characters, projectiles.All);
            EditorUtility.SetDirty(catalog);

            var playerPrefab = BuildPlayerPrefab();
            var ghoul = BuildGhoul();
            var skeleton = BuildSkeletonArcher(projectiles.EnemyBolt);

            string listPath = Configs + "/NetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(listPath);
            var prefabList = Asset<NetworkPrefabsList>(listPath);
            prefabList.Add(new NetworkPrefab { Prefab = ghoul.gameObject });
            prefabList.Add(new NetworkPrefab { Prefab = skeleton.gameObject });
            EditorUtility.SetDirty(prefabList);

            var cards = new List<SpawnCard>
            {
                Card("SpawnCard_Ghoul", ghoul, 10, 1f, 0f),
                Card("SpawnCard_SkeletonArcher", skeleton, 16, 0.7f, 0f)
            };

            AssetDatabase.SaveAssets();
            BuildScene(playerPrefab, prefabList, cards);
            AssetDatabase.SaveAssets();
            Debug.Log("[Risk of Magic] Prototype built. Play → choose a character → Host.");
        }

        // ---------------------------------------------------------------- VFX & projectiles

        private static VfxSet BuildVfx()
        {
            return new VfxSet
            {
                Impact = EffectPrefab("VFX_Impact", PrimitiveType.Sphere, Vector3.one * 0.5f, Vector3.zero, new Color(1f, 0.9f, 0.6f), 0.15f),
                FrostBurst = EffectPrefab("VFX_FrostBurst", PrimitiveType.Sphere, Vector3.one * 6f, Vector3.zero, new Color(0.6f, 0.9f, 1f), 0.2f),
                Area = EffectPrefab("VFX_Area", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.05f, 0f), new Color(1f, 0.6f, 0.2f), 0.25f),
                Swing = EffectPrefab("VFX_Swing", PrimitiveType.Cylinder, new Vector3(1f, 0.01f, 1f), Vector3.zero, new Color(0.9f, 0.9f, 1f), 0.12f),
                Blink = EffectPrefab("VFX_Blink", PrimitiveType.Sphere, Vector3.one * 1.2f, new Vector3(0f, 1f, 0f), new Color(0.7f, 0.4f, 1f), 0.3f),
                Lightning = LinePrefab("VFX_Lightning", new Color(0.6f, 0.85f, 1f), 0.15f, 0.2f)
            };
        }

        private static ProjectileSet BuildProjectiles(VfxSet vfx)
        {
            var set = new ProjectileSet
            {
                Arrow = ProjectileAsset("Projectile_Arrow", new Color(0.9f, 0.8f, 0.5f), 0.15f, vfx.Impact),
                PiercingArrow = ProjectileAsset("Projectile_PiercingArrow", new Color(1f, 0.85f, 0.2f), 0.25f, vfx.Impact),
                FireBolt = ProjectileAsset("Projectile_FireBolt", new Color(1f, 0.45f, 0.1f), 0.35f, vfx.Impact),
                FrostOrb = ProjectileAsset("Projectile_FrostOrb", new Color(0.5f, 0.85f, 1f), 0.5f, vfx.FrostBurst),
                EnemyBolt = ProjectileAsset("Projectile_EnemyBolt", new Color(0.4f, 1f, 0.3f), 0.3f, vfx.Impact)
            };

            Configure(set.Arrow, speed: 110f, gravity: 6f, lifetime: 2f, radius: 0.1f);
            Configure(set.PiercingArrow, speed: 160f, gravity: 0f, lifetime: 1.5f, radius: 0.25f, pierce: 10);
            Configure(set.FireBolt, speed: 70f, gravity: 0f, lifetime: 2f, radius: 0.2f, explosion: 1.5f);
            Configure(set.FrostOrb, speed: 40f, gravity: 0f, lifetime: 1.5f, radius: 0.3f, explosion: 3f, explodeOnExpire: true);
            Configure(set.EnemyBolt, speed: 35f, gravity: 0f, lifetime: 3f, radius: 0.25f);
            return set;
        }

        private static void Configure(ProjectileDefinition p, float speed, float gravity, float lifetime, float radius,
            int pierce = 0, float explosion = 0f, bool explodeOnExpire = false)
        {
            p.speed = speed;
            p.gravity = gravity;
            p.lifetime = lifetime;
            p.radius = radius;
            p.pierceCount = pierce;
            p.explosionRadius = explosion;
            p.explodeOnExpire = explodeOnExpire;
            EditorUtility.SetDirty(p);
        }

        // ---------------------------------------------------------------- Characters & skills

        private static List<CharacterDefinition> BuildCharacters(ProjectileSet p, VfxSet vfx)
        {
            // Archer
            var rapidShot = Skill<ProjectileSkill>("Archer_RapidShot", "Tir rapide", "Flèches en continu. 150% de dégâts.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.3f, attackSpeed: true);
            rapidShot.projectile = p.Arrow;
            rapidShot.damageCoefficient = 1.5f;

            var piercing = Skill<ProjectileSkill>("Archer_PiercingArrow", "Flèche perforante", "Charger pour transpercer les ennemis. 200-700%.",
                SkillActivationMode.Charge, cooldown: 4f, cast: 0.3f);
            piercing.projectile = p.PiercingArrow;
            piercing.maxChargeTime = 1.2f;
            piercing.minChargeCoefficient = 2f;
            piercing.maxChargeCoefficient = 7f;
            piercing.minChargeScale = 1f;
            piercing.maxChargeScale = 1.6f;

            var roll = Skill<DashSkill>("Archer_Roll", "Roulade", "Esquive rapide dans la direction de déplacement.",
                SkillActivationMode.Press, cooldown: 4f, cast: 0f);
            roll.direction = DashDirection.MoveInput;
            roll.speed = 28f;
            roll.duration = 0.22f;
            roll.canCastWhileBusy = true;
            roll.keepsSprint = true;

            var rain = Skill<AreaSkill>("Archer_ArrowRain", "Pluie de flèches", "Zone ciblée frappée 8 fois. 100% par impact.",
                SkillActivationMode.Press, cooldown: 12f, cast: 0.4f);
            rain.center = AreaCenter.AimPoint;
            rain.maxRange = 50f;
            rain.radius = 6f;
            rain.damageCoefficient = 1f;
            rain.ticks = 8;
            rain.tickInterval = 0.3f;
            rain.initialDelay = 0.4f;
            rain.tickVfx = vfx.Area;

            // Warrior
            var combo = Skill<MeleeComboSkill>("Warrior_SwordCombo", "Combo d'épée", "Trois coups, le dernier frappe fort.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.4f, attackSpeed: true);
            combo.swingVfx = vfx.Swing;

            var whirlwind = Skill<AreaSkill>("Warrior_Whirlwind", "Tourbillon", "3 coups circulaires. 140% chacun.",
                SkillActivationMode.Press, cooldown: 5f, cast: 0.6f);
            whirlwind.center = AreaCenter.Caster;
            whirlwind.radius = 4f;
            whirlwind.ticks = 3;
            whirlwind.tickInterval = 0.2f;
            whirlwind.damageCoefficient = 1.4f;
            whirlwind.tickVfx = vfx.Area;

            var charge = Skill<DashSkill>("Warrior_Charge", "Charge", "Fonce en avant, 300% aux ennemis percutés.",
                SkillActivationMode.Press, cooldown: 6f, cast: 0.3f);
            charge.direction = DashDirection.AimFlat;
            charge.speed = 30f;
            charge.duration = 0.3f;
            charge.damageCoefficient = 3f;
            charge.damageRadius = 1.8f;

            var slam = Skill<AreaSkill>("Warrior_SeismicSlam", "Frappe sismique", "Bond puis onde de choc. 600%.",
                SkillActivationMode.Press, cooldown: 12f, cast: 0.5f);
            slam.center = AreaCenter.Caster;
            slam.radius = 8f;
            slam.ticks = 1;
            slam.initialDelay = 0.35f;
            slam.damageCoefficient = 6f;
            slam.hitOncePerTarget = true;
            slam.casterUpwardVelocity = 6f;
            slam.tickVfx = vfx.Area;

            // Mage
            var fireBolt = Skill<ProjectileSkill>("Mage_FireBolt", "Trait de feu", "4 charges. 220%, petite explosion.",
                SkillActivationMode.Hold, cooldown: 1.2f, cast: 0.2f, attackSpeed: true);
            fireBolt.maxStock = 4;
            fireBolt.projectile = p.FireBolt;
            fireBolt.damageType = DamageType.Fire;
            fireBolt.damageCoefficient = 2.2f;

            var frostOrb = Skill<ProjectileSkill>("Mage_FrostOrb", "Orbe de givre", "Charger un orbe explosif. 200-800%.",
                SkillActivationMode.Charge, cooldown: 5f, cast: 0.3f);
            frostOrb.projectile = p.FrostOrb;
            frostOrb.damageType = DamageType.Frost;
            frostOrb.maxChargeTime = 1.5f;
            frostOrb.minChargeCoefficient = 2f;
            frostOrb.maxChargeCoefficient = 8f;
            frostOrb.minChargeScale = 1f;
            frostOrb.maxChargeScale = 2f;

            var blink = Skill<BlinkSkill>("Mage_Blink", "Transfert", "Téléportation dans la direction visée.",
                SkillActivationMode.Press, cooldown: 5f, cast: 0.1f);
            blink.distance = 14f;
            blink.canCastWhileBusy = true;
            blink.keepsSprint = true;
            blink.castVfx = vfx.Blink;
            blink.castVfxAnchor = VfxAnchor.Caster;
            blink.arrivalVfx = vfx.Blink;

            var chain = Skill<ChainLightningSkill>("Mage_ChainLightning", "Chaîne d'éclairs", "Éclair rebondissant sur 5 ennemis. 500%.",
                SkillActivationMode.Press, cooldown: 8f, cast: 0.3f);
            chain.damageCoefficient = 5f;
            chain.maxBounces = 4;
            chain.boltVfx = vfx.Lightning;

            foreach (var skill in new SkillDefinition[] { rapidShot, piercing, roll, rain, combo, whirlwind, charge, slam, fireBolt, frostOrb, blink, chain })
                EditorUtility.SetDirty(skill);

            return new List<CharacterDefinition>
            {
                Character("Character_Archer", "Archer", "Agile, double saut, dégâts à distance.", new Color(0.3f, 0.75f, 0.35f),
                    90f, Movement("Movement_Archer", 7.5f, 2), rapidShot, piercing, roll, rain),
                Character("Character_Warrior", "Guerrier", "Robuste, combat au corps à corps.", new Color(0.8f, 0.3f, 0.25f),
                    160f, Movement("Movement_Warrior", 7f, 1), combo, whirlwind, charge, slam),
                Character("Character_Mage", "Mage", "Sorts élémentaires et mobilité magique.", new Color(0.35f, 0.45f, 0.95f),
                    100f, Movement("Movement_Mage", 7f, 1), fireBolt, frostOrb, blink, chain)
            };
        }

        private static T Skill<T>(string file, string displayName, string description, SkillActivationMode mode,
            float cooldown, float cast, bool attackSpeed = false) where T : SkillDefinition
        {
            var skill = Asset<T>($"{SkillsFolder}/{file}.asset");
            skill.displayName = displayName;
            skill.description = description;
            skill.activationMode = mode;
            skill.cooldown = cooldown;
            skill.castDuration = cast;
            skill.scalesWithAttackSpeed = attackSpeed;
            return skill;
        }

        private static MovementConfig Movement(string file, float walkSpeed, int maxJumps)
        {
            var config = Asset<MovementConfig>($"{CharactersFolder}/{file}.asset");
            config.walkSpeed = walkSpeed;
            config.maxJumps = maxJumps;
            EditorUtility.SetDirty(config);
            return config;
        }

        private static CharacterDefinition Character(string file, string displayName, string description, Color color, float health,
            MovementConfig movement, SkillDefinition primary, SkillDefinition secondary, SkillDefinition utility, SkillDefinition special)
        {
            var c = Asset<CharacterDefinition>($"{CharactersFolder}/{file}.asset");
            c.displayName = displayName;
            c.description = description;
            c.bodyColor = color;
            c.maxHealth = health;
            c.baseDamage = 12f;
            c.movement = movement;
            c.primary = primary;
            c.secondary = secondary;
            c.utility = utility;
            c.special = special;
            EditorUtility.SetDirty(c);
            return c;
        }

        // ---------------------------------------------------------------- Player & enemies

        private static GameObject BuildPlayerPrefab()
        {
            var root = new GameObject("Player");
            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            root.AddComponent<NetworkObject>();
            ConfigureTransform(root.AddComponent<OwnerNetworkTransform>());

            var health = root.AddComponent<Health>();
            SetProperties(health, so => so.FindProperty("team").enumValueIndex = (int)Team.Player);

            root.AddComponent<PlayerInputHandler>();
            root.AddComponent<PlayerMotor>();
            root.AddComponent<CharacterStats>();
            root.AddComponent<PlayerWallet>();

            var body = CreateVisual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 0.9f, 0.8f), Mat("M_Player", Color.white));

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

            var avatar = root.AddComponent<PlayerAvatar>();
            SetProperties(avatar, so =>
            {
                var renderers = so.FindProperty("bodyRenderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = body.GetComponent<Renderer>();
            });
            root.AddComponent<SkillController>();
            root.AddComponent<PlayerInteractor>();

            return SavePrefab(root, Prefabs + "/Player.prefab");
        }

        private static EnemyActor BuildGhoul()
        {
            var def = EnemyDef("Enemy_Ghoul", "Goule", health: 60f, speed: 5f, gold: 8);
            var root = EnemyBase("Enemy_Ghoul", def, new Color(0.75f, 0.2f, 0.2f), out _);
            var attack = root.AddComponent<EnemyMeleeAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 2f;
                so.FindProperty("cooldown").floatValue = 1.5f;
                so.FindProperty("damage").floatValue = 12f;
            });
            FinishEnemy(root, attack);
            return SavePrefab(root, Prefabs + "/Enemy_Ghoul.prefab").GetComponent<EnemyActor>();
        }

        private static EnemyActor BuildSkeletonArcher(ProjectileDefinition bolt)
        {
            var def = EnemyDef("Enemy_SkeletonArcher", "Squelette archer", health: 45f, speed: 4f, gold: 12);
            var root = EnemyBase("Enemy_SkeletonArcher", def, new Color(0.85f, 0.85f, 0.75f), out _);
            var attack = root.AddComponent<EnemyProjectileAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 22f;
                so.FindProperty("cooldown").floatValue = 2.5f;
                so.FindProperty("damage").floatValue = 10f;
                so.FindProperty("projectile").objectReferenceValue = bolt;
            });
            FinishEnemy(root, attack);
            return SavePrefab(root, Prefabs + "/Enemy_SkeletonArcher.prefab").GetComponent<EnemyActor>();
        }

        private static EnemyDefinition EnemyDef(string file, string displayName, float health, float speed, int gold)
        {
            var def = Asset<EnemyDefinition>($"{EnemiesFolder}/{file}.asset");
            def.displayName = displayName;
            def.maxHealth = health;
            def.moveSpeed = speed;
            def.goldReward = gold;
            EditorUtility.SetDirty(def);
            return def;
        }

        private static GameObject EnemyBase(string name, EnemyDefinition def, Color color, out Health health)
        {
            var material = Mat("M_" + name, color);
            var root = new GameObject(name);
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 0.8f, 0f);
            capsule.height = 1.6f;
            capsule.radius = 0.45f;

            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.45f;
            agent.height = 2.1f;
            agent.enabled = false;

            root.AddComponent<NetworkObject>();
            ConfigureTransform(root.AddComponent<NetworkTransform>());

            health = root.AddComponent<Health>();
            SetProperties(health, so => so.FindProperty("team").enumValueIndex = (int)Team.Enemy);
            var actor = root.AddComponent<EnemyActor>();
            SetProperties(actor, so => so.FindProperty("definition").objectReferenceValue = def);

            CreateVisual(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(0.9f, 0.8f, 0.9f), material);

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.85f, 0f);
            head.transform.localScale = Vector3.one * 0.6f;
            head.GetComponent<Renderer>().sharedMaterial = material;
            var hitbox = head.AddComponent<Hitbox>();
            var ownerHealth = health;
            SetProperties(hitbox, so =>
            {
                so.FindProperty("owner").objectReferenceValue = ownerHealth;
                so.FindProperty("damageMultiplier").floatValue = 1.5f;
                so.FindProperty("isWeakPoint").boolValue = true;
            });
            return root;
        }

        private static void FinishEnemy(GameObject root, EnemyAttack attack)
        {
            var brain = root.AddComponent<EnemyBrain>();
            SetProperties(brain, so => so.FindProperty("attack").objectReferenceValue = attack);
        }

        private static SpawnCard Card(string file, EnemyActor prefab, int cost, float weight, float minCoefficient)
        {
            var card = Asset<SpawnCard>($"{EnemiesFolder}/{file}.asset");
            card.prefab = prefab;
            card.cost = cost;
            card.weight = weight;
            card.minCoefficient = minCoefficient;
            EditorUtility.SetDirty(card);
            return card;
        }

        private static void ConfigureTransform(NetworkTransform netTransform)
        {
            netTransform.SyncRotAngleX = false;
            netTransform.SyncRotAngleZ = false;
            netTransform.SyncScaleX = netTransform.SyncScaleY = netTransform.SyncScaleZ = false;
        }

        // ---------------------------------------------------------------- Scene

        private static void BuildScene(GameObject playerPrefab, NetworkPrefabsList prefabList, List<SpawnCard> cards)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                Object.DestroyImmediate(mainCam.GetComponent<AudioListener>());
                mainCam.transform.SetPositionAndRotation(new Vector3(0f, 25f, -30f), Quaternion.Euler(40f, 0f, 0f));
            }

            BuildLevel();

            var spawns = new GameObject("PlayerSpawns");
            for (int i = 0; i < 4; i++)
            {
                var point = new GameObject($"Spawn_{i}");
                point.transform.SetParent(spawns.transform);
                point.transform.position = new Vector3((i - 1.5f) * 2.5f, 0.1f, -3f);
                point.AddComponent<PlayerSpawnPoint>();
            }

            // Run systems (in-scene NetworkObject)
            var systems = new GameObject("GameSystems");
            systems.AddComponent<NetworkObject>();
            systems.AddComponent<RunDifficulty>();
            systems.AddComponent<ProjectileNetwork>();
            systems.AddComponent<GoldRewarder>();
            var director = systems.AddComponent<CombatDirector>();
            SetProperties(director, so =>
            {
                var list = so.FindProperty("cards");
                list.arraySize = cards.Count;
                for (int i = 0; i < cards.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            });

            Shrine(new Vector3(8f, 0f, 4f));
            Shrine(new Vector3(-12f, 0f, 14f));

            var netGo = new GameObject("NetworkManager");
            var manager = netGo.AddComponent<NetworkManager>();
            var transport = netGo.AddComponent<UnityTransport>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = playerPrefab;
            if (!manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Contains(prefabList))
                manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            netGo.AddComponent<NetworkDebugPanel>();
            netGo.AddComponent<DebugHud>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
        }

        private static void BuildLevel()
        {
            var groundMat = Mat("M_Ground", new Color(0.35f, 0.4f, 0.3f));
            var rockMat = Mat("M_Rock", new Color(0.45f, 0.43f, 0.4f));
            var level = new GameObject("Level");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(level.transform);
            ground.transform.localScale = new Vector3(12f, 1f, 12f);
            ground.GetComponent<Renderer>().sharedMaterial = groundMat;

            var random = new System.Random(42);
            for (int i = 0; i < 18; i++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Rock_{i}";
                block.transform.SetParent(level.transform);
                float x = (float)(random.NextDouble() * 90 - 45);
                float z = (float)(random.NextDouble() * 90 - 45);
                if (Mathf.Abs(x) < 8f && Mathf.Abs(z) < 8f)
                    x += 16f;
                var size = new Vector3(2f + (float)random.NextDouble() * 5f, 1f + (float)random.NextDouble() * 4f, 2f + (float)random.NextDouble() * 5f);
                block.transform.localScale = size;
                block.transform.position = new Vector3(x, size.y * 0.5f, z);
                block.GetComponent<Renderer>().sharedMaterial = rockMat;
            }
            foreach (var t in level.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string navPath = Scenes + "/Prototype_NavMesh.asset";
                AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            }
        }

        private static void Shrine(Vector3 position)
        {
            var shrine = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shrine.name = "HealingShrine";
            shrine.transform.position = position + Vector3.up * 0.75f;
            shrine.transform.localScale = new Vector3(1f, 1.5f, 1f);
            shrine.GetComponent<Renderer>().sharedMaterial = Mat("M_Shrine", new Color(0.3f, 1f, 0.5f));
            shrine.AddComponent<NetworkObject>();
            var component = shrine.AddComponent<HealingShrine>();
            SetProperties(component, so =>
            {
                so.FindProperty("displayName").stringValue = "Autel de soin";
                so.FindProperty("baseCost").uintValue = 15;
                so.FindProperty("maxUses").intValue = 3;
                so.FindProperty("costMultiplierPerUse").floatValue = 1.5f;
            });
        }

        // ---------------------------------------------------------------- Helpers

        private static ProjectileDefinition ProjectileAsset(string name, Color color, float size, GameObject impact)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = name;
            Object.DestroyImmediate(root.GetComponent<Collider>());
            root.transform.localScale = Vector3.one * size;
            root.GetComponent<Renderer>().sharedMaterial = Mat("M_" + name, color, unlit: true);
            root.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = 0.15f;
            trail.widthMultiplier = size * 0.8f;
            trail.sharedMaterial = LineMat();
            trail.startColor = color;
            trail.endColor = new Color(color.r, color.g, color.b, 0f);
            root.AddComponent<Projectile>();
            var prefab = SavePrefab(root, $"{ProjectilesFolder}/{name}.prefab");

            var def = Asset<ProjectileDefinition>($"{ProjectilesFolder}/{name}.asset");
            def.visualPrefab = prefab;
            def.impactVfx = impact;
            return def;
        }

        private static GameObject EffectPrefab(string name, PrimitiveType type, Vector3 meshScale, Vector3 meshOffset, Color color, float lifetime)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = meshOffset;
            mesh.transform.localScale = meshScale;
            var renderer = mesh.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat("M_" + name, color, unlit: true);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var timed = root.AddComponent<TimedEffect>();
            SetProperties(timed, so => so.FindProperty("lifetime").floatValue = lifetime);
            return SavePrefab(root, $"{VfxFolder}/{name}.prefab");
        }

        private static GameObject LinePrefab(string name, Color color, float width, float lifetime)
        {
            var root = new GameObject(name);
            var line = root.AddComponent<LineRenderer>();
            line.widthMultiplier = width;
            line.sharedMaterial = LineMat();
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            root.AddComponent<LineEffect>();
            var timed = root.AddComponent<TimedEffect>();
            SetProperties(timed, so => so.FindProperty("lifetime").floatValue = lifetime);
            return SavePrefab(root, $"{VfxFolder}/{name}.prefab");
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

        private static Material Mat(string name, Color color, bool unlit = false)
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = unlit
                ? Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color")
                : Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            return MaterialAsset(name, shader, color);
        }

        private static Material LineMat() => MaterialAsset("M_Line", Shader.Find("Sprites/Default"), Color.white);

        private static Material MaterialAsset(string name, Shader shader, Color color)
        {
            string path = $"{Materials}/{name}.mat";
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
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>Loads an asset of type T at path, replacing anything of another type.</summary>
        private static T Asset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
                AssetDatabase.DeleteAsset(path);
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
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == path))
                return;
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
