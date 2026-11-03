using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RiskOfMagic.Items
{
    /// <summary>Relative chances per rarity (chests, merchant, boss rewards). Boss items are rolled separately.</summary>
    [Serializable]
    public struct RarityWeights
    {
        [Min(0f)] public float common;
        [Min(0f)] public float uncommon;
        [Min(0f)] public float rare;
        [Min(0f)] public float legendary;

        public RarityWeights(float common, float uncommon, float rare, float legendary)
        {
            this.common = common;
            this.uncommon = uncommon;
            this.rare = rare;
            this.legendary = legendary;
        }

        public ItemRarity Roll()
        {
            float total = common + uncommon + rare + legendary;
            if (total <= 0f)
                return ItemRarity.Common;
            float roll = Random.value * total;
            if ((roll -= legendary) < 0f)
                return ItemRarity.Legendary;
            if ((roll -= rare) < 0f)
                return ItemRarity.Rare;
            return roll - uncommon < 0f ? ItemRarity.Uncommon : ItemRarity.Common;
        }
    }
}
