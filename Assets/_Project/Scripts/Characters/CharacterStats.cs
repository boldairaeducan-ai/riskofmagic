using RiskOfMagic.Combat;
using RiskOfMagic.Items;
using UnityEngine;

namespace RiskOfMagic.Characters
{
    /// <summary>
    /// Final combat stats = CharacterDefinition base values modified by items and status effects.
    /// Recalculated on every peer when the character, the inventory or the statuses change.
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
        public float FlatDamageReduction { get; private set; }
        public float DamageTakenMultiplier { get; private set; } = 1f;
        public int BonusSkillCharges { get; private set; }
        /// <summary>Added to the movement config's sprint multiplier.</summary>
        public float SprintSpeedBonus { get; private set; }

        public void Recalculate(CharacterDefinition definition, PlayerInventory inventory, StatusEffects status, int level = 1)
        {
            int levelsAboveOne = Mathf.Max(0, level - 1);
            float sprintBonus = 0f;
            float attackSpeedBonus = 0f, damageBonus = 0f, moveBonus = 0f, healthFlat = 0f, healthPercent = 0f, regen = 0f, crit = 0f, flatReduction = 0f;
            float cooldown = 1f, damageTaken = 1f;
            int jumps = 0, charges = 0;

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
                            case StatType.FlatDamageReduction: flatReduction += value; break;
                            case StatType.DamageTakenReduction: damageTaken *= Mathf.Pow(1f - Mathf.Clamp01(modifier.valuePerStack), stacks); break;
                            case StatType.BonusSkillCharges: charges += Mathf.RoundToInt(value); break;
                            case StatType.SprintSpeed: sprintBonus += value; break;
                        }
                    }
                }
            }

            float statusAttack = status != null ? status.AttackSpeedMultiplier : 1f;
            float statusMove = status != null ? status.MoveSpeedMultiplier : 1f;

            BaseDamage = definition.baseDamage * (1f + definition.damagePerLevel * levelsAboveOne);
            DamageMultiplier = (1f + damageBonus) * (status != null ? status.DamageDealtMultiplier : 1f);
            AttackSpeed = definition.attackSpeed * (1f + attackSpeedBonus) * statusAttack;
            CritChance = Mathf.Clamp01(definition.critChance + crit);
            CooldownMultiplier = cooldown;
            MoveSpeedMultiplier = (1f + moveBonus) * statusMove;
            MaxHealth = (definition.maxHealth * (1f + definition.healthPerLevel * levelsAboveOne) + healthFlat) * (1f + healthPercent) * (status != null ? status.MaxHealthMultiplier : 1f);
            HealthRegen = definition.healthRegen * (1f + definition.regenPerLevel * levelsAboveOne) + regen;
            ExtraJumps = jumps;
            FlatDamageReduction = flatReduction;
            DamageTakenMultiplier = damageTaken;
            BonusSkillCharges = charges;
            SprintSpeedBonus = sprintBonus;
        }

        /// <summary>Server side. coefficient 2.5 = "250% damage".</summary>
        public float RollDamage(float coefficient, out bool isCritical, bool forceCritical = false)
        {
            isCritical = forceCritical || Random.value < CritChance;
            return BaseDamage * DamageMultiplier * coefficient * (isCritical ? 2f : 1f);
        }
    }
}
