using RiskOfMagic.Audio;
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
        public Sprite portrait;

        [Header("Stats")]
        [Min(1f)] public float maxHealth = 110f;
        [Min(0f), Tooltip("HP per second.")] public float healthRegen = 1f;
        [Min(0f)] public float baseDamage = 12f;
        [Min(0.1f)] public float attackSpeed = 1f;
        [Range(0f, 1f)] public float critChance = 0.01f;
        public MovementConfig movement;

        [Header("Models (optional, replace the placeholder capsule)")]
        [Tooltip("Third-person body seen by other players (shadow only for its owner). Animator inside = driven by PlayerAnimationDriver.")]
        public GameObject modelPrefab;
        [Tooltip("First-person arms/weapon, parented to the owner's camera.")] public GameObject viewModelPrefab;

        [Header("Audio")]
        [SoundSlot(MaxSimultaneous = 8)] public SoundEvent footstepSound;
        public SoundEvent jumpSound;
        public SoundEvent landSound;

        [Header("Skills")]
        public SkillDefinition primary;
        public SkillDefinition secondary;
        public SkillDefinition utility;
        public SkillDefinition special;

        [Header("Alternative skills (unlocked by challenges, chosen in the lobby)")]
        public SkillDefinition primaryAlt;
        public SkillDefinition secondaryAlt;
        public SkillDefinition utilityAlt;
        public SkillDefinition specialAlt;

        public SkillDefinition GetSkill(SkillSlot slot) => slot switch
        {
            SkillSlot.Primary => primary,
            SkillSlot.Secondary => secondary,
            SkillSlot.Utility => utility,
            _ => special
        };

        public SkillDefinition GetAlternative(SkillSlot slot) => slot switch
        {
            SkillSlot.Primary => primaryAlt,
            SkillSlot.Secondary => secondaryAlt,
            SkillSlot.Utility => utilityAlt,
            _ => specialAlt
        };

        /// <summary>Skill of a slot for a loadout mask (bit per slot = alternative, if it exists).</summary>
        public SkillDefinition GetSkill(SkillSlot slot, byte loadout)
        {
            var alternative = (loadout & (1 << (int)slot)) != 0 ? GetAlternative(slot) : null;
            return alternative != null ? alternative : GetSkill(slot);
        }
    }
}
