using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Enemies;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>Secret bosses (one per map, summoned by the map secret's altar). Health is ×MapSecret.bossHealthMultiplier.</summary>
    public static partial class PrototypeSceneBuilder
    {
        private static void SetAttack(EnemyAttack attack, System.Action<SerializedObject> edit) => SetProperties(attack, edit);

        /// <summary>Builds the 5 secret bosses (+ the thorn root minion), registers them and returns their cards.</summary>
        private static List<EnemyActor> BuildSecretBosses(ProjectileSet p, VfxSet vfx, NetworkPrefabsList prefabList, SpawnCard skeletonCard,
            DotEffect burn)
        {
            var root = BuildThornRoot();
            prefabList.Add(new NetworkPrefab { Prefab = root.gameObject });
            var rootCard = Card("SpawnCard_ThornRoot", root, 1, 0f, 0f);

            var bosses = new List<EnemyActor>
            {
                BuildCorruptedSylvan(p, vfx, rootCard),
                BuildLichKing(p, vfx, skeletonCard),
                BuildFrostWyvern(vfx),
                BuildTitanSmith(p, vfx, burn),
                BuildVoidWeaver(p, vfx)
            };
            foreach (var boss in bosses)
            {
                prefabList.Add(new NetworkPrefab { Prefab = boss.gameObject });
                Card($"SpawnCard_Secret_{boss.name.Replace("Secret_", string.Empty)}", boss, 1, 0f, 0f); // weight 0: never picked by the teleporter
            }
            return bosses;
        }

        private static EnemyActor BuildThornRoot()
        {
            var def = EnemyDef("Enemy_ThornRoot", "Racine épineuse", health: 140f, speed: 0f, gold: 0, detection: 12f, feminine: true);
            var go = EnemyBase("Enemy_ThornRoot", def, new Color(0.25f, 0.4f, 0.15f), out _);
            go.transform.localScale = new Vector3(0.8f, 1.2f, 0.8f);
            var attack = go.AddComponent<EnemyMeleeAttack>();
            SetAttack(attack, so =>
            {
                so.FindProperty("range").floatValue = 2.6f;
                so.FindProperty("cooldown").floatValue = 1.6f;
                so.FindProperty("damage").floatValue = 7f;
                so.FindProperty("windup").floatValue = 0.5f;
                so.FindProperty("hitRadius").floatValue = 1.6f;
            });
            FinishEnemy(go, attack);
            return SavePrefab(go, Prefabs + "/Enemy_ThornRoot.prefab").GetComponent<EnemyActor>();
        }

        // ---------------------------------------------------------------- Grim Forest

        private static EnemyActor BuildCorruptedSylvan(ProjectileSet p, VfxSet vfx, SpawnCard rootCard)
        {
            var def = BossDef("Secret_CorruptedSylvan", "Sylvain corrompu", health: 1500f, speed: 4.2f);
            var go = EnemyBase("Secret_CorruptedSylvan", def, new Color(0.22f, 0.32f, 0.12f), out _);
            go.transform.localScale = new Vector3(2.2f, 2.6f, 2.2f);

            var thorns = go.AddComponent<EnemyEruptionAttack>(); // primary: thorns burst under every player
            SetAttack(thorns, so =>
            {
                so.FindProperty("range").floatValue = 30f;
                so.FindProperty("cooldown").floatValue = 3.5f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("maxHeightDifference").floatValue = 12f;
                so.FindProperty("pattern").enumValueIndex = (int)EruptionPattern.UnderPlayers;
                so.FindProperty("damage").floatValue = 26f;
                so.FindProperty("radius").floatValue = 2.2f;
                so.FindProperty("delay").floatValue = 1.1f;
                so.FindProperty("count").intValue = 3;
                so.FindProperty("spacing").floatValue = 4f;
                so.FindProperty("applyStatus").boolValue = true;
                so.FindProperty("status").intValue = (int)StatusType.Chilled; // entangled: slowed
                so.FindProperty("statusDuration").floatValue = 1.5f;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("impactVfx").objectReferenceValue = vfx.PoisonBurst;
            });
            var seeds = go.AddComponent<EnemyArtilleryAttack>();
            SetAttack(seeds, so =>
            {
                so.FindProperty("range").floatValue = 32f;
                so.FindProperty("minRange").floatValue = 6f;
                so.FindProperty("cooldown").floatValue = 4.5f;
                so.FindProperty("damage").floatValue = 22f;
                so.FindProperty("damageType").intValue = (int)DamageType.Poison;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = p.Firebomb;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
            });
            var regen = go.AddComponent<EnemyRootRegenAttack>();
            SetAttack(regen, so =>
            {
                so.FindProperty("range").floatValue = 40f;
                so.FindProperty("cooldown").floatValue = 22f;
                so.FindProperty("selectionWeight").floatValue = 3f;
                so.FindProperty("maxHeightDifference").floatValue = 20f;
                so.FindProperty("rootCard").objectReferenceValue = rootCard;
                so.FindProperty("healPerSecond").floatValue = 0.03f;
                so.FindProperty("channelDuration").floatValue = 8f;
                so.FindProperty("spawnVfx").objectReferenceValue = vfx.PoisonCloud;
                so.FindProperty("healVfx").objectReferenceValue = vfx.RallyPulse;
            });
            FinishEnemy(go, thorns);
            return SavePrefab(go, Prefabs + "/Secret_CorruptedSylvan.prefab").GetComponent<EnemyActor>();
        }

        // ---------------------------------------------------------------- Necropolis

        private static EnemyActor BuildLichKing(ProjectileSet p, VfxSet vfx, SpawnCard skeletonCard)
        {
            var def = BossDef("Secret_LichKing", "Roi-liche", health: 1300f, speed: 4.5f);
            def.movementStyle = EnemyMovementStyle.Kite;
            def.kiteRange = new Vector2(10f, 22f);
            def.retreatDistance = 8f;
            EditorUtility.SetDirty(def);
            var go = EnemyBase("Secret_LichKing", def, new Color(0.35f, 0.45f, 0.6f), out _);
            go.transform.localScale = new Vector3(1.6f, 2.2f, 1.6f);

            var volley = go.AddComponent<EnemyProjectileAttack>(); // primary: fan of frost orbs
            SetAttack(volley, so =>
            {
                so.FindProperty("range").floatValue = 26f;
                so.FindProperty("cooldown").floatValue = 2.6f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("damage").floatValue = 13f;
                so.FindProperty("damageType").intValue = (int)DamageType.Frost;
                so.FindProperty("windup").floatValue = 0.7f;
                so.FindProperty("count").intValue = 5;
                so.FindProperty("spreadAngle").floatValue = 50f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = p.FrostOrb;
            });
            var raise = go.AddComponent<EnemySummonAttack>();
            SetAttack(raise, so =>
            {
                so.FindProperty("range").floatValue = 40f;
                so.FindProperty("cooldown").floatValue = 3f;
                so.FindProperty("selectionWeight").floatValue = 5f;
                so.FindProperty("maxHeightDifference").floatValue = 20f;
                so.FindProperty("minion").objectReferenceValue = skeletonCard;
                so.FindProperty("countPerCast").intValue = 4;
                so.FindProperty("maxAlive").intValue = 4;
                so.FindProperty("windup").floatValue = 1.2f;
                so.FindProperty("onlyBelowHealth").floatValue = 0.5f;
                so.FindProperty("maxCasts").intValue = 1;
                so.FindProperty("spawnVfx").objectReferenceValue = vfx.CursePulse;
            });
            var haunt = go.AddComponent<EnemyHauntAttack>();
            SetAttack(haunt, so =>
            {
                so.FindProperty("range").floatValue = 30f;
                so.FindProperty("cooldown").floatValue = 12f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("radius").floatValue = 3.5f;
                so.FindProperty("speed").floatValue = 3.2f;
                so.FindProperty("duration").floatValue = 8f;
                so.FindProperty("damagePerTick").floatValue = 4f;
                so.FindProperty("zoneVfx").objectReferenceValue = vfx.CursePulse;
            });
            FinishEnemy(go, volley);
            return SavePrefab(go, Prefabs + "/Secret_LichKing.prefab").GetComponent<EnemyActor>();
        }

        // ---------------------------------------------------------------- Mountain Peak

        private static EnemyActor BuildFrostWyvern(VfxSet vfx)
        {
            var def = BossDef("Secret_FrostWyvern", "Wyverne des glaces", health: 1400f, speed: 6.5f, feminine: true);
            var go = EnemyBase("Secret_FrostWyvern", def, new Color(0.6f, 0.8f, 0.95f), out _);
            go.transform.localScale = new Vector3(2.4f, 1.8f, 2.4f);

            var breath = go.AddComponent<EnemyConeAttack>(); // primary: freezing breath
            SetAttack(breath, so =>
            {
                so.FindProperty("range").floatValue = 10f;
                so.FindProperty("cooldown").floatValue = 5.5f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
                so.FindProperty("angle").floatValue = 50f;
                so.FindProperty("damagePerTick").floatValue = 6f;
                so.FindProperty("damageType").intValue = (int)DamageType.Frost;
                so.FindProperty("duration").floatValue = 2f;
                so.FindProperty("applyStatus").boolValue = true;
                so.FindProperty("status").intValue = (int)StatusType.Chilled;
                so.FindProperty("statusDuration").floatValue = 1.5f;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("tickVfx").objectReferenceValue = vfx.FrostBurst;
            });
            var dive = go.AddComponent<EnemyLungeAttack>();
            SetAttack(dive, so =>
            {
                so.FindProperty("range").floatValue = 22f;
                so.FindProperty("minRange").floatValue = 7f;
                so.FindProperty("cooldown").floatValue = 6f;
                so.FindProperty("damage").floatValue = 34f;
                so.FindProperty("windup").floatValue = 0.9f;
                so.FindProperty("dashSpeed").floatValue = 30f;
                so.FindProperty("dashDuration").floatValue = 0.6f;
                so.FindProperty("recovery").floatValue = 1f;
                so.FindProperty("hitRadius").floatValue = 2.4f;
                so.FindProperty("maxHeightDifference").floatValue = 6f;
            });
            var avalanche = go.AddComponent<EnemyEruptionAttack>();
            SetAttack(avalanche, so =>
            {
                so.FindProperty("range").floatValue = 30f;
                so.FindProperty("cooldown").floatValue = 8f;
                so.FindProperty("maxHeightDifference").floatValue = 12f;
                so.FindProperty("pattern").enumValueIndex = (int)EruptionPattern.Line;
                so.FindProperty("damage").floatValue = 30f;
                so.FindProperty("radius").floatValue = 2.6f;
                so.FindProperty("delay").floatValue = 1.2f;
                so.FindProperty("stepDelay").floatValue = 0.12f;
                so.FindProperty("count").intValue = 9;
                so.FindProperty("spacing").floatValue = 3.2f;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("impactVfx").objectReferenceValue = vfx.Shockwave;
            });
            FinishEnemy(go, breath);
            return SavePrefab(go, Prefabs + "/Secret_FrostWyvern.prefab").GetComponent<EnemyActor>();
        }

        // ---------------------------------------------------------------- Volcano Tomb

        private static EnemyActor BuildTitanSmith(ProjectileSet p, VfxSet vfx, DotEffect burn)
        {
            var def = BossDef("Secret_TitanSmith", "Forgeron titanesque", health: 1800f, speed: 4.5f);
            var go = EnemyBase("Secret_TitanSmith", def, new Color(0.45f, 0.25f, 0.15f), out _);
            go.transform.localScale = new Vector3(2.5f, 2.6f, 2.5f);

            var hammer = go.AddComponent<EnemyEruptionAttack>(); // primary: hammer slam under the target, leaves lava
            SetAttack(hammer, so =>
            {
                so.FindProperty("range").floatValue = 7f;
                so.FindProperty("cooldown").floatValue = 3f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("maxHeightDifference").floatValue = 4f;
                so.FindProperty("pattern").enumValueIndex = (int)EruptionPattern.UnderPlayers;
                so.FindProperty("damage").floatValue = 38f;
                so.FindProperty("damageType").intValue = (int)DamageType.Fire;
                so.FindProperty("radius").floatValue = 3.2f;
                so.FindProperty("windup").floatValue = 0.4f;
                so.FindProperty("delay").floatValue = 0.9f;
                so.FindProperty("count").intValue = 1;
                so.FindProperty("hazardDuration").floatValue = 6f;
                so.FindProperty("hazardDamagePerTick").floatValue = 5f;
                so.FindProperty("hazardVfx").objectReferenceValue = vfx.FirePatch;
                so.FindProperty("hazardVfxLifetime").floatValue = 3f;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("impactVfx").objectReferenceValue = vfx.FireBurst;
            });
            var anvil = go.AddComponent<EnemyArtilleryAttack>();
            SetAttack(anvil, so =>
            {
                so.FindProperty("range").floatValue = 34f;
                so.FindProperty("minRange").floatValue = 7f;
                so.FindProperty("cooldown").floatValue = 4f;
                so.FindProperty("damage").floatValue = 32f;
                so.FindProperty("damageType").intValue = (int)DamageType.Physical;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = p.Boulder;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
            });
            var quake = go.AddComponent<EnemyEruptionAttack>(); // ring of lava bursts around him
            SetAttack(quake, so =>
            {
                so.FindProperty("range").floatValue = 14f;
                so.FindProperty("cooldown").floatValue = 9f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
                so.FindProperty("pattern").enumValueIndex = (int)EruptionPattern.Ring;
                so.FindProperty("damage").floatValue = 28f;
                so.FindProperty("damageType").intValue = (int)DamageType.Fire;
                so.FindProperty("radius").floatValue = 2.4f;
                so.FindProperty("count").intValue = 8;
                so.FindProperty("spacing").floatValue = 6f;
                so.FindProperty("hazardDuration").floatValue = 4f;
                so.FindProperty("hazardDamagePerTick").floatValue = 4f;
                so.FindProperty("hazardVfx").objectReferenceValue = vfx.FirePatch;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("impactVfx").objectReferenceValue = vfx.FireBurst;
            });
            var enrage = go.AddComponent<EnemyEnrage>();
            SetProperties(enrage, so =>
            {
                so.FindProperty("healthThreshold").floatValue = 0.3f;
                so.FindProperty("attackRate").floatValue = 1.5f;
                so.FindProperty("enrageVfx").objectReferenceValue = vfx.FireBurst;
            });
            FinishEnemy(go, hammer);
            return SavePrefab(go, Prefabs + "/Secret_TitanSmith.prefab").GetComponent<EnemyActor>();
        }

        // ---------------------------------------------------------------- Chaos Sanctum

        private static EnemyActor BuildVoidWeaver(ProjectileSet p, VfxSet vfx)
        {
            var def = BossDef("Secret_VoidWeaver", "Tisseur du néant", health: 1300f, speed: 5f);
            def.movementStyle = EnemyMovementStyle.Kite;
            def.kiteRange = new Vector2(9f, 20f);
            def.retreatDistance = 7f;
            EditorUtility.SetDirty(def);
            var go = EnemyBase("Secret_VoidWeaver", def, new Color(0.4f, 0.15f, 0.55f), out _);
            go.transform.localScale = new Vector3(1.6f, 2.3f, 1.6f);

            var bolts = go.AddComponent<EnemyProjectileAttack>(); // primary: homing void bolts
            SetAttack(bolts, so =>
            {
                so.FindProperty("range").floatValue = 26f;
                so.FindProperty("cooldown").floatValue = 2.4f;
                so.FindProperty("damage").floatValue = 12f;
                so.FindProperty("damageType").intValue = (int)DamageType.Arcane;
                so.FindProperty("windup").floatValue = 0.6f;
                so.FindProperty("count").intValue = 3;
                so.FindProperty("spreadAngle").floatValue = 30f;
                so.FindProperty("homing").boolValue = true;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("projectile").objectReferenceValue = p.ArcaneBolt;
            });
            var blink = go.AddComponent<EnemyBlinkAttack>();
            SetAttack(blink, so =>
            {
                so.FindProperty("range").floatValue = 40f;
                so.FindProperty("cooldown").floatValue = 7f;
                so.FindProperty("maxHeightDifference").floatValue = 20f;
                so.FindProperty("blinkVfx").objectReferenceValue = vfx.Blink;
            });
            var beams = go.AddComponent<EnemyBeamAttack>();
            SetAttack(beams, so =>
            {
                so.FindProperty("range").floatValue = 18f;
                so.FindProperty("cooldown").floatValue = 10f;
                so.FindProperty("selectionWeight").floatValue = 1.5f;
                so.FindProperty("maxHeightDifference").floatValue = 5f;
                so.FindProperty("beams").intValue = 4;
                so.FindProperty("length").floatValue = 22f;
                so.FindProperty("rotationSpeed").floatValue = 35f;
                so.FindProperty("duration").floatValue = 4f;
                so.FindProperty("damagePerTick").floatValue = 6f;
                so.FindProperty("previewVfx").objectReferenceValue = vfx.Lightning;
                so.FindProperty("beamVfx").objectReferenceValue = vfx.VoidBeam;
            });
            var rifts = go.AddComponent<EnemyVortexAttack>();
            SetAttack(rifts, so =>
            {
                so.FindProperty("range").floatValue = 30f;
                so.FindProperty("cooldown").floatValue = 12f;
                so.FindProperty("maxHeightDifference").floatValue = 15f;
                so.FindProperty("telegraphVfx").objectReferenceValue = vfx.Telegraph;
                so.FindProperty("riftVfx").objectReferenceValue = vfx.TimePulse;
            });
            FinishEnemy(go, bolts);
            return SavePrefab(go, Prefabs + "/Secret_VoidWeaver.prefab").GetComponent<EnemyActor>();
        }
    }
}
