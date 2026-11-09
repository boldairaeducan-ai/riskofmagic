using System.Collections.Generic;
using System.IO;
using RiskOfMagic.Animation;
using RiskOfMagic.Audio;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Core;
using RiskOfMagic.Economy;
using RiskOfMagic.Enemies;
using RiskOfMagic.Enemies.Elites;
using RiskOfMagic.Equipment;
using RiskOfMagic.Feedback;
using RiskOfMagic.Flow;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
using RiskOfMagic.Items.Types;
using RiskOfMagic.Maps;
using RiskOfMagic.Meta;
using RiskOfMagic.Networking;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Run.MapEvents;
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
        private const string ItemsFolder = Configs + "/Items";
        private const string EquipmentFolder = Configs + "/Equipment";
        private const string Prefabs = Root + "/Prefabs";
        private const string VfxFolder = Prefabs + "/Vfx";
        private const string Materials = Root + "/Materials";
        private const string Scenes = Root + "/Scenes";
        private const string ResourcesFolder = Root + "/Resources";
        private const string ScenePath = Scenes + "/Prototype.unity";

        private sealed class VfxSet
        {
            public GameObject Impact, FrostBurst, Area, Swing, Lightning, Blink, Explosion, FireBurst, Telegraph, ArcaneBurst, TimePulse, PoisonCloud, PoisonBurst, CursePulse, RallyPulse, Shockwave, DashBurst, ShieldPulse, GroundWave, VoidBeam, FirePatch, AuraBurning, AuraBlizzard, AuraStorm, AuraSpectral;
        }

        private sealed class ProjectileSet
        {
            public ProjectileDefinition Arrow, PiercingArrow, FireBolt, FrostOrb, EnemyBolt, ArcaneMissile, Firebomb, ArcaneBolt, PoisonDart, SpiderSpit, Boulder, ChaosBolt, Meteor,
                FireArrow, FrostArrow, ExplosiveArrow, Axe, ArcaneShard, ArcaneFrostOrb,
                HolyHammer, RadiantWave, ThrowingDagger;
            public DotEffect Poison;
            public List<ProjectileDefinition> All => new() { Arrow, PiercingArrow, FireBolt, FrostOrb, EnemyBolt, ArcaneMissile, Firebomb, ArcaneBolt, PoisonDart, SpiderSpit, Boulder, ChaosBolt, Meteor,
                FireArrow, FrostArrow, ExplosiveArrow, Axe, ArcaneShard, ArcaneFrostOrb,
                HolyHammer, RadiantWave, ThrowingDagger };
        }

        [MenuItem("Tools/Risk of Magic/Build Prototype Scene")]
        public static void Build()
        {
            // With compile errors, existing configs can't be loaded as their type: Asset<T> would replace them with
            // empty ones (items named "Item" with no effect). Never build in that state.
            if (EditorApplication.isCompiling || EditorUtility.scriptCompilationFailed)
            {
                EditorUtility.DisplayDialog("Build Prototype Scene",
                    "Des scripts ne compilent pas (ou sont en cours de compilation). Corrige les erreurs de la console puis relance.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !HudBuilder.EnsureTextMeshPro())
                return;

            foreach (var folder in new[] { ChallengesFolder, SkillsFolder, CharactersFolder, ProjectilesFolder, EnemiesFolder, ItemsFolder, EquipmentFolder, VfxFolder, Materials, Scenes, ResourcesFolder })
                Directory.CreateDirectory(folder);
            AssetDatabase.Refresh();

            touchedAssets.Clear();
            PostProcessBuilder.EnsureSetup();
            HitFeedbackBuilder.Ensure();
            var vfx = BuildVfx();
            var projectiles = BuildProjectiles(vfx);
            var items = BuildItems(projectiles, vfx);
            var characters = BuildCharacters(projectiles, vfx);
            var equipment = BuildEquipment(vfx);
            var pickup = BuildPickupPrefab();

            var catalog = Asset<GameCatalog>(ResourcesFolder + "/GameCatalog.asset");
            var replicatedEffects = new List<GameObject>
            {
                vfx.Explosion, vfx.Lightning, vfx.Telegraph, vfx.PoisonBurst, vfx.CursePulse, vfx.RallyPulse, vfx.Shockwave, vfx.FireBurst, vfx.GroundWave, vfx.FirePatch, vfx.Blink, vfx.VoidBeam, vfx.PoisonCloud, vfx.ArcaneBurst, vfx.FrostBurst
            };
            catalog.EditorSet(characters, projectiles.All, items, replicatedEffects, equipment, pickup);
            catalog.EditorSetMapRotation(MapTools.LoadOrCreateRotation());
            catalog.EditorSetElites(BuildElites(vfx));
            EditorUtility.SetDirty(catalog);

            var smallChest = BuildChestPrefab<ChestInteractable>("Chest_Small", "Petit coffre", 25, 1f, new RarityWeights(70f, 24f, 5f, 1f),
                new Color(0.45f, 0.3f, 0.15f), pickup);
            var largeChest = BuildChestPrefab<ChestInteractable>("Chest_Large", "Grand coffre", 50, 1.4f, new RarityWeights(0f, 60f, 32f, 8f),
                new Color(0.85f, 0.65f, 0.2f), pickup);
            var choiceChest = BuildChestPrefab<ChoiceChest>("Chest_Choice", "Coffre du choix", 40, 1.2f, new RarityWeights(70f, 24f, 5f, 1f),
                new Color(0.45f, 0.25f, 0.6f), pickup);
            var barrel = BuildEquipmentBarrelPrefab();
            var merchant = BuildMerchantPrefab();

            var playerPrefab = BuildPlayerPrefab();
            var ghoul = BuildGhoul();
            var wolf = BuildWolf();
            var assassin = BuildAssassin();
            var ogre = BuildOgre();
            var pyromancer = BuildPyromancer(projectiles.Firebomb, vfx.Telegraph);
            var skeleton = BuildSkeletonArcher(projectiles.EnemyBolt);

            string listPath = Configs + "/NetworkPrefabs.asset";
            AssetDatabase.DeleteAsset(listPath);
            var prefabList = Asset<NetworkPrefabsList>(listPath);
            prefabList.Add(new NetworkPrefab { Prefab = playerPrefab });
            var lobbyPrefab = MainMenuBuilder.BuildLobbyPrefab();
            prefabList.Add(new NetworkPrefab { Prefab = lobbyPrefab });
            foreach (var enemy in new[] { ghoul, wolf, assassin, ogre, pyromancer, skeleton })
                prefabList.Add(new NetworkPrefab { Prefab = enemy.gameObject });
            var colossus = BuildBoss(projectiles.Boulder, vfx);
            prefabList.Add(new NetworkPrefab { Prefab = colossus.gameObject });
            var spiderling = BuildSpiderling();
            prefabList.Add(new NetworkPrefab { Prefab = spiderling.gameObject });
            var spiderlingCard = Card("SpawnCard_Spiderling", spiderling, 4, 0f, 0f); // summoned only (weight 0, not in the director)
            var burn = AssetDatabase.LoadAssetAtPath<DotEffect>(ItemsFolder + "/Dot_Burn.asset"); // built with the items
            var drake = BuildEmberDrake(projectiles.Firebomb, vfx, burn);
            prefabList.Add(new NetworkPrefab { Prefab = drake.gameObject });
            var queen = BuildSpiderQueen(projectiles.SpiderSpit, spiderlingCard, vfx);
            prefabList.Add(new NetworkPrefab { Prefab = queen.gameObject });
            prefabList.Add(new NetworkPrefab { Prefab = pickup.gameObject });
            prefabList.Add(new NetworkPrefab { Prefab = smallChest });
            prefabList.Add(new NetworkPrefab { Prefab = largeChest });
            prefabList.Add(new NetworkPrefab { Prefab = choiceChest });
            prefabList.Add(new NetworkPrefab { Prefab = barrel });
            prefabList.Add(new NetworkPrefab { Prefab = merchant });
            var shrine = BuildShrinePrefab();
            prefabList.Add(new NetworkPrefab { Prefab = shrine });
            EditorUtility.SetDirty(prefabList);

            var cards = new List<SpawnCard>
            {
                // Min "progress" (difficulty without the player factor): ~1.2 ≈ 1.5 min, ~1.6 ≈ 4 min in Normal.
                // Ghouls only at first, then variety unlocks over time.
                Card("SpawnCard_Ghoul", ghoul, 10, 1.5f, 0f),
                Card("SpawnCard_Wolf", wolf, 12, 0.35f, 1.2f),
                Card("SpawnCard_Pyromancer", pyromancer, 16, 0.45f, 1.3f),
                Card("SpawnCard_Assassin", assassin, 14, 0.4f, 1.45f),
                Card("SpawnCard_Ogre", ogre, 28, 0.35f, 1.6f),
                // Projectiles are hard to read in first person: archers stay rare and late.
                Card("SpawnCard_SkeletonArcher", skeleton, 20, 0.15f, 1.9f)
            };

            // Teleporter: weighted pick among unlocked cards, never the previous boss; +1 boss from stage 4.
            var bossCards = new List<SpawnCard>
            {
                Card("SpawnCard_Boss_BoneColossus", colossus, 1, 1f, 0f),
                Card("SpawnCard_Boss_SpiderQueen", queen, 1, 1f, 0f),
                Card("SpawnCard_Boss_EmberDrake", drake, 1, 1f, 0f, minStage: 2)
            };
            // Final stage (every StageManager.stagesPerLoop stages): one avatar, then victory seal or loop.
            var avatar = BuildChaosAvatar(projectiles, vfx, new[]
            {
                cards.Find(c => c.name == "SpawnCard_Ghoul"), cards.Find(c => c.name == "SpawnCard_Wolf"),
                cards.Find(c => c.name == "SpawnCard_Pyromancer")
            });
            prefabList.Add(new NetworkPrefab { Prefab = avatar.gameObject });
            var avatarCard = Card("SpawnCard_Boss_ChaosAvatar", avatar, 1, 1f, 0f);
            SetProperties(avatar.GetComponent<ChaosAvatar>(), so => so.FindProperty("illusionCard").objectReferenceValue = avatarCard);
            EditorUtility.SetDirty(avatar.GetComponent<ChaosAvatar>());
            var seal = BuildRunEndSealPrefab();
            prefabList.Add(new NetworkPrefab { Prefab = seal });

            // Meta progression: challenges (unlocks) and bestiary order.
            var bestiary = new List<EnemyDefinition>();
            foreach (var enemy in new[] { ghoul, wolf, assassin, ogre, pyromancer, skeleton, spiderling, colossus, queen, drake, avatar })
                bestiary.Add(enemy.Definition);
            catalog.EditorSetMeta(BuildChallenges(characters, items), bestiary);
            EditorUtility.SetDirty(catalog);

            var teleporter = BuildTeleporterPrefab(bossCards, new List<SpawnCard> { avatarCard }, seal);
            prefabList.Add(new NetworkPrefab { Prefab = teleporter });

            // Map events: 0..2 per stage, every value in Resources/MapEventsConfig.asset (never overwritten).
            LoadOrCreateMapEventsConfig();
            var tomb = BuildPurificationTombPrefab();
            var altar = BuildChallengeAltarPrefab();
            var cursedChest = BuildCursedChestPrefab();
            foreach (var mapEventPrefab in new[] { tomb, altar, cursedChest })
                prefabList.Add(new NetworkPrefab { Prefab = mapEventPrefab });
            var mapEvents = new[]
            {
                (MapEventType.PurificationTomb, tomb), (MapEventType.ChallengeAltar, altar), (MapEventType.CursedChest, cursedChest)
            };
            var gameSystems = BuildGameSystemsPrefab(cards, new[]
            {
                (smallChest, 12), (largeChest, 3), (choiceChest, 3), (barrel, 3), (shrine, 2), (merchant, 1)
            }, teleporter, mapEvents);
            prefabList.Add(new NetworkPrefab { Prefab = gameSystems });
            EditorUtility.SetDirty(prefabList);

            DeleteObsoleteAssets(ItemsFolder);
            ArtLibraryBuilder.Save();
            var hud = HudBuilder.GetOrBuild();
            AssetDatabase.SaveAssets();
            AudioSlots.CreateAll(); // re-links the persistent sound slots to the regenerated prefabs
            BuildScene(playerPrefab, prefabList, gameSystems, hud);
            MainMenuBuilder.BuildScene(prefabList, lobbyPrefab);
            AddSceneToBuildSettings(MainMenuBuilder.ScenePath); // first scene of the build
            EditorSceneManager.OpenScene(ScenePath);
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
                Lightning = LinePrefab("VFX_Lightning", new Color(0.6f, 0.85f, 1f), 0.15f, 0.2f),
                Explosion = EffectPrefab("VFX_Explosion", PrimitiveType.Sphere, Vector3.one * 2f, Vector3.zero, new Color(1f, 0.5f, 0.15f), 0.2f),
                FireBurst = EffectPrefab("VFX_FireBurst", PrimitiveType.Sphere, Vector3.one * 6f, Vector3.zero, new Color(1f, 0.45f, 0.1f), 0.25f),
                // Ground marker of artillery impacts: lives for windup + flight.
                ArcaneBurst = EffectPrefab("VFX_ArcaneBurst", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.05f, 0f), new Color(0.75f, 0.4f, 1f), 0.3f),
                PoisonCloud = EffectPrefab("VFX_PoisonCloud", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.07f, 0f), new Color(0.45f, 0.85f, 0.25f), 0.45f),
                PoisonBurst = EffectPrefab("VFX_PoisonBurst", PrimitiveType.Sphere, Vector3.one * 2f, Vector3.zero, new Color(0.5f, 1f, 0.3f), 0.25f),
                // Ghost left behind by dashes (stretched along the dash direction).
                DashBurst = EffectPrefab("VFX_DashBurst", PrimitiveType.Cube, new Vector3(0.7f, 1.6f, 2.4f), new Vector3(0f, 0.9f, -1.2f), new Color(0.75f, 0.9f, 1f), 0.2f),
                ShieldPulse = EffectPrefab("VFX_ShieldPulse", PrimitiveType.Sphere, Vector3.one * 3f, new Vector3(0f, 0.9f, 0f), new Color(0.7f, 0.45f, 1f), 0.25f),
                CursePulse = EffectPrefab("VFX_CursePulse", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.09f, 0f), new Color(0.55f, 0.2f, 0.6f), 0.4f),
                RallyPulse = EffectPrefab("VFX_RallyPulse", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.1f, 0f), new Color(1f, 0.8f, 0.3f), 0.5f),
                Shockwave = EffectPrefab("VFX_Shockwave", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.11f, 0f), new Color(0.9f, 0.85f, 0.75f), 0.3f),
                TimePulse = EffectPrefab("VFX_TimePulse", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.08f, 0f), new Color(0.45f, 0.8f, 1f), 0.35f),
                Telegraph = EffectPrefab("VFX_Telegraph", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.06f, 0f), new Color(1f, 0.2f, 0.1f), 1.9f),
                GroundWave = RingPrefab("VFX_GroundWave", new Color(1f, 0.75f, 0.35f), 0.6f, ColossusWaveSpeed),
                VoidBeam = LinePrefab("VFX_VoidBeam", new Color(0.7f, 0.35f, 1f), 0.45f, 0.16f),
                // Burning elite trail: lifetime = BurningAffix.trailDuration, scaled by its radius.
                FirePatch = EffectPrefab("VFX_FirePatch", PrimitiveType.Cylinder, new Vector3(2f, 0.02f, 2f), new Vector3(0f, 0.04f, 0f), new Color(1f, 0.35f, 0.05f), 3f),
                // Elite auras (persistent, attached to the enemy, sizes in world units).
                AuraBurning = AuraPrefab("VFX_Aura_Burning", 1.2f, new Color(1f, 0.3f, 0.1f)),
                AuraBlizzard = AuraPrefab("VFX_Aura_Blizzard", 6f, new Color(0.75f, 0.9f, 1f)), // = BlizzardAffix.radius
                AuraStorm = AuraPrefab("VFX_Aura_Storm", 1.2f, new Color(1f, 0.9f, 0.2f)),
                AuraSpectral = AuraPrefab("VFX_Aura_Spectral", 1.2f, new Color(0.65f, 0.4f, 1f))
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
                EnemyBolt = ProjectileAsset("Projectile_EnemyBolt", new Color(0.4f, 1f, 0.3f), 0.3f, vfx.Impact),
                ArcaneMissile = ProjectileAsset("Projectile_ArcaneMissile", new Color(0.8f, 0.45f, 1f), 0.3f, vfx.Impact),
                Firebomb = ProjectileAsset("Projectile_Firebomb", new Color(1f, 0.4f, 0.1f), 0.6f, vfx.FireBurst),
                ArcaneBolt = ProjectileAsset("Projectile_ArcaneBolt", new Color(0.75f, 0.45f, 1f), 0.3f, vfx.Impact),
                PoisonDart = ProjectileAsset("Projectile_PoisonDart", new Color(0.45f, 0.9f, 0.2f), 0.15f, vfx.Impact),
                SpiderSpit = ProjectileAsset("Projectile_SpiderSpit", new Color(0.55f, 0.85f, 0.15f), 0.45f, vfx.PoisonBurst),
                Boulder = ProjectileAsset("Projectile_Boulder", new Color(0.45f, 0.4f, 0.35f), 1.1f, vfx.Explosion),
                ChaosBolt = ProjectileAsset("Projectile_ChaosBolt", new Color(0.85f, 0.2f, 0.6f), 0.4f, vfx.Impact),
                Meteor = ProjectileAsset("Projectile_Meteor", new Color(0.55f, 0.15f, 0.1f), 2.5f, vfx.FireBurst),
                // Alternative skills
                FireArrow = ProjectileAsset("Projectile_FireArrow", new Color(1f, 0.5f, 0.15f), 0.2f, vfx.Impact),
                FrostArrow = ProjectileAsset("Projectile_FrostArrow", new Color(0.6f, 0.9f, 1f), 0.25f, vfx.FrostBurst),
                ExplosiveArrow = ProjectileAsset("Projectile_ExplosiveArrow", new Color(1f, 0.7f, 0.3f), 0.25f, vfx.Explosion),
                Axe = ProjectileAsset("Projectile_Axe", new Color(0.7f, 0.7f, 0.75f), 0.5f, vfx.Impact),
                ArcaneShard = ProjectileAsset("Projectile_ArcaneShard", new Color(0.85f, 0.55f, 1f), 0.2f, vfx.Impact),
                ArcaneFrostOrb = ProjectileAsset("Projectile_ArcaneFrostOrb", new Color(0.55f, 0.85f, 1f), 0.7f, vfx.FrostBurst),
                // Paladin / Shadow
                HolyHammer = ProjectileAsset("Projectile_HolyHammer", new Color(1f, 0.9f, 0.5f), 0.55f, vfx.Impact),
                RadiantWave = ProjectileAsset("Projectile_RadiantWave", new Color(1f, 0.95f, 0.7f), 0.9f, vfx.Impact),
                ThrowingDagger = ProjectileAsset("Projectile_ThrowingDagger", new Color(0.6f, 0.6f, 0.7f), 0.15f, vfx.Impact)
            };

            Configure(set.Arrow, speed: 110f, gravity: 6f, lifetime: 2f, radius: 0.1f);
            Configure(set.PiercingArrow, speed: 160f, gravity: 0f, lifetime: 1.5f, radius: 0.25f, pierce: 10);
            Configure(set.FireBolt, speed: 70f, gravity: 0f, lifetime: 2f, radius: 0.2f, explosion: 1.5f);
            Configure(set.FrostOrb, speed: 40f, gravity: 0f, lifetime: 1.5f, radius: 0.3f, explosion: 3f, explodeOnExpire: true);
            Configure(set.EnemyBolt, speed: 35f, gravity: 0f, lifetime: 3f, radius: 0.25f);
            Configure(set.ArcaneMissile, speed: 50f, gravity: 0f, lifetime: 3f, radius: 0.3f);
            // Speed is overridden by the artillery attack to hit its target point.
            Configure(set.Firebomb, speed: 20f, gravity: 20f, lifetime: 4f, radius: 0.35f, explosion: 3f, explodeOnExpire: true);
            Configure(set.ArcaneBolt, speed: 30f, gravity: 0f, lifetime: 3f, radius: 0.25f);
            set.ArcaneBolt.homingTurnRate = 420f;
            EditorUtility.SetDirty(set.ArcaneBolt);
            set.ArcaneMissile.homingTurnRate = 300f;
            EditorUtility.SetDirty(set.ArcaneMissile);

            set.Poison = Asset<DotEffect>(ProjectilesFolder + "/Dot_Poison.asset");
            set.Poison.damageType = DamageType.Poison;
            set.Poison.tickDamageFraction = 0.25f;
            set.Poison.tickInterval = 0.5f;
            set.Poison.duration = 4f;
            set.Poison.maxStacks = 10;
            EditorUtility.SetDirty(set.Poison);

            Configure(set.PoisonDart, speed: 75f, gravity: 3f, lifetime: 2f, radius: 0.12f);
            Configure(set.SpiderSpit, speed: 28f, gravity: 4f, lifetime: 3f, radius: 0.3f, explosion: 2f);
            // Speed is overridden by the artillery attack.
            Configure(set.Boulder, speed: 20f, gravity: 20f, lifetime: 4f, radius: 0.5f, explosion: 3.5f, explodeOnExpire: true);
            Configure(set.ChaosBolt, speed: 22f, gravity: 0f, lifetime: 3f, radius: 0.3f);
            set.ChaosBolt.homingTurnRate = 90f;
            set.ChaosBolt.maxRange = 50f;
            // Speed is overridden by the meteor attack to land after its fall time.
            Configure(set.Meteor, speed: 25f, gravity: 0f, lifetime: 4f, radius: 1f, explosion: 5f, explodeOnExpire: true);
            Configure(set.FireArrow, speed: 100f, gravity: 6f, lifetime: 2f, radius: 0.12f);
            Configure(set.FrostArrow, speed: 120f, gravity: 0f, lifetime: 1.5f, radius: 0.25f, pierce: 3);
            Configure(set.ExplosiveArrow, speed: 80f, gravity: 4f, lifetime: 2f, radius: 0.2f, explosion: 4f);
            Configure(set.Axe, speed: 45f, gravity: 4f, lifetime: 2f, radius: 0.5f, pierce: 10);
            Configure(set.ArcaneShard, speed: 90f, gravity: 0f, lifetime: 1.5f, radius: 0.15f);
            Configure(set.ArcaneFrostOrb, speed: 18f, gravity: 0f, lifetime: 2.5f, radius: 0.35f, explosion: 4f, explodeOnExpire: true);
            set.FireArrow.maxRange = 45f;
            set.FrostArrow.maxRange = 50f;
            set.ExplosiveArrow.maxRange = 45f;
            set.Axe.maxRange = 45f;
            set.ArcaneShard.maxRange = 55f;
            set.ArcaneFrostOrb.maxRange = 45f;
            set.FrostArrow.freezeDuration = 1.5f;
            set.ArcaneFrostOrb.freezeDuration = 2f;
            Configure(set.HolyHammer, speed: 55f, gravity: 0f, lifetime: 1.5f, radius: 0.35f);
            Configure(set.RadiantWave, speed: 30f, gravity: 0f, lifetime: 0.4f, radius: 0.9f, pierce: 20);
            Configure(set.ThrowingDagger, speed: 85f, gravity: 2f, lifetime: 1.5f, radius: 0.12f);
            set.HolyHammer.maxRange = 50f;
            set.HolyHammer.freezeDuration = 1f;
            set.RadiantWave.maxRange = 8f;
            set.ThrowingDagger.maxRange = 45f;
            set.ThrowingDagger.backstabCritical = true;
            foreach (var alternative in new[] { set.FireArrow, set.FrostArrow, set.ExplosiveArrow, set.Axe, set.ArcaneShard, set.ArcaneFrostOrb,
                         set.HolyHammer, set.RadiantWave, set.ThrowingDagger })
                EditorUtility.SetDirty(alternative);

            // Strict max ranges (m) for ranged attacks.
            set.Arrow.maxRange = 45f;
            set.PiercingArrow.maxRange = 60f;
            set.ArcaneBolt.maxRange = 60f; // homing curves: a bit above the 45 m lock range
            set.ArcaneMissile.maxRange = 60f;
            set.EnemyBolt.maxRange = 40f;
            set.FireBolt.maxRange = 60f;
            set.FrostOrb.maxRange = 50f;
            set.PoisonDart.maxRange = 50f;
            set.SpiderSpit.maxRange = 45f;
            foreach (var projectile in set.All)
                EditorUtility.SetDirty(projectile);
            set.PoisonDart.dot = set.Poison;
            set.SpiderSpit.dot = set.Poison;
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
            p.homingTurnRate = 0f;
            p.maxRange = 0f;
            p.dot = null;
            p.freezeDuration = 0f;
            p.backstabCritical = false;
            EditorUtility.SetDirty(p);
        }

        // ---------------------------------------------------------------- Characters & skills

        private static List<CharacterDefinition> BuildCharacters(ProjectileSet p, VfxSet vfx)
        {
            // Archer
            var rapidShot = Skill<ProjectileSkill>("Archer_RapidShot", "Tir rapide", "Flèches en continu. 100% de dégâts.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.4f, attackSpeed: true);
            rapidShot.projectile = p.Arrow;
            rapidShot.damageCoefficient = 1.0f;

            var piercing = Skill<ProjectileSkill>("Archer_PiercingArrow", "Flèche perforante", "Charger pour transpercer les ennemis. 150-500%.",
                SkillActivationMode.Charge, cooldown: 6f, cast: 0.3f);
            piercing.projectile = p.PiercingArrow;
            piercing.maxChargeTime = 1.2f;
            piercing.minChargeCoefficient = 1.5f;
            piercing.maxChargeCoefficient = 5f;
            piercing.minChargeScale = 1f;
            piercing.maxChargeScale = 1.6f;

            var roll = Skill<DashSkill>("Archer_Roll", "Roulade", "Esquive rapide dans la direction de déplacement.",
                SkillActivationMode.Press, cooldown: 5f, cast: 0f);
            roll.direction = DashDirection.MoveInput;
            roll.speed = 28f;
            roll.duration = 0.22f;
            roll.canCastWhileBusy = true;
            roll.keepsSprint = true;
            roll.castVfx = vfx.DashBurst;
            roll.ownerFovKick = 10f;
            roll.ownerFovKickDuration = 0.3f;

            var rain = Skill<AreaSkill>("Archer_ArrowRain", "Pluie de flèches", "Zone ciblée frappée 6 fois. 60% par impact.",
                SkillActivationMode.Press, cooldown: 15f, cast: 0.4f);
            rain.center = AreaCenter.AimPoint;
            rain.maxRange = 50f;
            rain.radius = 5f;
            rain.damageCoefficient = 0.6f;
            rain.ticks = 6;
            rain.tickInterval = 0.3f;
            rain.initialDelay = 0.4f;
            rain.tickVfx = vfx.Area;

            // Warrior
            var combo = Skill<MeleeComboSkill>("Warrior_SwordCombo", "Combo d'épée", "Trois coups (100/100/200%), le dernier frappe fort.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.5f, attackSpeed: true);
            combo.swingVfx = vfx.Swing;
            combo.steps = new[]
            {
                new MeleeComboSkill.Step { damageCoefficient = 1f, radius = 1.6f, reach = 1.6f, hitDelay = 0.1f },
                new MeleeComboSkill.Step { damageCoefficient = 1f, radius = 1.6f, reach = 1.6f, hitDelay = 0.1f },
                new MeleeComboSkill.Step { damageCoefficient = 2f, radius = 2.2f, reach = 1.8f, hitDelay = 0.15f, lunge = 6f }
            };

            var whirlwind = Skill<AreaSkill>("Warrior_Whirlwind", "Tourbillon", "3 coups circulaires. 90% chacun.",
                SkillActivationMode.Press, cooldown: 7f, cast: 0.6f);
            whirlwind.center = AreaCenter.Caster;
            whirlwind.radius = 4f;
            whirlwind.ticks = 3;
            whirlwind.tickInterval = 0.2f;
            whirlwind.damageCoefficient = 0.9f;
            whirlwind.tickVfx = vfx.Area;
            whirlwind.ownerCameraTrauma = 0.1f;

            var charge = Skill<DashSkill>("Warrior_Charge", "Charge", "Fonce en avant, 200% aux ennemis percutés.",
                SkillActivationMode.Press, cooldown: 8f, cast: 0.3f);
            charge.direction = DashDirection.AimFlat;
            charge.speed = 30f;
            charge.duration = 0.3f;
            charge.damageCoefficient = 2f;
            charge.damageRadius = 1.8f;
            charge.castVfx = vfx.DashBurst;
            charge.ownerFovKick = 12f;
            charge.ownerFovKickDuration = 0.4f;
            charge.ownerCameraTrauma = 0.15f;

            var slam = Skill<AreaSkill>("Warrior_SeismicSlam", "Frappe sismique", "Bond puis onde de choc. 400%.",
                SkillActivationMode.Press, cooldown: 15f, cast: 0.5f);
            slam.center = AreaCenter.Caster;
            slam.radius = 7f;
            slam.ticks = 1;
            slam.initialDelay = 0.35f;
            slam.damageCoefficient = 4f;
            slam.hitOncePerTarget = true;
            slam.casterUpwardVelocity = 6f;
            slam.tickVfx = vfx.Area;
            slam.ownerCameraTrauma = 0.45f;

            // Arcanist
            var volley = Skill<HomingVolleySkill>("Arcanist_ArcaneVolley", "Projectile magique",
                "Charger pour acquérir jusqu'à 6 cibles (un ennemi peut être ciblé plusieurs fois). 110% par projectile.",
                SkillActivationMode.Charge, cooldown: 0f, cast: 0.25f, attackSpeed: true);
            volley.projectile = p.ArcaneBolt;
            volley.maxTargets = 6;
            volley.maxChargeTime = 1.5f;
            volley.damageCoefficient = 1.1f;

            var burst = Skill<AreaSkill>("Arcanist_ArcaneBurst", "Explosion arcanique", "Explosion autour de soi : 200%, repousse les ennemis.",
                SkillActivationMode.Press, cooldown: 6f, cast: 0.3f);
            burst.center = AreaCenter.Caster;
            burst.radius = 6f;
            burst.ticks = 1;
            burst.damageCoefficient = 2f;
            burst.damageType = DamageType.Arcane;
            burst.knockbackDistance = 7f;
            burst.tickVfx = vfx.ArcaneBurst;
            burst.ownerCameraTrauma = 0.25f;

            var shield = Skill<ShieldSkill>("Arcanist_ArcaneShield", "Bouclier arcanique", "Bloque 95% des dégâts pendant 3 s.",
                SkillActivationMode.Press, cooldown: 10f, cast: 0f);
            shield.damageReduction = 0.95f;
            shield.duration = 3f;
            shield.canCastWhileBusy = true;
            shield.keepsSprint = true;
            shield.castVfx = vfx.ShieldPulse;
            shield.castVfxAnchor = VfxAnchor.Caster;
            shield.ownerFovKick = -4f;
            shield.ownerFovKickDuration = 0.2f;

            var timeLock = Skill<ConeControlSkill>("Arcanist_TimeLock", "Blocage temporel", "Fige les ennemis devant soi pendant 4 s.",
                SkillActivationMode.Press, cooldown: 14f, cast: 0.4f);
            timeLock.range = 20f;
            timeLock.coneAngle = 35f;
            timeLock.freezeDuration = 4f;
            timeLock.castVfx = vfx.TimePulse;
            timeLock.castVfxAnchor = VfxAnchor.Caster;
            timeLock.castVfxScale = 20f;
            timeLock.ownerCameraTrauma = 0.15f;

            // Druid (poison, damage over time)
            var claws = Skill<MeleeComboSkill>("Druid_ToxicClaws", "Griffes toxiques",
                "Combo de 3 coups de griffes (80/80/140%), chacun empoisonne (8 ticks de 25%, cumulable 10 fois).",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.35f, attackSpeed: true);
            claws.damageType = DamageType.Poison;
            claws.dot = p.Poison;
            claws.swingVfx = vfx.Swing;
            claws.comboResetTime = 0.8f;
            claws.steps = new[]
            {
                new MeleeComboSkill.Step { damageCoefficient = 0.8f, radius = 1.5f, reach = 1.5f, hitDelay = 0.08f },
                new MeleeComboSkill.Step { damageCoefficient = 0.8f, radius = 1.5f, reach = 1.5f, hitDelay = 0.08f },
                new MeleeComboSkill.Step { damageCoefficient = 1.4f, radius = 1.9f, reach = 1.7f, hitDelay = 0.12f, lunge = 4f }
            };

            var cloud = Skill<AreaSkill>("Druid_ToxicCloud", "Nuage toxique", "Nuage ciblé pendant 5 s : 30% par tick + poison.",
                SkillActivationMode.Press, cooldown: 8f, cast: 0.3f);
            cloud.center = AreaCenter.AimPoint;
            cloud.maxRange = 35f;
            cloud.radius = 5f;
            cloud.ticks = 10;
            cloud.tickInterval = 0.5f;
            cloud.damageCoefficient = 0.3f;
            cloud.damageType = DamageType.Poison;
            cloud.dot = p.Poison;
            cloud.tickVfx = vfx.PoisonCloud;

            var leap = Skill<DashSkill>("Druid_WildLeap", "Bond sylvestre", "Bond en avant, empoisonne les ennemis traversés.",
                SkillActivationMode.Press, cooldown: 6f, cast: 0.2f);
            leap.direction = DashDirection.AimFlat;
            leap.speed = 22f;
            leap.duration = 0.35f;
            leap.ignoreGravity = false;
            leap.upwardVelocity = 7f;
            leap.damageCoefficient = 1f;
            leap.damageRadius = 2f;
            leap.damageType = DamageType.Poison;
            leap.dot = p.Poison;
            leap.keepsSprint = true;
            leap.castVfx = vfx.DashBurst;
            leap.ownerFovKick = 10f;
            leap.ownerFovKickDuration = 0.4f;

            var bloom = Skill<DetonateDotsSkill>("Druid_FatalBloom", "Floraison fatale",
                "Fait éclater tous les poisons à 25 m : 150% des dégâts de poison restants, instantanément.",
                SkillActivationMode.Press, cooldown: 10f, cast: 0.3f);
            bloom.dotType = DamageType.Poison;
            bloom.radius = 25f;
            bloom.damageMultiplier = 1.5f;
            bloom.burstVfx = vfx.PoisonBurst;

            foreach (var skill in new SkillDefinition[] { rapidShot, piercing, roll, rain, combo, whirlwind, charge, slam, volley, burst, shield, timeLock, claws, cloud, leap, bloom })
                EditorUtility.SetDirty(skill);

            var characters = new List<CharacterDefinition>
            {
                Character("Character_Archer", "Archer", "Agile, double saut, dégâts à distance.", new Color(0.3f, 0.75f, 0.35f),
                    80f, Movement("Movement_Archer", 7.5f, 2), rapidShot, piercing, roll, rain),
                Character("Character_Warrior", "Guerrier", "Robuste, combat au corps à corps.", new Color(0.8f, 0.3f, 0.25f),
                    130f, Movement("Movement_Warrior", 7f, 1), combo, whirlwind, charge, slam),
                Character("Character_Arcanist", "Arcaniste", "Projectiles chercheurs, contrôle et protection.", new Color(0.6f, 0.35f, 0.95f),
                    90f, Movement("Movement_Arcanist", 7f, 1), volley, burst, shield, timeLock),
                Character("Character_Druid", "Druide", "Corps à corps empoisonné, poisons cumulables.", new Color(0.25f, 0.55f, 0.2f),
                    100f, Movement("Movement_Druid", 7.2f, 1), claws, cloud, leap, bloom)
            };
            BuildPaladinAndShadow(characters, p, vfx);
            BuildAlternativeSkills(characters, p, vfx);
            return characters;
        }

        /// <summary>Paladin (frontline support, holy damage) and Ombre (stealth assassin, backstabs).</summary>
        private static void BuildPaladinAndShadow(List<CharacterDefinition> characters, ProjectileSet p, VfxSet vfx)
        {
            // Paladin
            var hammer = Skill<MeleeComboSkill>("Paladin_BlessedHammer", "Marteau béni",
                "2 coups (120% / 180%) ; chaque coup qui touche soigne de 3 PV les alliés à 10 m.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.6f, attackSpeed: true);
            hammer.damageType = DamageType.Holy;
            hammer.swingVfx = vfx.Swing;
            hammer.healAlliesOnHit = 3f;
            hammer.healRadius = 10f;
            hammer.comboResetTime = 1.1f;
            hammer.steps = new[]
            {
                new MeleeComboSkill.Step { damageCoefficient = 1.2f, radius = 2f, reach = 1.8f, hitDelay = 0.15f },
                new MeleeComboSkill.Step { damageCoefficient = 1.8f, radius = 2.3f, reach = 1.9f, hitDelay = 0.2f, lunge = 3f }
            };

            var judgment = Skill<ProjectileSkill>("Paladin_Judgment", "Jugement", "Marteau de lumière : 300%, étourdit 1 s.",
                SkillActivationMode.Press, cooldown: 6f, cast: 0.35f);
            judgment.projectile = p.HolyHammer;
            judgment.damageType = DamageType.Holy;
            judgment.damageCoefficient = 3f;

            var aegis = Skill<AegisSkill>("Paladin_Aegis", "Égide", "Vous et les alliés à 10 m : -50% de dégâts subis pendant 4 s.",
                SkillActivationMode.Press, cooldown: 12f, cast: 0.2f);
            aegis.radius = 10f;
            aegis.damageReduction = 0.5f;
            aegis.duration = 4f;
            aegis.pulseVfx = vfx.RallyPulse;
            aegis.canCastWhileBusy = true;

            var consecration = Skill<AreaSkill>("Paladin_Consecration", "Sol consacré",
                "Zone de 8 m pendant 6 s : alliés soignés de 5% PV/s, ennemis 60% de dégâts/s.",
                SkillActivationMode.Press, cooldown: 18f, cast: 0.4f);
            consecration.center = AreaCenter.Caster;
            consecration.followCaster = false;
            consecration.radius = 8f;
            consecration.ticks = 12;
            consecration.tickInterval = 0.5f;
            consecration.damageCoefficient = 0.3f;
            consecration.damageType = DamageType.Holy;
            consecration.allyHealPerTick = 0.025f;
            consecration.tickVfx = vfx.RallyPulse;

            // Shadow
            var daggers = Skill<MeleeComboSkill>("Shadow_TwinDaggers", "Dagues jumelles",
                "3 coups rapides (70/70/110%). Critique garanti dans le dos.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.28f, attackSpeed: true);
            daggers.swingVfx = vfx.Swing;
            daggers.backstabCritical = true;
            daggers.comboResetTime = 0.7f;
            daggers.steps = new[]
            {
                new MeleeComboSkill.Step { damageCoefficient = 0.7f, radius = 1.3f, reach = 1.4f, hitDelay = 0.05f },
                new MeleeComboSkill.Step { damageCoefficient = 0.7f, radius = 1.3f, reach = 1.4f, hitDelay = 0.05f },
                new MeleeComboSkill.Step { damageCoefficient = 1.1f, radius = 1.5f, reach = 1.5f, hitDelay = 0.08f, lunge = 4f }
            };

            var throwing = Skill<ProjectileSkill>("Shadow_ThrowingDaggers", "Dagues de lancer", "3 dagues en éventail : 3 × 100%. Critique garanti dans le dos.",
                SkillActivationMode.Press, cooldown: 4f, cast: 0.25f);
            throwing.projectile = p.ThrowingDagger;
            throwing.damageCoefficient = 1f;
            throwing.count = 3;
            throwing.spreadAngle = 14f;

            var veil = Skill<StealthSkill>("Shadow_ShadowVeil", "Voile d'ombre",
                "Invisible 3 s : les ennemis vous perdent de vue, +30% de vitesse. La première attaque inflige +100%.",
                SkillActivationMode.Press, cooldown: 10f, cast: 0f);
            veil.duration = 3f;
            veil.canCastWhileBusy = true;
            veil.keepsSprint = true;
            veil.castVfx = vfx.Blink;

            var mark = Skill<MarkSkill>("Shadow_DeathMark", "Marque de mort",
                "La cible visée subit +50% de dégâts de toutes sources pendant 6 s. Si elle meurt, la recharge est réinitialisée.",
                SkillActivationMode.Press, cooldown: 12f, cast: 0.2f);
            mark.duration = 6f;
            mark.markVfx = vfx.CursePulse;

            foreach (var skill in new SkillDefinition[] { hammer, judgment, aegis, consecration, daggers, throwing, veil, mark })
                EditorUtility.SetDirty(skill);

            characters.Add(Character("Character_Paladin", "Paladin", "Soutien de première ligne : soins, protection, dégâts sacrés.",
                new Color(0.95f, 0.85f, 0.45f), 140f, Movement("Movement_Paladin", 6.6f, 1), hammer, judgment, aegis, consecration));
            characters.Add(Character("Character_Shadow", "Ombre", "Assassin furtif : invisibilité, critiques dans le dos, marque.",
                new Color(0.3f, 0.25f, 0.4f), 85f, Movement("Movement_Shadow", 8f, 2), daggers, throwing, veil, mark));
        }

        /// <summary>One alternative per slot and character (locked by challenges, chosen in the lobby).</summary>
        private static void BuildAlternativeSkills(List<CharacterDefinition> characters, ProjectileSet p, VfxSet vfx)
        {
            // Same values as the item burn (BuildItems): the asset is shared.
            var burn = Dot("Dot_Burn", DamageType.Fire, tickFraction: 0.25f, interval: 0.5f, duration: 3f, maxStacks: 10);
            p.FireArrow.dot = burn;
            EditorUtility.SetDirty(p.FireArrow);

            // Archer
            var fireArrows = Skill<ProjectileSkill>("Archer_FireArrows", "Flèches enflammées", "Tirs plus lents : 160% + brûlure.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.65f, attackSpeed: true);
            fireArrows.projectile = p.FireArrow;
            fireArrows.damageType = DamageType.Fire;
            fireArrows.damageCoefficient = 1.6f;

            var frostArrow = Skill<ProjectileSkill>("Archer_FrostArrow", "Flèche de givre", "Charger : 200-400%, gèle les ennemis touchés 1,5 s.",
                SkillActivationMode.Charge, cooldown: 7f, cast: 0.3f);
            frostArrow.projectile = p.FrostArrow;
            frostArrow.damageType = DamageType.Frost;
            frostArrow.maxChargeTime = 1.2f;
            frostArrow.minChargeCoefficient = 2f;
            frostArrow.maxChargeCoefficient = 4f;
            frostArrow.maxChargeScale = 1.4f;

            var backHop = Skill<DashSkill>("Archer_BackHop", "Bond arrière", "Saut de 12 m vers l'arrière, repousse les ennemis proches.",
                SkillActivationMode.Press, cooldown: 6f, cast: 0f);
            backHop.direction = DashDirection.BackwardFlat;
            backHop.speed = 30f;
            backHop.duration = 0.4f;
            backHop.ignoreGravity = false;
            backHop.upwardVelocity = 6f;
            backHop.startKnockbackRadius = 4f;
            backHop.startKnockbackDistance = 6f;
            backHop.canCastWhileBusy = true;
            backHop.keepsSprint = true;
            backHop.castVfx = vfx.DashBurst;
            backHop.ownerFovKick = 8f;

            var explosiveVolley = Skill<ProjectileSkill>("Archer_ExplosiveVolley", "Volée explosive", "3 flèches qui explosent : 3 × 250% dans 4 m.",
                SkillActivationMode.Press, cooldown: 12f, cast: 0.4f);
            explosiveVolley.projectile = p.ExplosiveArrow;
            explosiveVolley.damageType = DamageType.Fire;
            explosiveVolley.damageCoefficient = 2.5f;
            explosiveVolley.count = 3;
            explosiveVolley.spreadAngle = 12f;

            // Warrior
            var hammer = Skill<MeleeComboSkill>("Warrior_WarHammer", "Marteau de guerre", "2 coups lents : 250% en zone (3 m), repousse.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.9f, attackSpeed: true);
            hammer.swingVfx = vfx.Swing;
            hammer.knockbackDistance = 3f;
            hammer.comboResetTime = 1.4f;
            hammer.steps = new[]
            {
                new MeleeComboSkill.Step { damageCoefficient = 2.5f, radius = 3f, reach = 2f, hitDelay = 0.25f },
                new MeleeComboSkill.Step { damageCoefficient = 2.5f, radius = 3f, reach = 2f, hitDelay = 0.25f, lunge = 4f }
            };

            var axe = Skill<ProjectileSkill>("Warrior_ThrowingAxe", "Hache de jet", "Hache perforante : 300%.",
                SkillActivationMode.Press, cooldown: 6f, cast: 0.35f);
            axe.projectile = p.Axe;
            axe.damageCoefficient = 3f;

            var parry = Skill<ParrySkill>("Warrior_Parry", "Parade", "Bloque 100% des dégâts pendant 1 s, puis contre-attaque : 200% autour de soi.",
                SkillActivationMode.Press, cooldown: 8f, cast: 0.2f);
            parry.damageReduction = 1f;
            parry.duration = 1f;
            parry.radius = 4f;
            parry.damageCoefficient = 2f;
            parry.knockbackDistance = 3f;
            parry.counterVfx = vfx.Shockwave;
            parry.castVfx = vfx.ShieldPulse;
            parry.canCastWhileBusy = true;

            var warCry = Skill<WarCrySkill>("Warrior_WarCry", "Cri de guerre",
                "Alliés à 15 m : +40% vitesse d'attaque et de déplacement pendant 6 s, et rend 20 PV.",
                SkillActivationMode.Press, cooldown: 20f, cast: 0.3f);
            warCry.radius = 15f;
            warCry.buffDuration = 6f;
            warCry.heal = 20f;
            warCry.pulseVfx = vfx.RallyPulse;
            warCry.ownerCameraTrauma = 0.2f;

            // Arcanist
            var shards = Skill<ProjectileSkill>("Arcanist_ArcaneShards", "Éclats arcaniques", "Rafale de 3 projectiles rapides : 3 × 60%.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.45f, attackSpeed: true);
            shards.projectile = p.ArcaneShard;
            shards.damageType = DamageType.Arcane;
            shards.damageCoefficient = 0.6f;
            shards.burstCount = 3;
            shards.burstInterval = 0.08f;

            var frostOrb = Skill<ProjectileSkill>("Arcanist_FrostOrb", "Orbe de givre", "Orbe lent qui explose (4 m) : 300%, gèle 2 s.",
                SkillActivationMode.Press, cooldown: 7f, cast: 0.35f);
            frostOrb.projectile = p.ArcaneFrostOrb;
            frostOrb.damageType = DamageType.Frost;
            frostOrb.damageCoefficient = 3f;

            var transfer = Skill<BlinkSkill>("Arcanist_Transfer", "Transfert", "Téléportation de 15 m vers la visée, à travers les ennemis.",
                SkillActivationMode.Press, cooldown: 7f, cast: 0f);
            transfer.distance = 15f;
            transfer.passThroughCharacters = true;
            transfer.arrivalVfx = vfx.Blink;
            transfer.canCastWhileBusy = true;
            transfer.keepsSprint = true;

            var voidBeam = Skill<BeamSkill>("Arcanist_VoidBeam", "Rayon du néant",
                "Rayon de 3 s qui traverse les ennemis : 80% 8 fois par seconde. Chaque tick déclenche les objets (coef. 0,5). Ralentit de 50%.",
                SkillActivationMode.Press, cooldown: 12f, cast: 0.3f);
            voidBeam.range = 40f;
            voidBeam.duration = 3f;
            voidBeam.ticksPerSecond = 8f;
            voidBeam.damageCoefficientPerTick = 0.8f;
            voidBeam.procCoefficient = 0.5f;
            voidBeam.beamVfx = vfx.VoidBeam;
            voidBeam.impactVfx = vfx.ArcaneBurst;

            // Druid
            var thorns = Skill<ProjectileSkill>("Druid_Thorns", "Épines", "Projectiles à distance : 70% + poison.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.35f, attackSpeed: true);
            thorns.projectile = p.PoisonDart;
            thorns.damageType = DamageType.Poison;
            thorns.damageCoefficient = 0.7f;

            var brambles = Skill<ConeControlSkill>("Druid_Brambles", "Ronces", "Cône de ronces : immobilise 2 s et empoisonne.",
                SkillActivationMode.Press, cooldown: 8f, cast: 0.3f);
            brambles.range = 15f;
            brambles.coneAngle = 35f;
            brambles.freezeDuration = 2f;
            brambles.damageCoefficient = 0.5f;
            brambles.damageType = DamageType.Poison;
            brambles.dot = p.Poison;
            brambles.castVfx = vfx.PoisonCloud;
            brambles.castVfxAnchor = VfxAnchor.Caster;
            brambles.castVfxScale = 8f;

            var root = Skill<RootSkill>("Druid_Rooting", "Enracinement", "Immobile 3 s : régénère 10% PV/s, nuage de poison autour de soi.",
                SkillActivationMode.Press, cooldown: 12f, cast: 0.2f);
            root.duration = 3f;
            root.healPerSecond = 0.1f;
            root.radius = 5f;
            root.damageCoefficientPerTick = 0.3f;
            root.dot = p.Poison;
            root.tickVfx = vfx.PoisonCloud;

            var epidemic = Skill<EpidemicSkill>("Druid_Epidemic", "Épidémie", "Propage les poisons de l'ennemi visé à tous les ennemis à 10 m.",
                SkillActivationMode.Press, cooldown: 10f, cast: 0.3f);
            epidemic.poison = p.Poison;
            epidemic.spreadRadius = 10f;
            epidemic.spreadVfx = vfx.PoisonBurst;

            foreach (var skill in new SkillDefinition[] { fireArrows, frostArrow, backHop, explosiveVolley, hammer, axe, parry, warCry, shards, frostOrb, transfer, voidBeam, thorns, brambles, root, epidemic })
                EditorUtility.SetDirty(skill);

            SetAlternatives(characters[0], fireArrows, frostArrow, backHop, explosiveVolley);
            SetAlternatives(characters[1], hammer, axe, parry, warCry);
            SetAlternatives(characters[2], shards, frostOrb, transfer, voidBeam);
            SetAlternatives(characters[3], thorns, brambles, root, epidemic);

            // Paladin
            var radiant = Skill<MeleeComboSkill>("Paladin_RadiantBlade", "Lame radieuse",
                "3 coups (90/90/150%) ; le 3e projette une onde de lumière perforante (8 m, 150%).",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.45f, attackSpeed: true);
            radiant.damageType = DamageType.Holy;
            radiant.swingVfx = vfx.Swing;
            radiant.comboResetTime = 0.9f;
            radiant.steps = new[]
            {
                new MeleeComboSkill.Step { damageCoefficient = 0.9f, radius = 1.7f, reach = 1.7f, hitDelay = 0.1f },
                new MeleeComboSkill.Step { damageCoefficient = 0.9f, radius = 1.7f, reach = 1.7f, hitDelay = 0.1f },
                new MeleeComboSkill.Step { damageCoefficient = 1.5f, radius = 2f, reach = 1.8f, hitDelay = 0.12f, projectile = p.RadiantWave, projectileCoefficient = 1.5f }
            };

            var shieldThrow = Skill<ChainLightningSkill>("Paladin_ShieldThrow", "Bouclier projeté", "Le bouclier ricoche sur 4 ennemis : 200% chacun.",
                SkillActivationMode.Press, cooldown: 6f, cast: 0.3f);
            shieldThrow.range = 35f;
            shieldThrow.maxBounces = 3;
            shieldThrow.bounceRadius = 10f;
            shieldThrow.damageType = DamageType.Holy;
            shieldThrow.damageCoefficient = 2f;
            shieldThrow.bounceFalloff = 1f;
            shieldThrow.boltVfx = vfx.Lightning;

            var crusade = Skill<DashSkill>("Paladin_CrusaderCharge", "Charge du croisé",
                "Ruée de 12 m : 200% et recul aux ennemis, soigne de 10% les alliés traversés.",
                SkillActivationMode.Press, cooldown: 8f, cast: 0.3f);
            crusade.direction = DashDirection.AimFlat;
            crusade.speed = 30f;
            crusade.duration = 0.4f;
            crusade.damageCoefficient = 2f;
            crusade.damageType = DamageType.Holy;
            crusade.damageRadius = 2f;
            crusade.contactKnockbackDistance = 4f;
            crusade.allyHealOnContact = 0.1f;
            crusade.castVfx = vfx.DashBurst;
            crusade.ownerFovKick = 10f;

            var intervention = Skill<DivineInterventionSkill>("Paladin_DivineIntervention", "Intervention divine",
                "Relève les alliés à terre à 30 m (50% PV) et les protège 3 s. Seul : vous soigne de 50%.",
                SkillActivationMode.Press, cooldown: 60f, cast: 0.5f);
            intervention.reviveVfx = vfx.ShieldPulse;

            // Shadow
            var bleed = Dot("Dot_Bleed", DamageType.Bleed, tickFraction: 0.4f, interval: 0.5f, duration: 3f, maxStacks: 20);
            var bleedingBlades = Skill<MeleeComboSkill>("Shadow_BleedingBlades", "Lames saignantes", "2 coups (90/120%) qui font saigner.",
                SkillActivationMode.Hold, cooldown: 0f, cast: 0.35f, attackSpeed: true);
            bleedingBlades.swingVfx = vfx.Swing;
            bleedingBlades.dot = bleed;
            bleedingBlades.backstabCritical = true;
            bleedingBlades.steps = new[]
            {
                new MeleeComboSkill.Step { damageCoefficient = 0.9f, radius = 1.4f, reach = 1.5f, hitDelay = 0.06f },
                new MeleeComboSkill.Step { damageCoefficient = 1.2f, radius = 1.5f, reach = 1.5f, hitDelay = 0.08f }
            };

            var smoke = Skill<AreaSkill>("Shadow_SmokeBomb", "Fumigène", "Nuage de 6 m : 150%, ennemis désorientés 2 s.",
                SkillActivationMode.Press, cooldown: 8f, cast: 0.3f);
            smoke.center = AreaCenter.AimPoint;
            smoke.maxRange = 25f;
            smoke.radius = 6f;
            smoke.ticks = 1;
            smoke.damageCoefficient = 1.5f;
            smoke.freezeDuration = 2f;
            smoke.tickVfx = vfx.Area;

            var shadowStep = Skill<ShadowStepSkill>("Shadow_ShadowStep", "Pas de l'ombre",
                "Téléportation dans le dos de la cible visée (20 m) ; le coup suivant est un critique garanti.",
                SkillActivationMode.Press, cooldown: 7f, cast: 0f);
            shadowStep.range = 20f;
            shadowStep.vfx = vfx.Blink;
            shadowStep.canCastWhileBusy = true;

            var bladeDance = Skill<BladeDanceSkill>("Shadow_BladeDance", "Danse des lames",
                "1,5 s invulnérable en frappant 8 fois tous les ennemis à 6 m (8 × 120%).",
                SkillActivationMode.Press, cooldown: 14f, cast: 0.2f);
            bladeDance.hitVfx = vfx.Swing;

            foreach (var skill in new SkillDefinition[] { radiant, shieldThrow, crusade, intervention, bleedingBlades, smoke, shadowStep, bladeDance })
                EditorUtility.SetDirty(skill);
            SetAlternatives(characters[4], radiant, shieldThrow, crusade, intervention);
            SetAlternatives(characters[5], bleedingBlades, smoke, shadowStep, bladeDance);
        }

        private const string ChallengesFolder = Configs + "/Challenges";

        /// <summary>Unlock challenges (validated design). Anything rewarded here starts locked.</summary>
        private static List<ChallengeDefinition> BuildChallenges(List<CharacterDefinition> characters, List<ItemDefinition> items)
        {
            Directory.CreateDirectory(ChallengesFolder);
            var archer = characters[0];
            var warrior = characters[1];
            var arcanist = characters[2];
            var druid = characters[3];
            var paladin = characters[4];
            var shadow = characters[5];
            ItemDefinition Find(string file) => items.Find(i => i != null && i.name == file);

            var list = new List<ChallengeDefinition>
            {
                Challenge("Unlock_Arcanist", "Apprenti mage", "Atteindre l'étape 3.", ChallengeStat.MaxStage, 3, null, arcanist),
                Challenge("Unlock_Druid", "Tueur de géants", "Vaincre 3 boss du téléporteur (cumulé).", ChallengeStat.BossKills, 3, null, druid),
                Challenge("Unlock_HardMode", "Vainqueur", "Remporter une victoire.", ChallengeStat.Victories, 1, null, null, hardMode: true),
                Challenge("Unlock_PhoenixFeather", "Increvable", "Être mis à terre 10 fois (cumulé).", ChallengeStat.Downs, 10, null, Find("Item_PhoenixFeather")),
                Challenge("Unlock_ChaosEye", "Briseur de chaos", "Vaincre l'Avatar du chaos.", ChallengeStat.AvatarKills, 1, null, Find("Item_ChaosEye")),
                Challenge("Unlock_StormOrb", "Chasseur d'élites", "Tuer 50 élites (cumulé).", ChallengeStat.EliteKills, 50, null, Find("Item_StormOrb")),
                Challenge("Unlock_Hourglass", "Pressé", "Terminer une étape en moins de 5 minutes.", ChallengeStat.FastStages, 1, null, Find("Item_Hourglass")),
                Challenge("Unlock_EnchantedQuiver", "Pilleur", "Ouvrir 50 coffres (cumulé).", ChallengeStat.ChestsOpened, 50, null, Find("Item_EnchantedQuiver")),
                Challenge("Unlock_ExecutionerBlade", "Exorciste", "Purifier 3 tombes (cumulé).", ChallengeStat.TombsPurified, 3, null, Find("Item_ExecutionerBlade")),
                Challenge("Unlock_OakShield", "Téméraire", "Réussir 3 autels de défi (cumulé).", ChallengeStat.AltarsCompleted, 3, null, Find("Item_OakShield")),

                // Alternative skills (with the character)
                Challenge("Alt_Archer_Primary", "Archer : premiers pas", "Avec l'Archer, atteindre l'étape 3.", ChallengeStat.MaxStage, 3, archer, archer.primaryAlt),
                Challenge("Alt_Archer_Secondary", "Archer : intouchable", "Avec l'Archer, terminer une étape sans être mis à terre.", ChallengeStat.StagesWithoutDown, 1, archer, archer.secondaryAlt),
                Challenge("Alt_Archer_Utility", "Archer : fouineur", "Avec l'Archer, ouvrir 20 coffres (cumulé).", ChallengeStat.ChestsOpened, 20, archer, archer.utilityAlt),
                Challenge("Alt_Archer_Special", "Archer : carnage", "Avec l'Archer, tuer 300 ennemis (cumulé).", ChallengeStat.Kills, 300, archer, archer.specialAlt),
                Challenge("Alt_Warrior_Primary", "Guerrier : massacre", "Avec le Guerrier, tuer 500 ennemis (cumulé).", ChallengeStat.Kills, 500, warrior, warrior.primaryAlt),
                Challenge("Alt_Warrior_Secondary", "Guerrier : premiers pas", "Avec le Guerrier, atteindre l'étape 3.", ChallengeStat.MaxStage, 3, warrior, warrior.secondaryAlt),
                Challenge("Alt_Warrior_Utility", "Guerrier : inébranlable",
                    "Avec le Guerrier, survivre à un événement de téléporteur sans être mis à terre.", ChallengeStat.TeleporterEventsWithoutDown, 1, warrior, warrior.utilityAlt),
                Challenge("Alt_Warrior_Special", "Guerrier : champion", "Avec le Guerrier, vaincre un boss.", ChallengeStat.BossKills, 1, warrior, warrior.specialAlt),
                Challenge("Alt_Arcanist_Primary", "Arcaniste : puissance brute", "Avec l'Arcaniste, tuer 300 ennemis (cumulé).", ChallengeStat.Kills, 300, arcanist, arcanist.primaryAlt),
                Challenge("Alt_Arcanist_Secondary", "Arcaniste : maître du temps", "Avec l'Arcaniste, geler 50 ennemis (cumulé).", ChallengeStat.Freezes, 50, arcanist, arcanist.secondaryAlt),
                Challenge("Alt_Arcanist_Utility", "Arcaniste : premiers pas", "Avec l'Arcaniste, atteindre l'étape 3.", ChallengeStat.MaxStage, 3, arcanist, arcanist.utilityAlt),
                Challenge("Alt_Arcanist_Special", "Arcaniste : fléau des élites", "Avec l'Arcaniste, tuer 20 élites (cumulé).", ChallengeStat.EliteKills, 20, arcanist, arcanist.specialAlt),
                Challenge("Alt_Druid_Primary", "Druide : prédateur", "Avec le Druide, tuer 300 ennemis (cumulé).", ChallengeStat.Kills, 300, druid, druid.primaryAlt),
                Challenge("Alt_Druid_Secondary", "Druide : premiers pas", "Avec le Druide, atteindre l'étape 3.", ChallengeStat.MaxStage, 3, druid, druid.secondaryAlt),
                Challenge("Alt_Druid_Utility", "Druide : enraciné", "Avec le Druide, atteindre l'étape 5.", ChallengeStat.MaxStage, 5, druid, druid.utilityAlt),
                Challenge("Alt_Druid_Special", "Druide : pestilence", "Avec le Druide, empoisonner 500 ennemis (cumulé).", ChallengeStat.PoisonedEnemies, 500, druid, druid.specialAlt),

                Challenge("Unlock_Paladin", "Croisade", "Atteindre l'étape 5.", ChallengeStat.MaxStage, 5, null, paladin),
                Challenge("Unlock_Shadow", "Mille lames", "Tuer 1 000 ennemis (cumulé).", ChallengeStat.Kills, 1000, null, shadow),
                Challenge("Alt_Paladin_Primary", "Paladin : premiers pas", "Avec le Paladin, atteindre l'étape 3.", ChallengeStat.MaxStage, 3, paladin, paladin.primaryAlt),
                Challenge("Alt_Paladin_Secondary", "Paladin : justicier", "Avec le Paladin, tuer 300 ennemis (cumulé).", ChallengeStat.Kills, 300, paladin, paladin.secondaryAlt),
                Challenge("Alt_Paladin_Utility", "Paladin : rempart",
                    "Avec le Paladin, survivre à un événement de téléporteur sans être mis à terre.", ChallengeStat.TeleporterEventsWithoutDown, 1, paladin, paladin.utilityAlt),
                Challenge("Alt_Paladin_Special", "Paladin : élu", "Avec le Paladin, vaincre un boss.", ChallengeStat.BossKills, 1, paladin, paladin.specialAlt),
                Challenge("Alt_Shadow_Primary", "Ombre : premiers pas", "Avec l'Ombre, atteindre l'étape 3.", ChallengeStat.MaxStage, 3, shadow, shadow.primaryAlt),
                Challenge("Alt_Shadow_Secondary", "Ombre : chasseuse", "Avec l'Ombre, tuer 300 ennemis (cumulé).", ChallengeStat.Kills, 300, shadow, shadow.secondaryAlt),
                Challenge("Alt_Shadow_Utility", "Ombre : insaisissable", "Avec l'Ombre, terminer une étape sans être mise à terre.", ChallengeStat.StagesWithoutDown, 1, shadow, shadow.utilityAlt),
                Challenge("Alt_Shadow_Special", "Ombre : exécutrice", "Avec l'Ombre, tuer 20 élites (cumulé).", ChallengeStat.EliteKills, 20, shadow, shadow.specialAlt)
            };
            return list;
        }

        private static ChallengeDefinition Challenge(string file, string displayName, string description, ChallengeStat stat, long target,
            CharacterDefinition character, Object reward, bool hardMode = false)
        {
            var challenge = Asset<ChallengeDefinition>($"{ChallengesFolder}/{file}.asset");
            challenge.displayName = displayName;
            challenge.description = description;
            challenge.stat = stat;
            challenge.target = target;
            challenge.character = character;
            challenge.reward = reward;
            challenge.unlocksHardMode = hardMode;
            EditorUtility.SetDirty(challenge);
            return challenge;
        }

        private static void SetAlternatives(CharacterDefinition character, SkillDefinition primary, SkillDefinition secondary, SkillDefinition utility, SkillDefinition special)
        {
            character.primaryAlt = primary;
            character.secondaryAlt = secondary;
            character.utilityAlt = utility;
            character.specialAlt = special;
            EditorUtility.SetDirty(character);
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
            c.damagePerLevel = 0.15f; // RoR2: 0.2; lower because levels come faster here (see PlayerExperience)
            c.healthPerLevel = 0.3f;
            c.regenPerLevel = 0.2f;
            c.baseDamage = 10f;
            c.movement = movement;
            c.primary = primary;
            c.secondary = secondary;
            c.utility = utility;
            c.special = special;
            EditorUtility.SetDirty(c);
            return c;
        }

        // ---------------------------------------------------------------- Items & chests

        private static List<ItemDefinition> BuildItems(ProjectileSet projectiles, VfxSet vfx)
        {
            // Assets are updated in place (user icons and sounds survive); obsolete ones are removed at the end of Build.

            var bleed = Dot("Dot_Bleed", DamageType.Bleed, tickFraction: 0.4f, interval: 0.5f, duration: 3f, maxStacks: 20);
            var burn = Dot("Dot_Burn", DamageType.Fire, tickFraction: 0.25f, interval: 0.5f, duration: 3f, maxStacks: 10);

            // Common
            var gauntlet = Item<ItemDefinition>("Item_Gauntlet", "Gantelet de célérité", "+15% vitesse d'attaque (+15%).", ItemRarity.Common, Mod(StatType.AttackSpeed, 0.15f));
            var boots = Item<ItemDefinition>("Item_Boots", "Bottes du messager", "+14% vitesse de déplacement (+14%).", ItemRarity.Common, Mod(StatType.MoveSpeed, 0.14f));
            var hawkEye = Item<ItemDefinition>("Item_HawkEye", "Œil de faucon", "+10% chances de coup critique (+10%).", ItemRarity.Common, Mod(StatType.CritChance, 0.1f));
            var meat = Item<ItemDefinition>("Item_DriedMeat", "Viande séchée", "+25 PV max (+25).", ItemRarity.Common, Mod(StatType.MaxHealth, 25f));
            var fang = Item<HealOnKillItem>("Item_WolfFang", "Croc de loup", "Tuer un ennemi soigne 8 PV (+6).", ItemRarity.Common);
            fang.healFirstStack = 8f;
            fang.healPerExtraStack = 6f;

            // Uncommon
            var oak = Item<ItemDefinition>("Item_OakShield", "Bouclier de chêne", "Chaque coup reçu est réduit de 5 (+5), jamais sous 1.", ItemRarity.Common,
                Mod(StatType.FlatDamageReduction, 5f));
            var dagger = Item<DotOnHitItem>("Item_BarbedDagger", "Dague barbelée", "10% (+10%) de chances de faire saigner : 240% en 3 s.", ItemRarity.Common);
            dagger.dot = bleed;
            dagger.chanceFirstStack = 0.1f;
            dagger.chancePerExtraStack = 0.1f;
            var fireOrb = Item<DotAreaOnKillItem>("Item_FireOrb", "Orbe de feu", "Un ennemi tué brûle les ennemis à 4 m (+4 m) : 150% en 3 s.", ItemRarity.Uncommon);
            fireOrb.dot = burn;
            fireOrb.radiusFirstStack = 4f;
            fireOrb.radiusPerExtraStack = 4f;
            fireOrb.vfx = vfx.Explosion;
            var feather = Item<ItemDefinition>("Item_GriffinFeather", "Plume de griffon", "+1 saut (+1).", ItemRarity.Uncommon, Mod(StatType.ExtraJumps, 1f));
            var executioner = Item<ExecutionerItem>("Item_ExecutionerBlade", "Lame du bourreau", "+30% (+30%) de dégâts contre les ennemis sous 30% de PV.", ItemRarity.Common);
            executioner.healthThreshold = 0.3f;
            executioner.bonusPerStack = 0.3f;
            executioner.aboveThreshold = false;
            var breaker = Item<ExecutionerItem>("Item_GuardBreaker", "Masse brise-garde", "+75% (+75%) de dégâts contre les ennemis à plus de 90% de PV.", ItemRarity.Common);
            breaker.healthThreshold = 0.9f;
            breaker.bonusPerStack = 0.75f;
            breaker.aboveThreshold = true;
            var adrenaline = Item<AdrenalineItem>("Item_AdrenalineVial", "Fiole d'adrénaline", "Après un kill : +25% (+15%) de vitesse d'attaque pendant 3 s.", ItemRarity.Common);
            adrenaline.bonusFirstStack = 0.25f;
            adrenaline.bonusPerExtraStack = 0.15f;
            adrenaline.duration = 3f;
            var whetstone = Item<ItemDefinition>("Item_Whetstone", "Pierre à aiguiser", "+8% de dégâts (+8%).", ItemRarity.Common, Mod(StatType.Damage, 0.08f));

            // Rare
            var hourglass = Item<ItemDefinition>("Item_Hourglass", "Sablier ancien", "-12% temps de recharge (multiplicatif).", ItemRarity.Uncommon, Mod(StatType.CooldownReduction, 0.12f));
            var storm = Item<ChainOnHitItem>("Item_StormOrb", "Orbe de tempête", "25% de chances : éclair sur 3 ennemis (+2), 80% des dégâts.", ItemRarity.Uncommon);
            storm.boltVfx = vfx.Lightning;
            var oil = Item<ExplodeOnKillItem>("Item_FireOil", "Huile incendiaire", "Les ennemis tués explosent : 150% (+75%) dans 6 m (+1,5 m).", ItemRarity.Uncommon);
            oil.explosionVfx = vfx.Explosion;
            var quiver = Item<ProjectileOnHitItem>("Item_EnchantedQuiver", "Carquois enchanté", "10% de chances : missile chercheur, 300% des dégâts (+300%).", ItemRarity.Uncommon);
            quiver.projectile = projectiles.ArcaneMissile;
            var horn = Item<WarHornItem>("Item_WarHorn", "Cor de guerre",
                "Chaque kill : +15% vitesse de déplacement et d'attaque, max 5 (+1). Tout est perdu en subissant des dégâts.", ItemRarity.Rare);

            // Legendary
            var chaosEye = Item<ExplodeOnHitItem>("Item_ChaosEye", "Œil du chaos", "Chaque coup explose : 60% des dégâts dans 4 m (+2,5 m).", ItemRarity.Legendary);
            chaosEye.explosionVfx = vfx.Explosion;
            var phoenix = Item<PhoenixFeatherItem>("Item_PhoenixFeather", "Plume de phénix", "À la mort, ressuscite avec 50% des PV. Consommée.", ItemRarity.Legendary);
            var grimoire = Item<ItemDefinition>("Item_ForbiddenGrimoire", "Grimoire interdit", "+1 charge (+1) à toutes les compétences sauf la principale.",
                ItemRarity.Rare, Mod(StatType.BonusSkillCharges, 1f));
            var twinBlade = Item<DoubleHitItem>("Item_TwinBlade", "Lame jumelle", "15% (+10%) de chances qu'un coup frappe une seconde fois (100%).", ItemRarity.Rare);
            twinBlade.chanceFirstStack = 0.15f;
            twinBlade.chancePerExtraStack = 0.1f;
            var frostRing = Item<FreezeOnHitItem>("Item_FrostRing", "Anneau de givre", "10% (+5%) de chances de geler 1,5 s et d'infliger 250% de givre.", ItemRarity.Rare);
            frostRing.chanceFirstStack = 0.1f;
            frostRing.chancePerExtraStack = 0.05f;
            frostRing.freezeDuration = 1.5f;
            frostRing.damageCoefficient = 2.5f;
            frostRing.vfx = vfx.FrostBurst;
            var banner = Item<BannerItem>("Item_WarBanner", "Bannière de guerre", "Les alliés à 12 m (+4 m), vous compris, infligent +20% de dégâts.", ItemRarity.Rare);
            banner.radiusFirstStack = 12f;
            banner.radiusPerExtraStack = 4f;
            var comet = Item<MeteorOnHitItem>("Item_CometTear", "Larme de comète", "8% (+4%) de chances qu'un coup appelle un météore : 500% dans 5 m.", ItemRarity.Legendary);
            comet.meteor = projectiles.Meteor;
            comet.chanceFirstStack = 0.08f;
            comet.chancePerExtraStack = 0.04f;
            comet.damageCoefficient = 5f;
            var crown = Item<SoulHarvestItem>("Item_SoulCrown", "Couronne des âmes", "Chaque kill : +1% (+1%) de dégâts jusqu'à la fin de l'étape (max +60%).", ItemRarity.Legendary);
            crown.percentPerKillPerStack = 1;
            crown.maxPercent = 60;

            // Boss
            var scale = Item<ItemDefinition>("Item_ColossusScale", "Écaille du colosse", "-15% de dégâts subis (dégressif).", ItemRarity.Boss,
                Mod(StatType.DamageTakenReduction, 0.15f));
            // Additions
            var pendant = Item<HealOnHitItem>("Item_BloodPendant", "Pendentif sanguin", "Chaque coup soigne 1 PV (+1).", ItemRarity.Common);
            pendant.healPerStack = 1f;
            var vigorRoot = Item<ItemDefinition>("Item_VigorRoot", "Racine de vigueur", "+1,5 PV/s de régénération (+1,5).", ItemRarity.Common,
                Mod(StatType.HealthRegen, 1.5f));
            var amber = Item<ChainOnHitItem>("Item_AmberShard", "Éclat d'ambre",
                "20% de chances : éclair en chaîne, 50% (+15%) des dégâts, 2 cibles (+1).", ItemRarity.Uncommon);
            amber.chance = 0.2f;
            amber.damageFraction = 0.5f;
            amber.damageFractionPerExtraStack = 0.15f;
            amber.targetsFirstStack = 2;
            amber.targetsPerExtraStack = 1;
            amber.boltVfx = vfx.Lightning;
            var thorns = Item<ThornsItem>("Item_ThornBracelet", "Bracelet d'épines",
                "Quand vous êtes touché : 200% (+100%) de vos dégâts de base renvoyés à l'attaquant. Recharge 15 s (-2 s).", ItemRarity.Uncommon);
            thorns.vfx = vfx.Impact;
            var voodoo = Item<CurseAreaOnKillItem>("Item_VoodooDoll", "Poupée vaudou",
                "Un ennemi tué maudit les ennemis à 4 m (+1 m) pendant 4 s.", ItemRarity.Uncommon);
            voodoo.vfx = vfx.CursePulse;
            var cloak = Item<StatusWhenHitItem>("Item_FugitiveCloak", "Cape du fuyard",
                "Quand vous êtes touché : +40% de vitesse de déplacement et d'attaque pendant 2 s (+1 s). Recharge 5 s.", ItemRarity.Uncommon);
            cloak.status = StatusType.Rally;
            cloak.vfx = vfx.RallyPulse;
            var resonance = Item<CritSplashItem>("Item_ResonanceStone", "Pierre de résonance",
                "Les coups critiques infligent 50% (+25%) de leurs dégâts autour de la cible (3 m).", ItemRarity.Uncommon);
            resonance.vfx = vfx.ArcaneBurst;

            var bone = Item<ShockwaveEveryHitsItem>("Item_TitanBone", "Os de titan", "Toutes les 5 attaques : onde de choc de 300% (+150%) autour de soi.", ItemRarity.Boss);
            bone.vfx = vfx.Shockwave;

            var items = new List<ItemDefinition>
            {
                gauntlet, boots, hawkEye, meat, fang, oak, dagger, executioner, breaker, adrenaline, whetstone,
                pendant, vigorRoot,
                fireOrb, feather, hourglass, storm, oil, quiver, amber, thorns, voodoo, cloak, resonance,
                horn, grimoire, twinBlade, frostRing, banner,
                chaosEye, phoenix, comet, crown,
                scale, bone
            };
            foreach (var item in items)
                EditorUtility.SetDirty(item);
            return items;
        }

        private static DotEffect Dot(string file, DamageType type, float tickFraction, float interval, float duration, int maxStacks)
        {
            var dot = Asset<DotEffect>($"{ItemsFolder}/{file}.asset");
            dot.damageType = type;
            dot.tickDamageFraction = tickFraction;
            dot.tickInterval = interval;
            dot.duration = duration;
            dot.maxStacks = maxStacks;
            EditorUtility.SetDirty(dot);
            return dot;
        }

        private static List<EquipmentDefinition> BuildEquipment(VfxSet vfx)
        {
            var horn = Asset<RallyHornEquipment>($"{EquipmentFolder}/Equipment_RallyHorn.asset");
            horn.displayName = "Corne de ralliement";
            horn.description = "+40% vitesse d'attaque et de déplacement pendant 8 s, pour soi et les alliés à 25 m.";
            horn.cooldown = 45f;
            horn.radius = 25f;
            horn.duration = 8f;
            horn.vfx = vfx.RallyPulse;

            var tome = Asset<CurseConeEquipment>($"{EquipmentFolder}/Equipment_DarkTome.asset");
            tome.displayName = "Tome sombre";
            tome.description = "Coûte 25% des PV max. Maudit les ennemis devant soi (20 m) pendant 8 s : -50% dégâts infligés, -10% vitesse, +30% dégâts subis.";
            tome.cooldown = 30f;
            tome.healthCost = 0.25f;
            tome.range = 20f;
            tome.coneAngle = 35f;
            tome.curseDuration = 8f;
            tome.vfx = vfx.CursePulse;

            var stone = Asset<LightningStrikeEquipment>($"{EquipmentFolder}/Equipment_LightningStone.asset");
            stone.displayName = "Pierre de foudre";
            stone.description = "La foudre frappe 3 fois la zone visée : 500% chaque.";
            stone.cooldown = 30f;
            stone.strikes = 3;
            stone.damageCoefficient = 5f;
            stone.radius = 4f;
            stone.boltVfx = vfx.Lightning;
            stone.impactVfx = vfx.Explosion;

            var all = new List<EquipmentDefinition> { horn, tome, stone };
            foreach (var equipment in all)
                EditorUtility.SetDirty(equipment);
            return all;
        }

        private static T Item<T>(string file, string displayName, string description, ItemRarity rarity, params StatModifier[] modifiers)
            where T : ItemDefinition
        {
            var item = Asset<T>($"{ItemsFolder}/{file}.asset");
            item.displayName = displayName;
            item.description = description;
            item.rarity = rarity;
            item.modifiers = modifiers;
            return item;
        }

        private static StatModifier Mod(StatType stat, float valuePerStack) => new() { stat = stat, valuePerStack = valuePerStack };

        private static ItemPickup BuildPickupPrefab()
        {
            var root = new GameObject("ItemPickup");
            root.AddComponent<NetworkObject>();
            var visual = CreateVisual(PrimitiveType.Cube, "Visual", root.transform, new Vector3(0f, 0.8f, 0f), Vector3.one * 0.45f, Mat("M_Pickup", Color.white, unlit: true));
            visual.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            var trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true; // equipment pickups are aimed at and taken with E
            trigger.center = new Vector3(0f, 0.8f, 0f);
            trigger.radius = 0.8f;
            var pickup = root.AddComponent<ItemPickup>();
            SetProperties(pickup, so => so.FindProperty("floatingVisual").objectReferenceValue = visual.transform);
            return SavePrefab(root, Prefabs + "/ItemPickup.prefab").GetComponent<ItemPickup>();
        }

        private static GameObject BuildChestPrefab<T>(string file, string displayName, uint cost, float size, RarityWeights weights, Color color, ItemPickup pickup)
            where T : ChestInteractable
        {
            var root = new GameObject(file);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.35f * size, 0f);
            body.transform.localScale = new Vector3(1.2f, 0.7f, 0.8f) * size;
            body.GetComponent<Renderer>().sharedMaterial = Mat("M_" + file, color);

            var lid = CreateVisual(PrimitiveType.Cube, "Lid", root.transform, new Vector3(0f, 0.78f * size, 0f), new Vector3(1.25f, 0.15f, 0.85f) * size,
                Mat("M_" + file + "_Lid", color * 0.7f));

            root.AddComponent<NetworkObject>();
            var chest = root.AddComponent<T>();
            SetProperties(chest, so =>
            {
                so.FindProperty("displayName").stringValue = displayName;
                so.FindProperty("baseCost").uintValue = cost;
                so.FindProperty("maxUses").intValue = 1;
                var rarity = so.FindProperty("rarityWeights");
                rarity.FindPropertyRelative("common").floatValue = weights.common;
                rarity.FindPropertyRelative("uncommon").floatValue = weights.uncommon;
                rarity.FindPropertyRelative("rare").floatValue = weights.rare;
                rarity.FindPropertyRelative("legendary").floatValue = weights.legendary;
                so.FindProperty("pickupPrefab").objectReferenceValue = pickup;
                so.FindProperty("dropOffset").vector3Value = new Vector3(0f, 0f, 1.4f * size);
                so.FindProperty("closedVisual").objectReferenceValue = lid;
            });
            return SavePrefab(root, $"{Prefabs}/{file}.prefab");
        }

        private static GameObject BuildEquipmentBarrelPrefab()
        {
            var root = new GameObject("EquipmentBarrel");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            body.transform.localScale = new Vector3(0.9f, 0.6f, 0.9f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("M_EquipmentBarrel", new Color(0.8f, 0.45f, 0.2f));
            var lid = CreateVisual(PrimitiveType.Cylinder, "Lid", root.transform, new Vector3(0f, 1.25f, 0f), new Vector3(0.95f, 0.06f, 0.95f),
                Mat("M_EquipmentBarrel_Lid", new Color(0.45f, 0.3f, 0.15f)));

            root.AddComponent<NetworkObject>();
            var barrel = root.AddComponent<EquipmentBarrel>();
            SetProperties(barrel, so =>
            {
                so.FindProperty("displayName").stringValue = "Baril d'équipement";
                so.FindProperty("baseCost").uintValue = 35;
                so.FindProperty("maxUses").intValue = 1;
                so.FindProperty("closedVisual").objectReferenceValue = lid;
            });
            return SavePrefab(root, Prefabs + "/EquipmentBarrel.prefab");
        }

        private static GameObject BuildMerchantPrefab()
        {
            var root = new GameObject("Merchant");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("M_Merchant", new Color(0.35f, 0.25f, 0.5f));
            CreateVisual(PrimitiveType.Cylinder, "Hat", root.transform, new Vector3(0f, 2.15f, 0f), new Vector3(0.9f, 0.08f, 0.9f), Mat("M_MerchantHat", new Color(0.2f, 0.15f, 0.1f)));
            CreateVisual(PrimitiveType.Cylinder, "HatTop", root.transform, new Vector3(0f, 2.4f, 0f), new Vector3(0.5f, 0.25f, 0.5f), Mat("M_MerchantHat", new Color(0.2f, 0.15f, 0.1f)));
            var stall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stall.name = "Stall";
            stall.transform.SetParent(root.transform, false);
            stall.transform.localPosition = new Vector3(0f, 0.5f, 1.1f);
            stall.transform.localScale = new Vector3(2.2f, 1f, 0.7f);
            stall.GetComponent<Renderer>().sharedMaterial = Mat("M_MerchantStall", new Color(0.5f, 0.35f, 0.2f));

            root.AddComponent<NetworkObject>();
            root.AddComponent<Merchant>();
            return SavePrefab(root, Prefabs + "/Merchant.prefab");
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
            root.AddComponent<PlayerInventory>();
            root.AddComponent<StatusEffects>();
            root.AddComponent<PlayerEquipment>();
            root.AddComponent<PlayerDowned>(); // after PlayerInventory: Phoenix Feather prevents death first
            root.AddComponent<LocalCombatFeedback>();
            root.AddComponent<PlayerAudio>();
            root.AddComponent<PlayerRunStats>();
            root.AddComponent<PlayerAnimationDriver>();
            root.AddComponent<FallRecovery>();
            root.AddComponent<PlayerExperience>();

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
            camGo.AddComponent<CameraEffects>();

            var look = root.AddComponent<PlayerLook>();
            SetProperties(look, so =>
            {
                so.FindProperty("cameraPivot").objectReferenceValue = pivot;
                so.FindProperty("playerCamera").objectReferenceValue = cam;
            });

            var bubble = CreateVisual(PrimitiveType.Sphere, "ShieldBubble", root.transform, new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 2.2f, 1.6f),
                Mat("M_ShieldBubble", new Color(0.7f, 0.45f, 1f), unlit: true));
            bubble.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            bubble.SetActive(false);
            var shieldVisual = root.AddComponent<HealthShieldVisual>();
            SetProperties(shieldVisual, so => so.FindProperty("visual").objectReferenceValue = bubble);

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
            var def = EnemyDef("Enemy_Ghoul", "Goule", health: 70f, speed: 6f, gold: 8, detection: 30f, feminine: true);
            var root = EnemyBase("Enemy_Ghoul", def, new Color(0.75f, 0.2f, 0.2f), out _);
            var attack = root.AddComponent<EnemyMeleeAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 2f;
                so.FindProperty("cooldown").floatValue = 1.5f;
                so.FindProperty("damage").floatValue = 12f;
            });
            FinishEnemy(root, attack, dodge: so =>
            {
                so.FindProperty("chanceOnHit").floatValue = 0.12f;
                so.FindProperty("chanceWhenAimedAt").floatValue = 0.05f;
            });
            return SavePrefab(root, Prefabs + "/Enemy_Ghoul.prefab").GetComponent<EnemyActor>();
        }

        /// <summary>Fast pack hunter: flanks, then bounds at the player from mid range.</summary>
        private static EnemyActor BuildWolf()
        {
            var def = EnemyDef("Enemy_Wolf", "Loup spectral", health: 40f, speed: 7f, gold: 8, detection: 35f);
            def.movementStyle = EnemyMovementStyle.Flank;
            def.flankRadius = 7f;
            def.flankAngle = new Vector2(90f, 160f);
            def.wanderRadius = 10f;
            var root = EnemyBase("Enemy_Wolf", def, new Color(0.45f, 0.55f, 0.75f), out _);
            root.transform.localScale = Vector3.one * 0.8f;
            var attack = root.AddComponent<EnemyLungeAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 7f;
                so.FindProperty("cooldown").floatValue = 1.8f;
                so.FindProperty("damage").floatValue = 9f;
                so.FindProperty("windup").floatValue = 0.4f;
                so.FindProperty("dashSpeed").floatValue = 16f;
                so.FindProperty("dashDuration").floatValue = 0.35f;
                so.FindProperty("recovery").floatValue = 0.5f;
            });
            FinishEnemy(root, attack, dodge: so =>
            {
                so.FindProperty("chanceOnHit").floatValue = 0.1f;
                so.FindProperty("chanceWhenAimedAt").floatValue = 0.1f;
                so.FindProperty("distance").floatValue = 5f;
            });
            return SavePrefab(root, Prefabs + "/Enemy_Wolf.prefab").GetComponent<EnemyActor>();
        }

        /// <summary>Gets behind the player and sidesteps a lot: forces the player to turn around.</summary>
        private static EnemyActor BuildAssassin()
        {
            var def = EnemyDef("Enemy_Assassin", "Lame d'ombre", health: 50f, speed: 7.5f, gold: 10, detection: 30f, feminine: true);
            def.movementStyle = EnemyMovementStyle.Flank;
            def.flankRadius = 5f;
            def.flankAngle = new Vector2(140f, 180f);
            var root = EnemyBase("Enemy_Assassin", def, new Color(0.25f, 0.15f, 0.35f), out _);
            var attack = root.AddComponent<EnemyMeleeAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 2f;
                so.FindProperty("cooldown").floatValue = 0.9f;
                so.FindProperty("damage").floatValue = 9f;
                so.FindProperty("windup").floatValue = 0.3f;
                so.FindProperty("recovery").floatValue = 0.25f;
            });
            FinishEnemy(root, attack, dodge: so =>
            {
                so.FindProperty("chanceOnHit").floatValue = 0.3f;
                so.FindProperty("chanceWhenAimedAt").floatValue = 0.25f;
                so.FindProperty("cooldown").floatValue = 2.5f;
                so.FindProperty("distance").floatValue = 5f;
                so.FindProperty("backwardBias").floatValue = 0f;
            });
            return SavePrefab(root, Prefabs + "/Enemy_Assassin.prefab").GetComponent<EnemyActor>();
        }

        /// <summary>Slow tank with a long telegraphed charge.</summary>
        private static EnemyActor BuildOgre()
        {
            var def = EnemyDef("Enemy_Ogre", "Ogre", health: 240f, speed: 4.5f, gold: 24, detection: 30f);
            def.controlResistance = 0.4f;
            var root = EnemyBase("Enemy_Ogre", def, new Color(0.4f, 0.5f, 0.25f), out _);
            root.transform.localScale = Vector3.one * 1.5f;
            var attack = root.AddComponent<EnemyLungeAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 12f;
                so.FindProperty("cooldown").floatValue = 4f;
                so.FindProperty("damage").floatValue = 26f;
                so.FindProperty("windup").floatValue = 0.9f;
                so.FindProperty("dashSpeed").floatValue = 16f;
                so.FindProperty("dashDuration").floatValue = 0.7f;
                so.FindProperty("recovery").floatValue = 1f;
                so.FindProperty("hitRadius").floatValue = 1.8f;
                so.FindProperty("maxHeightDifference").floatValue = 3f;
            });
            FinishEnemy(root, attack);
            return SavePrefab(root, Prefabs + "/Enemy_Ogre.prefab").GetComponent<EnemyActor>();
        }

        /// <summary>Artillery: keeps its distance, stops to lob telegraphed fire bombs.</summary>
        private static EnemyActor BuildPyromancer(ProjectileDefinition firebomb, GameObject telegraph)
        {
            var def = EnemyDef("Enemy_Pyromancer", "Pyromancien", health: 55f, speed: 5f, gold: 12, detection: 40f);
            def.movementStyle = EnemyMovementStyle.Kite;
            def.kiteRange = new Vector2(11f, 24f);
            def.retreatDistance = 8f;
            var root = EnemyBase("Enemy_Pyromancer", def, new Color(0.95f, 0.5f, 0.15f), out _);
            var attack = root.AddComponent<EnemyArtilleryAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 28f;
                so.FindProperty("cooldown").floatValue = 3f;
                so.FindProperty("damage").floatValue = 16f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = firebomb;
                so.FindProperty("telegraphVfx").objectReferenceValue = telegraph;
            });
            FinishEnemy(root, attack);
            return SavePrefab(root, Prefabs + "/Enemy_Pyromancer.prefab").GetComponent<EnemyActor>();
        }

        private static EnemyActor BuildSkeletonArcher(ProjectileDefinition bolt)
        {
            var def = EnemyDef("Enemy_SkeletonArcher", "Squelette archer", health: 60f, speed: 4f, gold: 14, detection: 35f);
            def.movementStyle = EnemyMovementStyle.Kite;
            def.kiteRange = new Vector2(10f, 20f);
            var root = EnemyBase("Enemy_SkeletonArcher", def, new Color(0.85f, 0.85f, 0.75f), out _);
            var attack = root.AddComponent<EnemyProjectileAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 22f;
                so.FindProperty("cooldown").floatValue = 2f;
                so.FindProperty("damage").floatValue = 12f;
                so.FindProperty("projectile").objectReferenceValue = bolt;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
            });
            FinishEnemy(root, attack);
            return SavePrefab(root, Prefabs + "/Enemy_SkeletonArcher.prefab").GetComponent<EnemyActor>();
        }

        /// <summary>Seismic titan: ground slam + expanding wave to jump over, lobbed boulder, charge.</summary>
        private static EnemyActor BuildBoss(ProjectileDefinition boulder, VfxSet vfx)
        {
            var def = BossDef("Boss_BoneColossus", "Colosse d'os", health: 1400f, speed: 5f);
            var root = EnemyBase("Boss_BoneColossus", def, new Color(0.35f, 0.3f, 0.28f), out _);
            root.transform.localScale = Vector3.one * 2.2f;

            // Primary (drives the approach): slam + shockwave.
            var slam = root.AddComponent<EnemyShockwaveAttack>();
            SetProperties(slam, so =>
            {
                so.FindProperty("range").floatValue = 7f;
                so.FindProperty("cooldown").floatValue = 4.5f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
                so.FindProperty("slamDamage").floatValue = 30f;
                so.FindProperty("slamRadius").floatValue = 4f;
                so.FindProperty("windup").floatValue = 1f;
                so.FindProperty("recovery").floatValue = 0.8f;
                so.FindProperty("waveDamage").floatValue = 20f;
                so.FindProperty("waveMaxRadius").floatValue = 25f;
                so.FindProperty("waveSpeed").floatValue = ColossusWaveSpeed;
                so.FindProperty("waveThickness").floatValue = 1.5f;
                so.FindProperty("jumpClearance").floatValue = 0.5f;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("slamVfx").objectReferenceValue = vfx.Shockwave;
                so.FindProperty("waveVfx").objectReferenceValue = vfx.GroundWave;
            });
            var rock = root.AddComponent<EnemyArtilleryAttack>();
            SetProperties(rock, so =>
            {
                so.FindProperty("range").floatValue = 40f;
                so.FindProperty("minRange").floatValue = 10f;
                so.FindProperty("cooldown").floatValue = 5f;
                so.FindProperty("damage").floatValue = 30f;
                so.FindProperty("damageType").enumValueIndex = (int)DamageType.Physical;
                so.FindProperty("windup").floatValue = 0.9f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = boulder;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
            });
            var charge = root.AddComponent<EnemyLungeAttack>();
            SetProperties(charge, so =>
            {
                so.FindProperty("range").floatValue = 18f;
                so.FindProperty("minRange").floatValue = 7f;
                so.FindProperty("cooldown").floatValue = 8f;
                so.FindProperty("damage").floatValue = 32f;
                so.FindProperty("windup").floatValue = 0.9f;
                so.FindProperty("dashSpeed").floatValue = 22f;
                so.FindProperty("dashDuration").floatValue = 0.6f;
                so.FindProperty("recovery").floatValue = 1f;
                so.FindProperty("hitRadius").floatValue = 2.4f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
            });
            FinishEnemy(root, slam);
            return SavePrefab(root, Prefabs + "/Boss_BoneColossus.prefab").GetComponent<EnemyActor>();
        }

        private static GameObject BuildTeleporterPrefab(List<SpawnCard> bossCards, List<SpawnCard> finalBossCards, GameObject seal)
        {
            var stone = Mat("M_TeleporterStone", new Color(0.25f, 0.23f, 0.28f));
            var glow = Mat("M_TeleporterGlow", Color.white, unlit: true);

            var root = new GameObject("Teleporter");
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.3f, 0f);
            pedestal.transform.localScale = new Vector3(4f, 0.3f, 4f);
            pedestal.GetComponent<Renderer>().sharedMaterial = stone;

            var glowRenderers = new List<Renderer>();
            for (int i = 0; i < 3; i++)
            {
                float angle = i * Mathf.PI * 2f / 3f;
                var prong = CreateVisual(PrimitiveType.Cube, $"Prong_{i}", root.transform,
                    new Vector3(Mathf.Cos(angle) * 1.5f, 1.4f, Mathf.Sin(angle) * 1.5f), new Vector3(0.35f, 2.2f, 0.35f), glow);
                glowRenderers.Add(prong.GetComponent<Renderer>());
            }
            var core = CreateVisual(PrimitiveType.Sphere, "Core", root.transform, new Vector3(0f, 1.8f, 0f), Vector3.one * 0.8f, glow);
            glowRenderers.Add(core.GetComponent<Renderer>());

            var ringGo = new GameObject("RadiusRing");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.widthMultiplier = 0.35f;
            ring.sharedMaterial = LineMat();
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.enabled = false;

            root.AddComponent<NetworkObject>();
            var teleporter = root.AddComponent<Teleporter>();
            SetProperties(teleporter, so =>
            {
                var cards = so.FindProperty("bossCards");
                cards.arraySize = bossCards.Count;
                for (int i = 0; i < bossCards.Count; i++)
                    cards.GetArrayElementAtIndex(i).objectReferenceValue = bossCards[i];
                var finals = so.FindProperty("finalBossCards");
                finals.arraySize = finalBossCards.Count;
                for (int i = 0; i < finalBossCards.Count; i++)
                    finals.GetArrayElementAtIndex(i).objectReferenceValue = finalBossCards[i];
                so.FindProperty("runEndSealPrefab").objectReferenceValue = seal.GetComponent<RunEndSeal>();
                so.FindProperty("radiusRing").objectReferenceValue = ring;
                var renderers = so.FindProperty("glowRenderers");
                renderers.arraySize = glowRenderers.Count;
                for (int i = 0; i < glowRenderers.Count; i++)
                    renderers.GetArrayElementAtIndex(i).objectReferenceValue = glowRenderers[i];
            });
            return SavePrefab(root, Prefabs + "/Teleporter.prefab");
        }

        private static GameObject BuildRunEndSealPrefab()
        {
            var root = new GameObject("RunEndSeal");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Obelisk";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 2f;
            body.transform.localScale = new Vector3(1f, 4f, 1f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("M_RunEndSeal", new Color(1f, 0.85f, 0.35f), unlit: true);
            root.AddComponent<NetworkObject>();
            root.AddComponent<RunEndSeal>();
            return SavePrefab(root, Prefabs + "/RunEndSeal.prefab");
        }

        /// <summary>Final boss: chaos bolts + meteors; splits into illusions at 66% and 33% (summons from 66%, meteor storm from 33%).</summary>
        private static EnemyActor BuildChaosAvatar(ProjectileSet projectiles, VfxSet vfx, SpawnCard[] minionPool)
        {
            var def = BossDef("Boss_ChaosAvatar", "Avatar du chaos", health: 4000f, speed: 5f);
            def.movementStyle = EnemyMovementStyle.Kite;
            def.kiteRange = new Vector2(10f, 24f);
            def.retreatDistance = 8f;
            def.controlResistance = 0.8f;
            def.goldReward = 400;
            EditorUtility.SetDirty(def);
            var root = EnemyBase("Boss_ChaosAvatar", def, new Color(0.45f, 0.1f, 0.35f), out _);
            root.transform.localScale = Vector3.one * 2.6f;

            var bolts = root.AddComponent<EnemyProjectileAttack>();
            SetProperties(bolts, so =>
            {
                so.FindProperty("range").floatValue = 35f;
                so.FindProperty("cooldown").floatValue = 3f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("maxHeightDifference").floatValue = 20f;
                so.FindProperty("damage").floatValue = 10f;
                so.FindProperty("damageType").enumValueIndex = (int)DamageType.Arcane;
                so.FindProperty("windup").floatValue = 0.7f;
                so.FindProperty("count").intValue = 5;
                so.FindProperty("spreadAngle").floatValue = 40f;
                so.FindProperty("homing").boolValue = true;
                so.FindProperty("projectile").objectReferenceValue = projectiles.ChaosBolt;
            });
            var meteors = root.AddComponent<EnemyMeteorAttack>();
            SetProperties(meteors, so =>
            {
                so.FindProperty("range").floatValue = 60f;
                so.FindProperty("cooldown").floatValue = 9f;
                so.FindProperty("selectionWeight").floatValue = 2f;
                so.FindProperty("maxHeightDifference").floatValue = 30f;
                so.FindProperty("damage").floatValue = 45f;
                so.FindProperty("fallTime").floatValue = 1.6f;
                so.FindProperty("extraMeteorsPerPlayer").intValue = 2;
                so.FindProperty("meteor").objectReferenceValue = projectiles.Meteor;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
            });
            var summon = root.AddComponent<EnemySummonAttack>();
            SetProperties(summon, so =>
            {
                so.FindProperty("range").floatValue = 40f;
                so.FindProperty("cooldown").floatValue = 18f;
                so.FindProperty("selectionWeight").floatValue = 3f;
                so.FindProperty("maxHeightDifference").floatValue = 20f;
                so.FindProperty("countPerCast").intValue = 3;
                so.FindProperty("maxAlive").intValue = 6;
                so.FindProperty("spawnVfx").objectReferenceValue = vfx.Blink;
                var pool = so.FindProperty("minionPool");
                pool.arraySize = minionPool.Length;
                for (int i = 0; i < minionPool.Length; i++)
                    pool.GetArrayElementAtIndex(i).objectReferenceValue = minionPool[i];
            });

            var phases = root.AddComponent<ChaosAvatar>();
            SetProperties(phases, so =>
            {
                so.FindProperty("meteors").objectReferenceValue = meteors;
                var locked = so.FindProperty("lockedAtStart");
                locked.arraySize = 1;
                locked.GetArrayElementAtIndex(0).objectReferenceValue = summon;
                so.FindProperty("illusionHealthFraction").floatValue = 0.04f;
                so.FindProperty("splitDistance").vector2Value = new Vector2(8f, 14f);
                so.FindProperty("illusionLifetime").floatValue = 25f;
                so.FindProperty("splitVfx").objectReferenceValue = vfx.Blink;
                var list = so.FindProperty("phases");
                list.arraySize = 2;
                var p2 = list.GetArrayElementAtIndex(0);
                p2.FindPropertyRelative("healthThreshold").floatValue = 0.66f;
                p2.FindPropertyRelative("illusions").intValue = 3;
                var unlock = p2.FindPropertyRelative("unlockAttacks");
                unlock.arraySize = 1;
                unlock.GetArrayElementAtIndex(0).objectReferenceValue = summon;
                p2.FindPropertyRelative("meteorCooldownMultiplier").floatValue = 1f;
                p2.FindPropertyRelative("bonusMeteors").intValue = 0;
                var p3 = list.GetArrayElementAtIndex(1);
                p3.FindPropertyRelative("healthThreshold").floatValue = 0.33f;
                p3.FindPropertyRelative("illusions").intValue = 3;
                p3.FindPropertyRelative("unlockAttacks").arraySize = 0;
                p3.FindPropertyRelative("meteorCooldownMultiplier").floatValue = 0.5f;
                p3.FindPropertyRelative("bonusMeteors").intValue = 1;
            });
            FinishEnemy(root, bolts);
            return SavePrefab(root, Prefabs + "/Boss_ChaosAvatar.prefab").GetComponent<EnemyActor>();
        }

        private static EnemyDefinition BossDef(string file, string displayName, float health, float speed, bool feminine = false)
        {
            var def = EnemyDef(file, displayName, health, speed, gold: 150, detection: 200f, feminine);
            def.isBoss = true;
            def.controlResistance = 0.6f;
            def.alertAlliesRadius = 0f;
            def.despawnDelay = 3f;
            EditorUtility.SetDirty(def);
            return def;
        }

        private static EnemyActor BuildEmberDrake(ProjectileDefinition firebomb, VfxSet vfx, DotEffect burn)
        {
            var def = BossDef("Boss_EmberDrake", "Drake de braise", health: 1600f, speed: 6f);
            var root = EnemyBase("Boss_EmberDrake", def, new Color(0.75f, 0.2f, 0.08f), out _);
            root.transform.localScale = Vector3.one * 2f;

            // Primary (drives the approach): fire breath, slow tracking, strafe around it.
            var breath = root.AddComponent<EnemyConeAttack>();
            SetProperties(breath, so =>
            {
                so.FindProperty("range").floatValue = 9f;
                so.FindProperty("cooldown").floatValue = 6f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
                so.FindProperty("angle").floatValue = 55f;
                so.FindProperty("damagePerTick").floatValue = 7f;
                so.FindProperty("windup").floatValue = 0.8f;
                so.FindProperty("duration").floatValue = 2.2f;
                so.FindProperty("tickInterval").floatValue = 0.25f;
                so.FindProperty("originHeight").floatValue = 1.5f;
                so.FindProperty("dot").objectReferenceValue = burn;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("tickVfx").objectReferenceValue = vfx.FireBurst;
            });
            var charge = root.AddComponent<EnemyLungeAttack>();
            SetProperties(charge, so =>
            {
                so.FindProperty("range").floatValue = 16f;
                so.FindProperty("minRange").floatValue = 6f;
                so.FindProperty("cooldown").floatValue = 7f;
                so.FindProperty("damage").floatValue = 30f;
                so.FindProperty("windup").floatValue = 0.9f;
                so.FindProperty("dashSpeed").floatValue = 24f;
                so.FindProperty("dashDuration").floatValue = 0.55f;
                so.FindProperty("recovery").floatValue = 1f;
                so.FindProperty("hitRadius").floatValue = 2.2f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
            });
            var fireball = root.AddComponent<EnemyArtilleryAttack>();
            SetProperties(fireball, so =>
            {
                so.FindProperty("range").floatValue = 35f;
                so.FindProperty("minRange").floatValue = 8f;
                so.FindProperty("cooldown").floatValue = 5f;
                so.FindProperty("damage").floatValue = 28f;
                so.FindProperty("windup").floatValue = 0.9f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = firebomb;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
            });
            FinishEnemy(root, breath);
            return SavePrefab(root, Prefabs + "/Boss_EmberDrake.prefab").GetComponent<EnemyActor>();
        }

        private static EnemyActor BuildSpiderQueen(ProjectileDefinition spit, SpawnCard spiderling, VfxSet vfx)
        {
            var def = BossDef("Boss_SpiderQueen", "Reine araignée", health: 1300f, speed: 5f, feminine: true);
            def.movementStyle = EnemyMovementStyle.Kite;
            def.kiteRange = new Vector2(8f, 20f);
            def.retreatDistance = 7f;
            EditorUtility.SetDirty(def);
            var root = EnemyBase("Boss_SpiderQueen", def, new Color(0.3f, 0.12f, 0.35f), out _);
            root.transform.localScale = Vector3.one * 1.8f;

            // Primary: poison spit (keeps her at range with the Kite style).
            var spitAttack = root.AddComponent<EnemyProjectileAttack>();
            SetProperties(spitAttack, so =>
            {
                so.FindProperty("range").floatValue = 24f;
                so.FindProperty("cooldown").floatValue = 2.2f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("damage").floatValue = 14f;
                so.FindProperty("windup").floatValue = 0.6f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = spit;
            });
            var summon = root.AddComponent<EnemySummonAttack>();
            SetProperties(summon, so =>
            {
                so.FindProperty("range").floatValue = 30f;
                so.FindProperty("cooldown").floatValue = 15f;
                so.FindProperty("selectionWeight").floatValue = 3f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("minion").objectReferenceValue = spiderling;
                so.FindProperty("countPerCast").intValue = 4;
                so.FindProperty("maxAlive").intValue = 8;
                so.FindProperty("spawnVfx").objectReferenceValue = vfx.PoisonBurst;
            });
            var pounce = root.AddComponent<EnemyLungeAttack>();
            SetProperties(pounce, so =>
            {
                so.FindProperty("range").floatValue = 12f;
                so.FindProperty("minRange").floatValue = 4f;
                so.FindProperty("cooldown").floatValue = 6f;
                so.FindProperty("damage").floatValue = 24f;
                so.FindProperty("windup").floatValue = 0.5f;
                so.FindProperty("dashSpeed").floatValue = 22f;
                so.FindProperty("dashDuration").floatValue = 0.45f;
                so.FindProperty("recovery").floatValue = 0.8f;
                so.FindProperty("hitRadius").floatValue = 1.8f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
            });
            FinishEnemy(root, spitAttack);
            return SavePrefab(root, Prefabs + "/Boss_SpiderQueen.prefab").GetComponent<EnemyActor>();
        }

        private static EnemyActor BuildSpiderling()
        {
            var def = EnemyDef("Enemy_Spiderling", "Araignée", health: 20f, speed: 7.5f, gold: 2, detection: 40f, feminine: true);
            var root = EnemyBase("Enemy_Spiderling", def, new Color(0.2f, 0.1f, 0.25f), out _);
            root.transform.localScale = Vector3.one * 0.55f;
            var attack = root.AddComponent<EnemyMeleeAttack>();
            SetProperties(attack, so =>
            {
                so.FindProperty("range").floatValue = 1.8f;
                so.FindProperty("cooldown").floatValue = 1.2f;
                so.FindProperty("damage").floatValue = 5f;
                so.FindProperty("windup").floatValue = 0.35f;
                so.FindProperty("hitRadius").floatValue = 1f;
            });
            FinishEnemy(root, attack);
            return SavePrefab(root, Prefabs + "/Enemy_Spiderling.prefab").GetComponent<EnemyActor>();
        }

        private static EnemyDefinition EnemyDef(string file, string displayName, float health, float speed, int gold, float detection,
            bool feminine = false)
        {
            var def = Asset<EnemyDefinition>($"{EnemiesFolder}/{file}.asset");
            def.feminineName = feminine;
            def.detectionRange = detection;
            def.isBoss = false;
            def.movementStyle = EnemyMovementStyle.Direct;
            def.flankRadius = 6f;
            def.flankAngle = new Vector2(100f, 160f);
            def.kiteRange = new Vector2(10f, 24f);
            def.retreatDistance = 8f;
            def.alertAlliesRadius = 10f;
            def.wanderRadius = 6f;
            def.controlResistance = 0f;
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
            root.AddComponent<StatusEffects>();
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

        // Regular enemies (not bosses): more aggressive than their base values below.
        private const float RegularAttackRate = 1.3f;   // cooldowns / 1.3
        private const float RegularDamage = 1.15f;
        private const float RegularSpeed = 1.15f;
        private const float RegularDetection = 1.4f;
        private const float RegularFlankRadius = 0.7f;  // flankers commit closer, sooner

        private static void FinishEnemy(GameObject root, EnemyAttack attack, System.Action<SerializedObject> dodge = null)
        {
            var actor = root.GetComponent<EnemyActor>();
            var def = actor != null ? actor.Definition : null;
            if (def != null && !def.isBoss)
            {
                def.moveSpeed *= RegularSpeed;
                def.detectionRange *= RegularDetection;
                def.flankRadius *= RegularFlankRadius;
                def.alertAlliesRadius *= RegularDetection;
                EditorUtility.SetDirty(def);
                foreach (var enemyAttack in root.GetComponents<EnemyAttack>())
                    SetProperties(enemyAttack, so =>
                    {
                        var cooldown = so.FindProperty("cooldown");
                        if (cooldown != null)
                            cooldown.floatValue /= RegularAttackRate;
                        var damage = so.FindProperty("damage");
                        if (damage != null)
                            damage.floatValue *= RegularDamage;
                    });
            }
            if (dodge != null)
                SetProperties(root.AddComponent<EnemyDodge>(), dodge);
            root.AddComponent<EnemyLeap>();
            root.AddComponent<EnemyAnimationDriver>(); // after every EnemyAttack/Dodge/Leap
            var brain = root.AddComponent<EnemyBrain>();
            SetProperties(brain, so => so.FindProperty("attack").objectReferenceValue = attack);
        }

        private static SpawnCard Card(string file, EnemyActor prefab, int cost, float weight, float minCoefficient, int minStage = 1)
        {
            var card = Asset<SpawnCard>($"{EnemiesFolder}/{file}.asset");
            card.prefab = prefab;
            card.cost = cost;
            card.weight = weight;
            card.minCoefficient = minCoefficient;
            card.minStage = minStage;
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

        private static void BuildScene(GameObject playerPrefab, NetworkPrefabsList prefabList, GameObject gameSystems, GameObject hud)
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

            var netGo = new GameObject("NetworkManager");
            var manager = netGo.AddComponent<NetworkManager>();
            var transport = netGo.AddComponent<UnityTransport>();
            manager.NetworkConfig ??= new NetworkConfig();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.PlayerPrefab = null; // spawned by GameBootstrap (also when coming from the lobby)
            // Only our list: Netcode's auto-generated default list would register the same prefabs twice.
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
            manager.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabList);
            // Direct testing: this NetworkManager is discarded when arriving from the main menu (PersistentNetworkManager).
            netGo.AddComponent<PersistentNetworkManager>();

            var bootstrapGo = new GameObject("GameBootstrap");
            var bootstrap = bootstrapGo.AddComponent<GameBootstrap>();
            SetProperties(bootstrap, so =>
            {
                so.FindProperty("playerPrefab").objectReferenceValue = playerPrefab.GetComponent<NetworkObject>();
                var list = so.FindProperty("serverPrefabs");
                list.arraySize = 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = gameSystems.GetComponent<NetworkObject>();
            });

            PrefabUtility.InstantiatePrefab(hud);
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            PostProcessBuilder.AddVolumes(scene, "Prototype");
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

            // Traversal test area: plateaus separated by a gap, and raised/floating platforms.
            var platformMat = Mat("M_Platform", new Color(0.5f, 0.45f, 0.35f));
            Platform(level, "Plateau_A", new Vector3(18f, 0.75f, 22f), new Vector3(10f, 1.5f, 10f), platformMat);
            Platform(level, "Plateau_B", new Vector3(32f, 0.75f, 22f), new Vector3(10f, 1.5f, 10f), platformMat);
            Platform(level, "Step", new Vector3(-14f, 0.6f, 8f), new Vector3(4f, 1.2f, 4f), platformMat);
            Platform(level, "FloatingPlatform", new Vector3(-14f, 2.6f, 15f), new Vector3(7f, 0.5f, 7f), platformMat);

            // Layout variants (one child picked per group and per stage).
            var bridges = new GameObject("Variants_GapCrossing");
            bridges.transform.SetParent(level.transform);
            bridges.AddComponent<LayoutVariantGroup>();
            var bridge = new GameObject("Bridge");
            bridge.transform.SetParent(bridges.transform);
            Platform(bridge, "Deck", new Vector3(25f, 1.35f, 22f), new Vector3(4.5f, 0.3f, 3f), platformMat);
            var ramp = new GameObject("Ramp");
            ramp.transform.SetParent(bridges.transform);
            Platform(ramp, "RampBlock", new Vector3(18f, 0.4f, 15f), new Vector3(4f, 0.8f, 4f), platformMat);

            var rocks = new GameObject("Variants_RockCluster");
            rocks.transform.SetParent(level.transform);
            SetProperties(rocks.AddComponent<LayoutVariantGroup>(), so => so.FindProperty("allowNone").boolValue = true);
            var clusterWest = new GameObject("West");
            clusterWest.transform.SetParent(rocks.transform);
            Platform(clusterWest, "RockA", new Vector3(-32f, 2f, -20f), new Vector3(6f, 4f, 6f), rockMat);
            Platform(clusterWest, "RockB", new Vector3(-27f, 1.2f, -14f), new Vector3(4f, 2.4f, 5f), rockMat);
            var clusterNorth = new GameObject("North");
            clusterNorth.transform.SetParent(rocks.transform);
            Platform(clusterNorth, "RockA", new Vector3(2f, 2f, 38f), new Vector3(8f, 4f, 4f), rockMat);
            Platform(clusterNorth, "RockB", new Vector3(10f, 1.5f, 34f), new Vector3(4f, 3f, 4f), rockMat);

            // Candidate spots: teleporter and merchant are placed on one of these (chests stay random).
            var spots = new GameObject("SpawnSpots");
            spots.transform.SetParent(level.transform);
            foreach (var position in new[] { new Vector3(-40f, 0f, -40f), new Vector3(40f, 0f, -38f), new Vector3(-38f, 0f, 42f), new Vector3(42f, 0f, 40f) })
                Spot(spots, SpawnSpotType.Teleporter, position);
            foreach (var position in new[] { new Vector3(-20f, 0f, 5f), new Vector3(22f, 0f, -12f), new Vector3(0f, 0f, 25f) })
                Spot(spots, SpawnSpotType.Merchant, position);
            foreach (var t in level.GetComponentsInChildren<Transform>())
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);

            level.AddComponent<StageBounds>();
            level.AddComponent<MapLayout>();
            level.AddComponent<MapLayoutClientSync>();
            var surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders; // rebaked at runtime per layout
            surface.BuildNavMesh();
            if (surface.navMeshData != null)
            {
                string navPath = Scenes + "/Prototype_NavMesh.asset";
                AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            }
        }

        private static void Spot(GameObject parent, SpawnSpotType type, Vector3 position)
        {
            var spot = new GameObject($"Spot_{type}");
            spot.transform.SetParent(parent.transform);
            spot.transform.position = position;
            SetProperties(spot.AddComponent<SpawnSpot>(), so => so.FindProperty("type").enumValueIndex = (int)type);
        }

        private static void Platform(GameObject level, string name, Vector3 position, Vector3 size, Material material)
        {
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = name;
            platform.transform.SetParent(level.transform);
            platform.transform.position = position;
            platform.transform.localScale = size;
            platform.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static GameObject BuildShrinePrefab()
        {
            var root = new GameObject("HealingShrine");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.up * 0.75f;
            body.transform.localScale = new Vector3(1f, 1.5f, 1f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("M_Shrine", new Color(0.3f, 1f, 0.5f));

            root.AddComponent<NetworkObject>();
            var shrine = root.AddComponent<HealingShrine>();
            SetProperties(shrine, so =>
            {
                so.FindProperty("displayName").stringValue = "Autel de soin";
                so.FindProperty("baseCost").uintValue = 15;
                so.FindProperty("maxUses").intValue = 3;
                so.FindProperty("costMultiplierPerUse").floatValue = 1.5f;
            });
            return SavePrefab(root, Prefabs + "/HealingShrine.prefab");
        }

        private static void LoadOrCreateMapEventsConfig()
        {
            string path = ResourcesFolder + "/MapEventsConfig.asset";
            if (AssetDatabase.LoadAssetAtPath<MapEventsConfig>(path) != null)
                return; // user-tuned: never overwritten
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<MapEventsConfig>(), path);
        }

        private static GameObject BuildPurificationTombPrefab()
        {
            var root = new GameObject("PurificationTomb");
            var stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stone.name = "Gravestone";
            stone.transform.SetParent(root.transform, false);
            stone.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            stone.transform.localScale = new Vector3(1.2f, 1.8f, 0.4f);
            stone.GetComponent<Renderer>().sharedMaterial = Mat("M_Tomb", new Color(0.35f, 0.33f, 0.4f));
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(0f, 0.1f, -1.2f);
            slab.transform.localScale = new Vector3(1.4f, 0.2f, 2.2f);
            slab.GetComponent<Renderer>().sharedMaterial = Mat("M_Tomb", new Color(0.35f, 0.33f, 0.4f));

            var ringGo = new GameObject("ZoneRing");
            ringGo.transform.SetParent(root.transform, false);
            var ring = ringGo.AddComponent<LineRenderer>();
            ring.widthMultiplier = 0.25f;
            ring.sharedMaterial = LineMat();
            ring.shadowCastingMode = ShadowCastingMode.Off;

            var heal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            heal.name = "HealZone";
            Object.DestroyImmediate(heal.GetComponent<Collider>());
            heal.transform.SetParent(root.transform, false);
            heal.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            heal.transform.localScale = new Vector3(16f, 0.01f, 16f);
            heal.GetComponent<Renderer>().sharedMaterial = Mat("M_TombHeal", new Color(0.35f, 1f, 0.5f), unlit: true);
            heal.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            heal.SetActive(false);

            root.AddComponent<NetworkObject>();
            var tomb = root.AddComponent<PurificationTomb>();
            SetProperties(tomb, so =>
            {
                so.FindProperty("zoneRing").objectReferenceValue = ring;
                so.FindProperty("healVisual").objectReferenceValue = heal;
            });
            return SavePrefab(root, Prefabs + "/MapEvent_PurificationTomb.prefab");
        }

        private static GameObject BuildChallengeAltarPrefab()
        {
            var root = new GameObject("ChallengeAltar");
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pedestal.name = "Pedestal";
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            pedestal.transform.localScale = new Vector3(1.6f, 0.5f, 1.6f);
            pedestal.GetComponent<Renderer>().sharedMaterial = Mat("M_ChallengeAltar", new Color(0.25f, 0.2f, 0.2f));
            var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flame.name = "Flame";
            Object.DestroyImmediate(flame.GetComponent<Collider>());
            flame.transform.SetParent(root.transform, false);
            flame.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            flame.transform.localScale = Vector3.one * 0.7f;
            flame.GetComponent<Renderer>().sharedMaterial = Mat("M_ChallengeAltarFlame", Color.white, unlit: true);

            root.AddComponent<NetworkObject>();
            var altar = root.AddComponent<ChallengeAltar>();
            SetProperties(altar, so =>
            {
                var renderers = so.FindProperty("glowRenderers");
                renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = flame.GetComponent<Renderer>();
            });
            return SavePrefab(root, Prefabs + "/MapEvent_ChallengeAltar.prefab");
        }

        private static GameObject BuildCursedChestPrefab()
        {
            var root = new GameObject("CursedChest");
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.4f, 0f);
            body.transform.localScale = new Vector3(1.3f, 0.8f, 0.9f);
            body.GetComponent<Renderer>().sharedMaterial = Mat("M_CursedChest", new Color(0.2f, 0.08f, 0.12f));
            var lid = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lid.name = "Lid";
            Object.DestroyImmediate(lid.GetComponent<Collider>());
            lid.transform.SetParent(root.transform, false);
            lid.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            lid.transform.localScale = new Vector3(1.35f, 0.25f, 0.95f);
            lid.GetComponent<Renderer>().sharedMaterial = Mat("M_CursedChestGlow", new Color(0.8f, 0.1f, 0.25f), unlit: true);

            root.AddComponent<NetworkObject>();
            var chest = root.AddComponent<CursedChest>();
            SetProperties(chest, so => so.FindProperty("closedVisual").objectReferenceValue = lid);
            return SavePrefab(root, Prefabs + "/MapEvent_CursedChest.prefab");
        }

        private static GameObject BuildGameSystemsPrefab(List<SpawnCard> cards, (GameObject prefab, int count)[] entries, GameObject teleporter,
            (MapEventType type, GameObject prefab)[] mapEvents)
        {
            var root = new GameObject("GameSystems");
            root.AddComponent<NetworkObject>();
            root.AddComponent<RunDifficulty>();
            root.AddComponent<ProjectileNetwork>();
            root.AddComponent<VfxNetwork>();
            root.AddComponent<GoldRewarder>();

            var director = root.AddComponent<CombatDirector>();
            SetProperties(director, so =>
            {
                var list = so.FindProperty("cards");
                list.arraySize = cards.Count;
                for (int i = 0; i < cards.Count; i++)
                    list.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
            });

            var interactables = root.AddComponent<InteractableSpawner>();
            SetProperties(interactables, so =>
            {
                var list = so.FindProperty("entries");
                list.arraySize = entries.Length;
                for (int i = 0; i < entries.Length; i++)
                {
                    var element = list.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("prefab").objectReferenceValue = entries[i].prefab.GetComponent<NetworkObject>();
                    element.FindPropertyRelative("count").intValue = entries[i].count;
                    bool isMerchant = entries[i].prefab.GetComponent<Merchant>() != null;
                    element.FindPropertyRelative("useSpawnSpots").boolValue = isMerchant;
                    element.FindPropertyRelative("spotType").enumValueIndex = (int)SpawnSpotType.Merchant;
                }
                var events = so.FindProperty("mapEvents");
                events.arraySize = mapEvents.Length;
                for (int i = 0; i < mapEvents.Length; i++)
                {
                    var element = events.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("type").enumValueIndex = (int)mapEvents[i].type;
                    element.FindPropertyRelative("prefab").objectReferenceValue = mapEvents[i].prefab.GetComponent<NetworkObject>();
                }
            });

            var stages = root.AddComponent<StageManager>();
            SetProperties(stages, so => so.FindProperty("teleporterPrefab").objectReferenceValue = teleporter.GetComponent<Teleporter>());

            return SavePrefab(root, Prefabs + "/GameSystems.prefab");
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
            if (def.visualPrefab == null || AssetDatabase.GetAssetPath(def.visualPrefab) == AssetDatabase.GetAssetPath(prefab))
                def.visualPrefab = prefab; // a visual plugged by hand is kept
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
            root.AddComponent<EffectSound>();
            return SavePrefab(root, $"{VfxFolder}/{name}.prefab");
        }

        /// <summary>Flat ground disc, no lifetime (elite auras).</summary>
        private static GameObject AuraPrefab(string name, float radius, Color color)
        {
            var root = new GameObject(name);
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(mesh.GetComponent<Collider>());
            mesh.transform.SetParent(root.transform, false);
            mesh.transform.localPosition = new Vector3(0f, 0.03f, 0f);
            mesh.transform.localScale = new Vector3(radius * 2f, 0.01f, radius * 2f);
            var renderer = mesh.GetComponent<Renderer>();
            renderer.sharedMaterial = Mat("M_" + name, color, unlit: true);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return SavePrefab(root, $"{VfxFolder}/{name}.prefab");
        }

        private static List<EliteAffix> BuildElites(VfxSet vfx)
        {
            var burn = AssetDatabase.LoadAssetAtPath<DotEffect>(ItemsFolder + "/Dot_Burn.asset");

            var burning = Asset<BurningAffix>($"{EnemiesFolder}/Elite_Burning.asset");
            Elite(burning, "ardent", "ardente", new Color(1f, 0.35f, 0.15f), vfx.AuraBurning);
            burning.burn = burn;
            burning.patchVfx = vfx.FirePatch;
            burning.dropSpacing = 1.5f;
            burning.trailDuration = 3f;
            burning.trailRadius = 1.3f;
            burning.trailDamage = 3f;
            burning.tickInterval = 0.5f;

            var blizzard = Asset<BlizzardAffix>($"{EnemiesFolder}/Elite_Blizzard.asset");
            Elite(blizzard, "glacial", "glaciale", new Color(0.55f, 0.85f, 1f), vfx.AuraBlizzard);
            blizzard.radius = 6f;
            blizzard.damagePerTick = 2f;
            blizzard.tickInterval = 0.5f;
            blizzard.chillDuration = 1.2f;

            var storm = Asset<StormAffix>($"{EnemiesFolder}/Elite_Storm.asset");
            Elite(storm, "foudroyant", "foudroyante", new Color(1f, 0.9f, 0.3f), vfx.AuraStorm);
            storm.interval = 4f;
            storm.strikeDelay = 1f;
            storm.strikeRadius = 2.5f;
            storm.strikeDamage = 12f;
            storm.maxRange = 40f;
            storm.telegraphVfx = vfx.Telegraph;
            storm.boltVfx = vfx.Lightning;
            storm.impactVfx = vfx.Shockwave;

            var spectral = Asset<SpectralAffix>($"{EnemiesFolder}/Elite_Spectral.asset");
            Elite(spectral, "éthéré", "éthérée", new Color(0.75f, 0.55f, 1f), vfx.AuraSpectral);
            spectral.phaseDuration = 1f;
            spectral.phaseCooldown = 4f;
            spectral.blinkInterval = 6f;
            spectral.blinkMinDistance = 12f;
            spectral.blinkDistanceFromTarget = new Vector2(3f, 5f);
            spectral.blinkVfx = vfx.Blink;

            var all = new List<EliteAffix> { burning, blizzard, storm, spectral };
            foreach (var affix in all)
            {
                EditorUtility.SetDirty(affix);
                ArtLibraryBuilder.RegisterElite(affix, $"Élite {affix.nameMasculine}");
            }
            return all;
        }

        private static void Elite(EliteAffix affix, string masculine, string feminine, Color tint, GameObject aura)
        {
            affix.nameMasculine = masculine;
            affix.nameFeminine = feminine;
            affix.tint = tint;
            affix.auraPrefab = aura;
            affix.healthMultiplier = 4f;
            affix.damageMultiplier = 2f;
            affix.costMultiplier = 6f;
            affix.goldMultiplier = 4f;
            affix.bossHealthMultiplier = 1.5f;
            affix.bossDamageMultiplier = 1.25f;
            affix.bossGoldMultiplier = 1.5f;
        }

        private const float ColossusWaveSpeed = 14f; // shared by the ring VFX and the shockwave attack

        private static GameObject RingPrefab(string name, Color color, float width, float speed)
        {
            var root = new GameObject(name);
            var line = root.AddComponent<LineRenderer>();
            line.widthMultiplier = width;
            line.sharedMaterial = LineMat();
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            var ring = root.AddComponent<ExpandingRing>();
            SetProperties(ring, so => so.FindProperty("speed").floatValue = speed);
            root.AddComponent<EffectSound>();
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
            root.AddComponent<EffectSound>();
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

        /// <summary>HDR multiplier of unlit (effect) materials under URP, above the bloom threshold.</summary>
        private const float GlowIntensity = 1.8f;

        internal static Material Mat(string name, Color color, bool unlit = false)
        {
            bool urp = GraphicsSettings.currentRenderPipeline != null;
            var shader = unlit
                ? Shader.Find(urp ? "Universal Render Pipeline/Unlit" : "Unlit/Color")
                : Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
            if (unlit && urp)
                color = new Color(color.r * GlowIntensity, color.g * GlowIntensity, color.b * GlowIntensity, color.a); // HDR: spells feed the bloom
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
        private static readonly HashSet<string> touchedAssets = new();

        /// <summary>Removes the assets of a folder that this build didn't create or update (renamed / removed content).</summary>
        private static void DeleteObsoleteAssets(string folder)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!touchedAssets.Contains(path))
                {
                    Debug.Log($"[Risk of Magic] Obsolete asset removed: {path}");
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        private static ArtLibraryBuilder.Category ArtCategory(string path)
        {
            if (path.StartsWith(VfxFolder + "/"))
                return ArtLibraryBuilder.Category.Vfx;
            if (path.StartsWith(ProjectilesFolder + "/"))
                return ArtLibraryBuilder.Category.Projectile;
            string file = Path.GetFileNameWithoutExtension(path);
            bool actor = file == "Player" || file == "GameSystems" || file.StartsWith("Enemy_") || file.StartsWith("Boss_");
            return path.StartsWith(Prefabs + "/") && !actor && !path.Substring(Prefabs.Length + 1).Contains("/")
                ? ArtLibraryBuilder.Category.Interactable
                : ArtLibraryBuilder.Category.None;
        }

        private static T Asset<T>(string path) where T : ScriptableObject
        {
            touchedAssets.Add(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            if (main != null)
            {
                // Only replace an asset of a genuinely different type, never one whose script failed to load.
                if (main is not ScriptableObject other || other.GetType() == typeof(ScriptableObject) || other.GetType() == typeof(MonoBehaviour))
                    throw new System.InvalidOperationException($"[Risk of Magic] {path} could not be loaded as {typeof(T).Name} (script error?). Build aborted.");
                AssetDatabase.DeleteAsset(path);
            }
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            ArtLibraryBuilder.Apply(root, ArtCategory(path), Path.GetFileNameWithoutExtension(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssignStableNetworkHash(prefab, path);
            return prefab;
        }

        /// <summary>
        /// Objects built in an unsaved scene get a GlobalObjectIdHash computed from that scene (0 or duplicates),
        /// which Netcode rejects. Derive a stable, unique hash from the prefab GUID instead.
        /// </summary>
        private static void AssignStableNetworkHash(GameObject prefab, string path)
        {
            if (!prefab.TryGetComponent(out NetworkObject networkObject))
                return;

            uint hash = 2166136261u;
            foreach (char c in AssetDatabase.AssetPathToGUID(path))
                hash = (hash ^ c) * 16777619u;
            if (hash == 0u)
                hash = 1u;

            var so = new SerializedObject(networkObject);
            var property = so.FindProperty("GlobalObjectIdHash");
            if (property == null)
            {
                Debug.LogWarning($"[Risk of Magic] Could not find GlobalObjectIdHash on {prefab.name}.");
                return;
            }
            property.uintValue = hash;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(prefab);
            AssetDatabase.SaveAssetIfDirty(prefab);
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
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true)); // the last added scene becomes the first one
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
