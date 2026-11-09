using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Danse des lames: invulnerable for a short time while hitting every enemy around several times.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Blade Dance", fileName = "Skill_BladeDance")]
    public class BladeDanceSkill : SkillDefinition
    {
        [Min(0.1f)] public float duration = 1.5f;
        [Min(1)] public int hits = 8;
        [Min(0.5f)] public float radius = 6f;
        [Min(0f)] public float damageCoefficientPerHit = 1.2f;
        public DamageType damageType = DamageType.Physical;
        public GameObject hitVfx;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            context.Avatar.Health.AddShield(1f, duration);
            context.Run(Dance(context));
        }

        private IEnumerator Dance(SkillContext context)
        {
            var wait = new WaitForSeconds(duration / hits);
            for (int i = 0; i < hits; i++)
            {
                if (!context.Avatar.IsStanding)
                    yield break;
                Vector3 center = context.Transform.position + Vector3.up;
                context.DamageArea(center, radius, damageCoefficientPerHit, damageType);
                VfxNetwork.Spawn(hitVfx, context.Transform.position, Quaternion.Euler(0f, i * 45f, 0f), radius * 0.5f);
                yield return wait;
            }
        }
    }
}
