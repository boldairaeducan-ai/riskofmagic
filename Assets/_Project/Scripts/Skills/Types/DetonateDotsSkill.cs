using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Consumes every DoT of a type on nearby enemies and deals their remaining damage at once (with a bonus).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Detonate Dots", fileName = "Skill_DetonateDots")]
    public class DetonateDotsSkill : SkillDefinition
    {
        public DamageType dotType = DamageType.Poison;
        [Min(0f)] public float radius = 25f;
        [Min(0f), Tooltip("Damage = remaining DoT damage x this multiplier.")] public float damageMultiplier = 1.5f;
        [Tooltip("Spawned on every detonated enemy (must be in the GameCatalog effects).")] public GameObject burstVfx;

        private static readonly List<Health> targets = new();

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            TargetFinder.FindHostiles(context.BodyCenter, radius, context.Team, targets, context.HitMask);
            foreach (var health in targets)
            {
                float remaining = DotSystem.Consume(health, dotType);
                if (remaining <= 0f)
                    continue;
                Vector3 center = TargetFinder.Center(health);
                var info = DamageInfo.Create(remaining * damageMultiplier, dotType, context.NetworkObject, context.Team, center, Vector3.up);
                health.ApplyDamage(info);
                VfxNetwork.Spawn(burstVfx, center, Quaternion.identity, 1.5f);
            }
        }
    }
}
