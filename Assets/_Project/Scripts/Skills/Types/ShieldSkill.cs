using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Temporary damage reduction on the caster (Health.AddShield).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Shield", fileName = "Skill_Shield")]
    public class ShieldSkill : SkillDefinition
    {
        [Range(0f, 1f)] public float damageReduction = 0.95f;
        [Min(0.1f)] public float duration = 3f;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
            => context.Avatar.Health.AddShield(damageReduction, duration);
    }
}
