using System;
using RiskOfMagic.Audio;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items
{
    public enum ItemRarity : byte
    {
        Common,
        Uncommon,
        Rare,
        Legendary,
        /// <summary>Only from boss rewards.</summary>
        Boss
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
        ExtraJumps,
        /// <summary>Flat damage removed from each hit taken (never below 1).</summary>
        FlatDamageReduction,
        /// <summary>Multiplicative per stack: 0.15 = -15%, then -15% of the rest...</summary>
        DamageTakenReduction,
        /// <summary>Extra charges on every skill except the primary.</summary>
        BonusSkillCharges,
        /// <summary>Added to the sprint multiplier (0.25 = sprinting 25 % of base speed faster).</summary>
        SprintSpeed
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        [Tooltip("Percent stats: 0.15 = +15%. CooldownReduction/DamageTakenReduction stack multiplicatively. MaxHealth, HealthRegen, ExtraJumps, FlatDamageReduction, BonusSkillCharges are flat.")]
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
        [SoundSlot(false, Optional = true), Tooltip("Empty = AudioConfig pickup sound of the rarity.")] public SoundEvent pickupSound;
        public Sprite icon;
        public StatModifier[] modifiers = Array.Empty<StatModifier>();

        /// <summary>Server only. Called a frame after a hit with ProcCoefficient > 0 dealt by the owner.</summary>
        public virtual void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks) { }

        /// <summary>Server only. Called a frame after the owner killed something.</summary>
        public virtual void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks) { }

        /// <summary>Server only. Called every <paramref name="interval"/> seconds while the owner stands (auras).</summary>
        public virtual void OnServerTick(PlayerAvatar owner, int stacks, float interval) { }

        /// <summary>Server only. Called right after the owner took damage.</summary>
        public virtual void OnOwnerDamagedServer(PlayerAvatar owner, in DamageInfo info, int stacks) { }

        /// <summary>Server only. Modify the damage the owner deals to <paramref name="victim"/>.</summary>
        public virtual float ModifyOutgoingDamage(PlayerAvatar owner, Health victim, in DamageInfo info, float amount, int stacks) => amount;

        /// <summary>Server only. Return true to survive a lethal hit (one stack is consumed by the inventory).</summary>
        public virtual bool TryPreventDeath(PlayerAvatar owner, int stacks, out float healthFraction)
        {
            healthFraction = 0f;
            return false;
        }

        public static Color GetRarityColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => new Color(0.4f, 0.85f, 0.35f),
            ItemRarity.Rare => new Color(0.35f, 0.6f, 1f),
            ItemRarity.Legendary => new Color(1f, 0.6f, 0.15f),
            ItemRarity.Boss => new Color(0.95f, 0.85f, 0.2f),
            _ => Color.white
        };

        public static string GetRarityName(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => "Peu commun",
            ItemRarity.Rare => "Rare",
            ItemRarity.Legendary => "Légendaire",
            ItemRarity.Boss => "Boss",
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
