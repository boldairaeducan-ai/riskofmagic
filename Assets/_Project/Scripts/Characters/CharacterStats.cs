using RiskOfMagic.Items;
using UnityEngine;

namespace RiskOfMagic.Characters
{
    /// <summary>
    /// Final combat stats = CharacterDefinition base values modified by the inventory's items.
    /// Recalculated on every peer when the character or the inventory changes.
    /// </summary>
    public class CharacterStats : MonoBehaviour
    {
        public float BaseDamage { get; private set; } = 10f;
        public float DamageMultiplier { get; private set; } = 1f;
        public float AttackSpeed { get; private set; } = 1f;
        public float CritChance { get; private set; } = 0.01f;
        public float CooldownMultiplier { get; private set; } = 1f;
        public float MoveSpeedMultiplier { get; private set; } = 1f;
        public float MaxHealth { get; private set; } = 100f;
        public float HealthRegen { get; private set; }
        public int ExtraJumps { get; private set; }

        public void Recalculate(CharacterDefinition definition, PlayerInventory inventory)
        {
            float attackSpeedBonus = 0f, damageBonus = 0f, moveBonus = 0f, healthFlat = 0f, healthPercent = 0f, regen = 0f, crit = 0f;
            float cooldown = 1f;
            int jumps = 0;

            var catalog = RiskOfMagic.Core.GameCatalog.Instance;
            if (inventory != null && catalog != null)
            {
                for (int i = 0; i < inventory.SlotCount; i++)
                {
                    int stacks = inventory.GetCountAt(i);
                    var item = stacks > 0 ? catalog.GetItem(i) : null;
                    if (item == null)
                        continue;

                    foreach (var modifier in item.modifiers)
                    {
                        float value = modifier.valuePerStack * stacks;
                        switch (modifier.stat)
                        {
                            case StatType.AttackSpeed: attackSpeedBonus += value; break;
                            case StatType.Damage: damageBonus += value; break;
                            case StatType.MoveSpeed: moveBonus += value; break;
                            case StatType.MaxHealth: healthFlat += value; break;
                            case StatType.MaxHealthPercent: healthPercent += value; break;
                            case StatType.HealthRegen: regen += value; break;
                            case StatType.CritChance: crit += value; break;
                            case StatType.CooldownReduction: cooldown *= Mathf.Pow(1f - Mathf.Clamp01(modifier.valuePerStack), stacks); break;
                            case StatType.ExtraJumps: jumps += Mathf.RoundToInt(value); break;
                        }
                    }
                }
            }

            BaseDamage = definition.baseDamage;
            DamageMultiplier = 1f + damageBonus;
            AttackSpeed = definition.attackSpeed * (1f + attackSpeedBonus);
            CritChance = Mathf.Clamp01(definition.critChance + crit);
            CooldownMultiplier = cooldown;
            MoveSpeedMultiplier = 1f + moveBonus;
            MaxHealth = (definition.maxHealth + healthFlat) * (1f + healthPercent);
            HealthRegen = definition.healthRegen + regen;
            ExtraJumps = jumps;
        }

        /// <summary>Server side. coefficient 2.5 = "250% damage".</summary>
        public float RollDamage(float coefficient, out bool isCritical)
        {
            isCritical = Random.value < CritChance;
            return BaseDamage * DamageMultiplier * coefficient * (isCritical ? 2f : 1f);
        }
    }
}
