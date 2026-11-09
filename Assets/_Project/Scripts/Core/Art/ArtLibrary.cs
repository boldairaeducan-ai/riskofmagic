using System;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Items;
using UnityEngine;

namespace RiskOfMagic.Core
{
    /// <summary>
    /// User-owned art slots (Resources/ArtLibrary.asset): never overwritten by the builders, which only append
    /// missing entries. Prefab entries are applied by Build Prototype Scene (the model is nested in the generated
    /// prefab and replaces the placeholder renderers); menu art, cursor and icons are read at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Art Library", fileName = "ArtLibrary")]
    public class ArtLibrary : ScriptableObject
    {
        public const string ResourcePath = "ArtLibrary";

        [Serializable]
        public class PrefabArt
        {
            [Tooltip("Generated prefab name (do not edit).")] public string name;
            [Tooltip("Your model / effect. Empty = placeholder.")] public GameObject model;
            [Tooltip("Keep the placeholder renderers visible as well (lines, rings, trails driven by code).")] public bool keepPlaceholder;
            public Vector3 offset;
            public Vector3 rotation;
            public float scale = 1f;
        }

        [Serializable]
        public class StatusArt
        {
            [Tooltip("Do not edit.")] public string name;
            public StatusType status;
            public Sprite icon;
            [Tooltip("Shown in the HUD status bar.")] public bool visible = true;
            public bool debuff;
        }

        [Serializable]
        public class RarityArt
        {
            [Tooltip("Do not edit.")] public string name;
            public ItemRarity rarity;
            [Tooltip("Fallback icon of items of this rarity without their own icon.")] public Sprite icon;
        }

        [Header("Menus")]
        [Tooltip("Replaces the « RISK OF MAGIC » title of the main menu.")] public Sprite logo;
        [Tooltip("Full-screen image behind the main menu.")] public Sprite menuBackground;
        [Tooltip("Full-screen image of the loading screen.")] public Sprite loadingBackground;
        [Tooltip("Texture imported as « Cursor ».")] public Texture2D cursor;
        public Vector2 cursorHotspot;

        [Header("Icons")]
        public List<StatusArt> statuses = new();
        public List<RarityArt> rarities = new();

        [Header("Prefabs (applied by Build Prototype Scene)")]
        public List<PrefabArt> vfx = new();
        public List<PrefabArt> projectiles = new();
        public List<PrefabArt> interactables = new();

        private static ArtLibrary instance;
        private static bool loaded;

        /// <summary>Null when the asset doesn't exist (everything falls back to placeholders).</summary>
        public static ArtLibrary Instance
        {
            get
            {
                if (!loaded)
                {
                    instance = Resources.Load<ArtLibrary>(ResourcePath);
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyCursor()
        {
            var library = Instance;
            if (library != null && library.cursor != null)
                Cursor.SetCursor(library.cursor, library.cursorHotspot, CursorMode.Auto);
        }

        public StatusArt GetStatus(StatusType type)
        {
            foreach (var entry in statuses)
                if (entry.status == type)
                    return entry;
            return null;
        }

        /// <summary>Item icon, else its rarity icon, else <paramref name="fallback"/>.</summary>
        public static Sprite ItemIcon(ItemDefinition item, Sprite fallback)
        {
            if (item == null)
                return fallback;
            if (item.icon != null)
                return item.icon;
            var rarityIcon = Instance != null ? Instance.GetRarityIcon(item.rarity) : null;
            return rarityIcon != null ? rarityIcon : fallback;
        }

        public Sprite GetRarityIcon(ItemRarity rarity)
        {
            foreach (var entry in rarities)
                if (entry.rarity == rarity)
                    return entry.icon;
            return null;
        }
    }
}
