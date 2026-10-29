using UnityEngine;

namespace RiskOfMagic.Characters
{
    /// <summary>
    /// Combat stats read by skills. Base values come from the CharacterDefinition;
    /// the multiplier properties are the hook for items and buffs.
    /// </summary>
    public class CharacterStats : MonoBehaviour
    {
        public float BaseDamage { get; private set; } = 12f;
        public float AttackSpeed { get; set; } = 1f;
        public float CritChance { get; set; } = 0.01f;
        public float CooldownMultiplier { get; set; } = 1f;
        public float DamageMultiplier { get; set; } = 1f;

        public void ApplyDefinition(CharacterDefinition definition)
        {
            BaseDamage = definition.baseDamage;
            AttackSpeed = definition.attackSpeed;
            CritChance = definition.critChance;
            CooldownMultiplier = 1f;
            DamageMultiplier = 1f;
        }

        /// <summary>Server side. coefficient 2.5 = "250% damage".</summary>
        public float RollDamage(float coefficient, out bool isCritical)
        {
            isCritical = Random.value < CritChance;
            return BaseDamage * DamageMultiplier * coefficient * (isCritical ? 2f : 1f);
        }
    }
}
