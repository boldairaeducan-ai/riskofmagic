using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Parade: blocks damage for a short time, then counter-attacks around the caster.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Parry", fileName = "Skill_Parry")]
    public class ParrySkill : SkillDefinition
    {
        [Range(0f, 1f)] public float damageReduction = 1f;
        [Min(0.1f)] public float duration = 1f;
        [Header("Counter-attack")]
        [Min(0.1f)] public float radius = 4f;
        [Min(0f)] public float damageCoefficient = 2f;
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float knockbackDistance = 3f;
        public GameObject counterVfx;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            context.Avatar.Health.AddShield(damageReduction, duration);
            context.Run(Counter(context));
        }

        private IEnumerator Counter(SkillContext context)
        {
            yield return new WaitForSeconds(duration);
            if (!context.Avatar.IsStanding)
                yield break;
            Vector3 center = context.Transform.position;
            context.DamageArea(center + Vector3.up, radius, damageCoefficient, damageType);
            SkillUtility.Knockback(context, center, radius, knockbackDistance);
            VfxNetwork.Spawn(counterVfx, center, Quaternion.identity, radius);
        }
    }
}
