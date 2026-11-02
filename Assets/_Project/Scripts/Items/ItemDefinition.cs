using System;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items
{
    public enum ItemRarity : byte
    {
        Common,
        Rare,
        Legendary
    }

    public enum StatType : byte
    {
        AttackSpeed,
        Damage,
        MoveSpeed,
        MaxHealth,
        MaxHealthPercent,
        HealthRegen,
        CritChance,
        CooldownReduction,
        ExtraJumps
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        [Tooltip("Percent stats: 0.15 = +15%. CooldownReduction stacks multiplicatively. MaxHealth/HealthRegen/ExtraJumps are flat.")]
        public float valuePerStack;
    }

    /// <summary>
    /// Stackable item. Stat modifiers cover passive items; override the server hooks for procs.
    /// Definitions are shared assets: no per-player state here.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Stat Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public string displayName = "Item";
        [TextArea] public string description;
        public ItemRarity rarity = ItemRarity.Common;
        public Sprite icon;
        public StatModifier[] modifiers = Array.Empty<StatModifier>();

        /// <summary>Server only. Called a frame after a hit with ProcCoefficient > 0 dealt by the owner.</summary>
        public virtual void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks) { }

        /// <summary>Server only. Called a frame after the owner killed something.</summary>
        public virtual void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks) { }

        public static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Rare => new Color(0.35f, 0.6f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.75f, 0.2f),
            _ => Color.white
        };

        public static string GetRarityName(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Rare => "Rare",
            ItemRarity.Legendary => "Légendaire",
            _ => "Commun"
        };

        /// <summary>RoR-style stacking: first stack value + value per additional stack.</summary>
        protected static float Stacked(float first, float perExtraStack, int stacks) => first + perExtraStack * (stacks - 1);

        /// <summary>Damage that can't proc items again (prevents infinite chains).</summary>
        protected static DamageInfo ProcDamage(PlayerAvatar owner, float amount, DamageType type, Vector3 point, Vector3 direction = default)
        {
            var info = DamageInfo.Create(amount, type, owner.NetworkObject, owner.Health.Team, point, direction);
            info.ProcCoefficient = 0f;
            return info;
        }

        protected static float OwnerBaseDamage(PlayerAvatar owner) => owner.Stats.BaseDamage * owner.Stats.DamageMultiplier;
    }
}
