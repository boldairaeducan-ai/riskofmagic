using System.Collections.Generic;
using System.Linq;
using System.Text;
using RiskOfMagic.Characters;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
using RiskOfMagic.Items.Types;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Skills;
using RiskOfMagic.Skills.Types;
using UnityEditor;
using UnityEngine;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Tools → Risk of Magic → Balance Simulator: stage-by-stage estimate from the real assets (difficulty, director,
    /// spawn cards, chests, items, character, experience curve). Offense = enemy health arriving per second / team DPS,
    /// defense = enemy damage / player health, both normalized to stage 1 (1 = as hard as the start).
    /// It is a model (averages, every spawned enemy killed, all gold spent on chests): use it to compare tunings.
    /// </summary>
    public class BalanceSimulator : EditorWindow
    {
        private const string GameSystemsPath = "Assets/_Project/Prefabs/GameSystems.prefab";
        private const string PlayerPath = "Assets/_Project/Prefabs/Player.prefab";

        private int players = 1;
        private bool hard;
        private float stageMinutes = 7f;
        private int stages = 10;
        private int characterIndex;
        private float goldSpentOnChests = 0.8f;
        private bool includeLevels = true;
        private Vector2 scroll;
        private readonly List<Row> rows = new();
        private string error;

        private struct Row
        {
            public int Stage;
            public float Minutes, Coefficient, EnemyHealthMult, EnemyDamageMult, EnemiesPerMinute, PlayerLevel, Items;
            public float PlayerDpsMult, PlayerHealthMult, Offense, Defense;
            public int EnemyLevel;
        }

        [MenuItem("Tools/Risk of Magic/Balance Simulator")]
        public static void Open() => GetWindow<BalanceSimulator>("Équilibrage");

        private void OnEnable() => Simulate();

        private void OnGUI()
        {
            var catalog = Resources.Load<GameCatalog>(GameCatalog.ResourcePath);
            EditorGUI.BeginChangeCheck();
            players = EditorGUILayout.IntSlider("Joueurs", players, 1, 4);
            hard = EditorGUILayout.Toggle("Difficile", hard);
            stageMinutes = EditorGUILayout.Slider("Minutes par étape", stageMinutes, 3f, 15f);
            stages = EditorGUILayout.IntSlider("Étapes", stages, 2, 15);
            if (catalog != null && catalog.Characters.Count > 0)
            {
                string[] names = catalog.Characters.Select(c => c != null ? c.displayName : "?").ToArray();
                characterIndex = EditorGUILayout.Popup("Personnage", Mathf.Clamp(characterIndex, 0, names.Length - 1), names);
            }
            goldSpentOnChests = EditorGUILayout.Slider("Or dépensé en coffres", goldSpentOnChests, 0f, 1f);
            includeLevels = EditorGUILayout.Toggle("Niveaux du joueur", includeLevels);
            if (EditorGUI.EndChangeCheck() || GUILayout.Button("Recalculer (après un Build)"))
                Simulate();

            if (!string.IsNullOrEmpty(error))
            {
                EditorGUILayout.HelpBox(error, MessageType.Error);
                return;
            }

            EditorGUILayout.HelpBox("Offensive = PV ennemis arrivant par seconde / DPS de l'équipe. Défense = dégâts ennemis / PV du joueur.\n" +
                                    "Normalisés à l'étape 1 (1 = aussi dur qu'au début). Zone verte visée : 0,8 – 2.", MessageType.Info);
            DrawGraph(GUILayoutUtility.GetRect(position.width - 20f, 220f));
            DrawTable();
            if (GUILayout.Button("Copier en CSV"))
                EditorGUIUtility.systemCopyBuffer = ToCsv();
        }

        // ---------------------------------------------------------------- Model

        private void Simulate()
        {
            rows.Clear();
            error = null;
            var catalog = Resources.Load<GameCatalog>(GameCatalog.ResourcePath);
            var systems = AssetDatabase.LoadAssetAtPath<GameObject>(GameSystemsPath);
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            if (catalog == null || systems == null || player == null || catalog.Characters.Count == 0)
            {
                error = "Assets manquants : lance Tools → Risk of Magic → Build Prototype Scene.";
                return;
            }

            if (systems.GetComponent<RunDifficulty>() == null || systems.GetComponent<CombatDirector>() == null
                || systems.GetComponent<InteractableSpawner>() == null)
            {
                error = "Prefab GameSystems incomplet : relance Tools → Risk of Magic → Build Prototype Scene.";
                return;
            }

            // Difficulty (RunDifficulty serialized values)
            var difficulty = new SerializedObject(systems.GetComponent<RunDifficulty>());
            float scalingPerMinute = difficulty.FindProperty("scalingPerMinute").floatValue;
            float playerFactorPerPlayer = difficulty.FindProperty("playerFactorPerPlayer").floatValue;
            float stageMultiplier = difficulty.FindProperty("stageMultiplier").floatValue;
            var startProperty = difficulty.FindProperty("startingCoefficient");
            float startingCoefficient = startProperty != null ? startProperty.floatValue : 1f;
            float levelStep = difficulty.FindProperty("levelStep").floatValue;
            float hardCredits = difficulty.FindProperty("hardDirectorCreditMultiplier").floatValue;
            float hardUnlock = difficulty.FindProperty("hardUnlockMultiplier").floatValue;
            int difficultyValue = hard ? (int)DifficultyLevel.Hard : (int)DifficultyLevel.Normal;

            // Director
            var director = new SerializedObject(systems.GetComponent<CombatDirector>());
            float creditMultiplier = director.FindProperty("creditMultiplier").floatValue;
            float populationCredits = director.FindProperty("stagePopulationCredits").floatValue;
            var cards = new List<SpawnCard>();
            var cardList = director.FindProperty("cards");
            for (int i = 0; i < cardList.arraySize; i++)
                if (cardList.GetArrayElementAtIndex(i).objectReferenceValue is SpawnCard card && card.prefab != null && card.prefab.Definition != null)
                    cards.Add(card);

            // Chests (InteractableSpawner)
            var spawner = new SerializedObject(systems.GetComponent<InteractableSpawner>());
            var entries = spawner.FindProperty("entries");
            float chestCount = 0f, chestCostSum = 0f;
            var rarity = new float[4];
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var prefab = entry.FindPropertyRelative("prefab").objectReferenceValue as Unity.Netcode.NetworkObject;
                int count = entry.FindPropertyRelative("count").intValue;
                var chest = prefab != null ? prefab.GetComponent<ChestInteractable>() : null;
                if (chest == null || count <= 0)
                    continue;
                var chestSo = new SerializedObject(chest);
                chestCount += count;
                chestCostSum += chestSo.FindProperty("baseCost").uintValue * count;
                var weights = chestSo.FindProperty("rarityWeights");
                rarity[0] += weights.FindPropertyRelative("common").floatValue * count;
                rarity[1] += weights.FindPropertyRelative("uncommon").floatValue * count;
                rarity[2] += weights.FindPropertyRelative("rare").floatValue * count;
                rarity[3] += weights.FindPropertyRelative("legendary").floatValue * count;
            }
            float avgChestBaseCost = chestCount > 0f ? chestCostSum / chestCount : 30f;
            float rarityTotal = rarity.Sum();
            const float chestCostExponent = 1.25f;

            // Expected value of one chest item
            var perItem = ExpectedItem(catalog, rarity, rarityTotal);

            // Character
            var character = catalog.GetCharacter(Mathf.Clamp(characterIndex, 0, catalog.Characters.Count - 1));
            float primaryDps = PrimaryDps(character.primary);

            // Experience
            var experienceComponent = player.GetComponent<PlayerExperience>();
            if (experienceComponent == null)
            {
                error = "Prefab du joueur obsolète (sans PlayerExperience) : relance Tools → Risk of Magic → Build Prototype Scene.";
                return;
            }
            var experience = new SerializedObject(experienceComponent);
            float xpPerGold = experience.FindProperty("experiencePerGold").floatValue;
            float firstLevel = experience.FindProperty("firstLevelExperience").floatValue;
            float growth = experience.FindProperty("levelGrowth").floatValue;

            float playerFactor = 1f + playerFactorPerPlayer * (players - 1);
            float timeFactor = scalingPerMinute * difficultyValue * Mathf.Pow(playerFactor, 0.2f);
            float totalGold = 0f, items = 0f;
            Row baseline = default;

            for (int s = 1; s <= stages; s++)
            {
                float midMinutes = (s - 0.5f) * stageMinutes;
                float stageFactor = Mathf.Pow(stageMultiplier, s - 1);
                float coefficient = (playerFactor * startingCoefficient + midMinutes * timeFactor) * stageFactor;
                float startCoefficient = (playerFactor * startingCoefficient + (s - 1) * stageMinutes * timeFactor) * stageFactor;
                float progress = coefficient / playerFactor * (hard ? hardUnlock : 1f);
                int enemyLevel = 1 + Mathf.FloorToInt((coefficient - playerFactor) / levelStep);

                // Unlocked enemies, weighted
                float weight = 0f, cost = 0f, health = 0f, gold = 0f, healthPerLevel = 0f, damagePerLevel = 0f;
                foreach (var card in cards)
                {
                    if (card.minCoefficient > progress || card.weight <= 0f)
                        continue;
                    var def = card.prefab.Definition;
                    weight += card.weight;
                    cost += card.cost * card.weight;
                    health += def.maxHealth * card.weight;
                    gold += def.goldReward * card.weight;
                    healthPerLevel += def.healthPerLevel * card.weight;
                    damagePerLevel += def.damagePerLevel * card.weight;
                }
                if (weight <= 0f)
                {
                    error = "Aucune carte de spawn débloquée.";
                    return;
                }
                cost /= weight; health /= weight; gold /= weight; healthPerLevel /= weight; damagePerLevel /= weight;

                float enemyHealthMult = 1f + healthPerLevel * (enemyLevel - 1);
                float enemyDamageMult = 1f + damagePerLevel * (enemyLevel - 1);
                // CombatDirector.Update credit income
                float creditsPerSecond = creditMultiplier * (hard ? hardCredits : 1f) * 0.75f * (1f + 0.4f * coefficient) * (players + 1) * 0.5f;
                float spawnsPerSecond = creditsPerSecond / cost;
                float stageSeconds = stageMinutes * 60f;
                float kills = spawnsPerSecond * stageSeconds + populationCredits * Mathf.Sqrt(coefficient) / cost;
                float stageGold = kills * gold * coefficient; // every player gets the full reward
                totalGold += stageGold;

                // Items: gold spent on chests (cost locked at spawn), capped by the chests of the map shared by the team
                float chestCost = avgChestBaseCost * Mathf.Pow(startCoefficient, chestCostExponent);
                float bought = Mathf.Min(stageGold * goldSpentOnChests / chestCost, chestCount / players);
                float itemsMid = items + bought * 0.5f;
                items += bought;

                int level = includeLevels ? LevelFor(totalGold * xpPerGold - stageGold * xpPerGold * 0.5f, firstLevel, growth) : 1;
                float levelDamage = 1f + character.damagePerLevel * (level - 1);
                float levelHealth = 1f + character.healthPerLevel * (level - 1);

                float dpsMult = levelDamage * (1f + perItem.AttackSpeed * itemsMid) * (1f + perItem.Damage * itemsMid)
                                * (1f + Mathf.Min(1f, character.critChance + perItem.Crit * itemsMid)) / (1f + character.critChance)
                                * (1f + perItem.Proc * itemsMid);
                float healthMult = (levelHealth * character.maxHealth + perItem.FlatHealth * itemsMid) / character.maxHealth
                                   / Mathf.Pow(Mathf.Max(0.05f, 1f - perItem.DamageReduction), itemsMid);

                float pressure = spawnsPerSecond * health * enemyHealthMult;
                float teamDps = players * character.baseDamage * primaryDps * dpsMult;
                var row = new Row
                {
                    Stage = s,
                    Minutes = s * stageMinutes,
                    Coefficient = coefficient,
                    EnemyLevel = enemyLevel,
                    EnemyHealthMult = enemyHealthMult,
                    EnemyDamageMult = enemyDamageMult,
                    EnemiesPerMinute = spawnsPerSecond * 60f,
                    PlayerLevel = level,
                    Items = itemsMid,
                    PlayerDpsMult = dpsMult,
                    PlayerHealthMult = healthMult,
                    Offense = pressure / teamDps,
                    Defense = enemyDamageMult / healthMult
                };
                if (s == 1)
                    baseline = row;
                row.Offense /= baseline.Offense;
                row.Defense /= baseline.Defense;
                rows.Add(row);
            }
            Repaint();
        }

        private static int LevelFor(float experience, float firstLevel, float growth)
        {
            int level = 1;
            while (level < 99 && experience >= firstLevel * (Mathf.Pow(growth, level) - 1f) / (growth - 1f))
                level++;
            return level;
        }

        /// <summary>Damage coefficient per second of the primary skill at attack speed 1 (rough).</summary>
        private static float PrimaryDps(SkillDefinition skill)
        {
            if (skill == null)
                return 1f;
            float cast = Mathf.Max(0.1f, skill.castDuration);
            return skill switch
            {
                ProjectileSkill p => p.damageCoefficient * p.count * p.burstCount / cast,
                MeleeComboSkill m when m.steps.Length > 0 => m.steps.Average(step => step.damageCoefficient) / cast,
                HomingVolleySkill h => h.damageCoefficient * h.maxTargets / (h.maxChargeTime + cast),
                _ => 1f / cast
            };
        }

        private struct ItemValue
        {
            public float AttackSpeed, Damage, Crit, Proc, FlatHealth, DamageReduction;
        }

        /// <summary>Expected first-stack value of one chest item (rarity weights of the map's chests, uniform within a rarity).</summary>
        private static ItemValue ExpectedItem(GameCatalog catalog, float[] rarity, float rarityTotal)
        {
            var result = new ItemValue();
            if (rarityTotal <= 0f)
                return result;
            for (int r = 0; r < 4; r++)
            {
                var pool = catalog.Items.Where(i => i != null && (int)i.rarity == r).ToList();
                if (pool.Count == 0 || rarity[r] <= 0f)
                    continue;
                float share = rarity[r] / rarityTotal / pool.Count;
                foreach (var item in pool)
                {
                    var v = Value(item);
                    result.AttackSpeed += v.AttackSpeed * share;
                    result.Damage += v.Damage * share;
                    result.Crit += v.Crit * share;
                    result.Proc += v.Proc * share;
                    result.FlatHealth += v.FlatHealth * share;
                    result.DamageReduction += v.DamageReduction * share;
                }
            }
            return result;
        }

        /// <summary>Rough first-stack offensive/defensive value of an item (fraction of base output).</summary>
        private static ItemValue Value(ItemDefinition item)
        {
            var v = new ItemValue();
            foreach (var m in item.modifiers)
            {
                switch (m.stat)
                {
                    case StatType.AttackSpeed: v.AttackSpeed += m.valuePerStack; break;
                    case StatType.Damage: v.Damage += m.valuePerStack; break;
                    case StatType.CritChance: v.Crit += m.valuePerStack; break;
                    case StatType.CooldownReduction: v.Damage += m.valuePerStack * 0.5f; break;
                    case StatType.MaxHealth: v.FlatHealth += m.valuePerStack; break;
                    case StatType.DamageTakenReduction: v.DamageReduction += m.valuePerStack; break;
                    case StatType.FlatDamageReduction: v.DamageReduction += 0.04f * m.valuePerStack / 5f; break;
                }
            }
            switch (item)
            {
                case DotOnHitItem d: v.Proc += d.chanceFirstStack * 2.4f * d.baseDamageCoefficient; break;
                case ChainOnHitItem c: v.Proc += c.chance * c.damageFraction * c.targetsFirstStack * 0.5f; break;
                case ProjectileOnHitItem p: v.Proc += p.chance * p.damageFractionPerStack; break;
                case ExplodeOnHitItem e: v.Proc += e.damageFraction * 1.5f; break;
                case DoubleHitItem d: v.Proc += d.chanceFirstStack; break;
                case FreezeOnHitItem f: v.Proc += f.chanceFirstStack * f.damageCoefficient; break;
                case MeteorOnHitItem m: v.Proc += m.chanceFirstStack * m.damageCoefficient * 2f; break;
                case ExecutionerItem x: v.Damage += x.bonusPerStack * (x.aboveThreshold ? 0.2f : 0.3f); break;
                case AdrenalineItem a: v.AttackSpeed += a.bonusFirstStack * 0.5f; break;
                case SoulHarvestItem s: v.Damage += s.maxPercent / 100f * 0.5f; break;
                case BannerItem: v.Damage += 0.2f; break;
                case WarHornItem: v.AttackSpeed += 0.3f; break;
                case ExplodeOnKillItem k: v.Proc += k.coefficientFirstStack * 0.15f; break;
                case DotAreaOnKillItem k: v.Proc += k.baseDamageCoefficient * 1.5f * 0.15f; break;
                case ShockwaveEveryHitsItem w: v.Proc += w.coefficientFirstStack / w.hitsRequired; break;
                case HealOnKillItem: v.FlatHealth += 10f; break;
            }
            return v;
        }

        // ---------------------------------------------------------------- Display

        private void DrawGraph(Rect rect)
        {
            EditorGUI.DrawRect(rect, new Color(0.13f, 0.13f, 0.15f));
            if (rows.Count < 2)
                return;
            float maxValue = Mathf.Max(3f, rows.Max(r => Mathf.Max(r.Offense, r.Defense)) * 1.1f);
            Vector2 Point(int i, float value) => new(
                rect.x + 30f + (rect.width - 40f) * i / (rows.Count - 1),
                rect.yMax - 10f - (rect.height - 20f) * Mathf.Clamp01(value / maxValue));

            // Target band 0.8 – 2
            float yTop = Point(0, 2f).y, yBottom = Point(0, 0.8f).y;
            EditorGUI.DrawRect(new Rect(rect.x + 30f, yTop, rect.width - 40f, yBottom - yTop), new Color(0.2f, 0.45f, 0.25f, 0.35f));
            GUI.Label(new Rect(rect.x + 2f, Point(0, 1f).y - 8f, 30f, 16f), "1");
            GUI.Label(new Rect(rect.x + 2f, Point(0, 2f).y - 8f, 30f, 16f), "2");
            GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, 40f, 16f), maxValue.ToString("0.0"));

            Handles.BeginGUI();
            DrawLine(rows.Select((r, i) => (Vector3)Point(i, r.Offense)).ToArray(), new Color(1f, 0.45f, 0.3f));
            DrawLine(rows.Select((r, i) => (Vector3)Point(i, r.Defense)).ToArray(), new Color(0.4f, 0.7f, 1f));
            Handles.EndGUI();
            GUI.Label(new Rect(rect.xMax - 220f, rect.y + 4f, 220f, 18f), "<color=#FF7350>— Offensive</color>   <color=#66B3FF>— Défense</color>",
                new GUIStyle(EditorStyles.label) { richText = true });
        }

        private static void DrawLine(Vector3[] points, Color color)
        {
            Handles.color = color;
            Handles.DrawAAPolyLine(3f, points);
        }

        private void DrawTable()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            string[] headers = { "Étape", "Min", "Coef", "Niv. ennemi", "PV ×", "Dégâts ×", "Ennemis/min", "Niv. joueur", "Objets", "DPS ×", "PV joueur ×", "Offensive", "Défense" };
            EditorGUILayout.BeginHorizontal();
            foreach (var header in headers)
                GUILayout.Label(header, EditorStyles.boldLabel, GUILayout.Width(78f));
            EditorGUILayout.EndHorizontal();
            foreach (var r in rows)
            {
                EditorGUILayout.BeginHorizontal();
                Cell(r.Stage.ToString());
                Cell(r.Minutes.ToString("0"));
                Cell(r.Coefficient.ToString("0.00"));
                Cell(r.EnemyLevel.ToString());
                Cell(r.EnemyHealthMult.ToString("0.0"));
                Cell(r.EnemyDamageMult.ToString("0.0"));
                Cell(r.EnemiesPerMinute.ToString("0.0"));
                Cell(r.PlayerLevel.ToString("0"));
                Cell(r.Items.ToString("0.0"));
                Cell(r.PlayerDpsMult.ToString("0.0"));
                Cell(r.PlayerHealthMult.ToString("0.0"));
                Cell(Colored(r.Offense));
                Cell(Colored(r.Defense));
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
        }

        private static string Colored(float value)
        {
            string color = value > 2f ? "#FF6060" : value < 0.8f ? "#60A0FF" : "#70D070";
            return $"<color={color}>{value:0.00}</color>";
        }

        private static void Cell(string text) => GUILayout.Label(text, new GUIStyle(EditorStyles.label) { richText = true }, GUILayout.Width(78f));

        private string ToCsv()
        {
            var builder = new StringBuilder("stage;minutes;coefficient;enemyLevel;enemyHealth;enemyDamage;enemiesPerMinute;playerLevel;items;dps;playerHealth;offense;defense\n");
            foreach (var r in rows)
                builder.AppendLine($"{r.Stage};{r.Minutes:0};{r.Coefficient:0.00};{r.EnemyLevel};{r.EnemyHealthMult:0.00};{r.EnemyDamageMult:0.00};" +
                                   $"{r.EnemiesPerMinute:0.0};{r.PlayerLevel:0};{r.Items:0.0};{r.PlayerDpsMult:0.00};{r.PlayerHealthMult:0.00};{r.Offense:0.00};{r.Defense:0.00}");
            return builder.ToString();
        }
    }
}
