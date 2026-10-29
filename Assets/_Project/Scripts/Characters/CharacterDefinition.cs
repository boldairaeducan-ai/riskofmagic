using RiskOfMagic.Core;
using RiskOfMagic.Player;
using RiskOfMagic.Skills;
using UnityEngine;

namespace RiskOfMagic.Characters
{
    [CreateAssetMenu(menuName = "Risk of Magic/Character", fileName = "Character")]
    public class CharacterDefinition : ScriptableObject
    {
        public string displayName = "Character";
        [TextArea] public string description;
        public Color bodyColor = Color.white;

        [Header("Stats")]
        [Min(1f)] public float maxHealth = 110f;
        [Min(0f)] public float baseDamage = 12f;
        [Min(0.1f)] public float attackSpeed = 1f;
        [Range(0f, 1f)] public float critChance = 0.01f;
        public MovementConfig movement;

        [Header("Skills")]
        public SkillDefinition primary;
        public SkillDefinition secondary;
        public SkillDefinition utility;
        public SkillDefinition special;

        public SkillDefinition GetSkill(SkillSlot slot) => slot switch
        {
            SkillSlot.Primary => primary,
            SkillSlot.Secondary => secondary,
            SkillSlot.Utility => utility,
            _ => special
        };
    }
}
