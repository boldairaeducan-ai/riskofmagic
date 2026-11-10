using System;
using System.Collections.Generic;
using System.IO;
using RiskOfMagic.Combat;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies.Elites;
using RiskOfMagic.Items;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// Keeps Resources/ArtLibrary.asset in sync (creates it once, appends missing entries, never clears a field) and
    /// tags the generated prefabs with an ArtSlot (the art itself is applied at runtime).
    /// </summary>
    public static class ArtLibraryBuilder
    {
        public const string AssetPath = "Assets/_Project/Resources/ArtLibrary.asset";

        public enum Category
        {
            None,
            Vfx,
            Projectile,
            Interactable
        }

        private static readonly Dictionary<StatusType, (string name, bool visible, bool debuff)> StatusNames = new()
        {
            { StatusType.Curse, ("Malédiction", true, true) },
            { StatusType.Rally, ("Ralliement", true, false) },
            { StatusType.Warhorn, ("Cor de guerre", true, false) },
            { StatusType.Chilled, ("Gel", true, true) },
            { StatusType.Withered, ("Flétrissement", true, true) },
            { StatusType.Channeling, ("Canalisation", false, false) },
            { StatusType.Rooted, ("Enracinement", true, true) },
            { StatusType.Adrenaline, ("Adrénaline", true, false) },
            { StatusType.Banner, ("Bannière de guerre", true, false) },
            { StatusType.SoulHarvest, ("Moisson d'âmes", true, false) },
            { StatusType.Stealth, ("Furtivité", true, false) },
            { StatusType.Marked, ("Marqué", true, true) },
            { StatusType.Ambush, ("Embuscade", true, false) },
            { StatusType.RuneBlessing, ("Bénédiction runique", true, false) },
        };

        private static readonly Dictionary<ItemRarity, string> RarityNames = new()
        {
            { ItemRarity.Common, "Commun" },
            { ItemRarity.Uncommon, "Inhabituel" },
            { ItemRarity.Rare, "Rare" },
            { ItemRarity.Legendary, "Légendaire" },
            { ItemRarity.Boss, "Boss" },
        };

        [MenuItem("Tools/Risk of Magic/Select Art Library")]
        private static void SelectFromMenu()
        {
            Selection.activeObject = Library;
            EditorGUIUtility.PingObject(Library);
        }

        private static ArtLibrary library;

        public static ArtLibrary Library
        {
            get
            {
                if (library != null)
                    return library;
                library = AssetDatabase.LoadAssetAtPath<ArtLibrary>(AssetPath);
                if (library == null)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(AssetPath));
                    library = ScriptableObject.CreateInstance<ArtLibrary>();
                    AssetDatabase.CreateAsset(library, AssetPath);
                }
                SyncIcons(library);
                return library;
            }
        }

        /// <summary>One entry per status and rarity (new enum values are appended).</summary>
        private static void SyncIcons(ArtLibrary target)
        {
            bool dirty = false;
            foreach (StatusType type in Enum.GetValues(typeof(StatusType)))
            {
                if (target.GetStatus(type) != null)
                    continue;
                var info = StatusNames.TryGetValue(type, out var known) ? known : (type.ToString(), true, false);
                target.statuses.Add(new ArtLibrary.StatusArt { name = info.Item1, status = type, visible = info.Item2, debuff = info.Item3 });
                dirty = true;
            }
            foreach (ItemRarity rarity in Enum.GetValues(typeof(ItemRarity)))
            {
                if (target.rarities.Exists(r => r.rarity == rarity))
                    continue;
                target.rarities.Add(new ArtLibrary.RarityArt { name = RarityNames.TryGetValue(rarity, out var n) ? n : rarity.ToString(), rarity = rarity });
                dirty = true;
            }
            foreach (var (type, name) in new[] { (DamageType.Poison, "Poison"), (DamageType.Bleed, "Saignement"), (DamageType.Fire, "Brûlure") })
            {
                if (target.GetDot(type) != null)
                    continue;
                target.dots.Add(new ArtLibrary.DotArt { name = name, type = type });
                dirty = true;
            }
            if (dirty)
                EditorUtility.SetDirty(target);
        }

        /// <summary>One icon slot per elite affix (called by the builder).</summary>
        public static void RegisterElite(EliteAffix affix, string displayName)
        {
            var entry = Library.elites.Find(e => e.affix == affix);
            if (entry == null)
            {
                Library.elites.Add(new ArtLibrary.EliteArt { name = displayName, affix = affix });
                EditorUtility.SetDirty(Library);
            }
            else if (entry.name != displayName)
            {
                entry.name = displayName;
                EditorUtility.SetDirty(Library);
            }
        }

        private static List<ArtLibrary.PrefabArt> ListFor(Category category) => category switch
        {
            Category.Vfx => Library.vfx,
            Category.Projectile => Library.projectiles,
            Category.Interactable => Library.interactables,
            _ => null
        };

        /// <summary>
        /// Called on a generated prefab root before it's saved: registers its ArtLibrary slot and adds the ArtSlot
        /// that shows the plugged model at runtime (so art changes never need a rebuild).
        /// </summary>
        public static void Apply(GameObject root, Category category, string key)
        {
            var list = ListFor(category);
            if (list == null)
                return;
            if (!list.Exists(e => e.name == key))
            {
                list.Add(new ArtLibrary.PrefabArt { name = key });
                EditorUtility.SetDirty(Library);
            }
            var slot = root.AddComponent<ArtSlot>();
            var so = new SerializedObject(slot);
            so.FindProperty("key").stringValue = key;
            so.FindProperty("category").intValue = (int)(category switch
            {
                Category.Vfx => ArtLibrary.Category.Vfx,
                Category.Projectile => ArtLibrary.Category.Projectile,
                _ => ArtLibrary.Category.Interactable
            });
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Save()
        {
            if (library != null)
                EditorUtility.SetDirty(library);
            library = null; // reloaded on the next build
        }
    }
}
