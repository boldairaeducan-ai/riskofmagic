using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Voile d'ombre: Stealth status. Enemies lose track of the caster, who moves faster; the first hit dealt out of
    /// stealth is doubled and ends it (PlayerInventory outgoing damage). Other players see the caster vanish.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Stealth", fileName = "Skill_Stealth")]
    public class StealthSkill : SkillDefinition
    {
        [Min(0.1f)] public float duration = 3f;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            if (context.Avatar.Status != null)
                context.Avatar.Status.Apply(StatusType.Stealth, duration);
        }
    }
}
