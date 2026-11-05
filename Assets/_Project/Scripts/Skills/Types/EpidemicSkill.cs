using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Épidémie: the aimed enemy's poison stacks are copied onto every enemy around it (based on the caster's damage).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Epidemic", fileName = "Skill_Epidemic")]
    public class EpidemicSkill : SkillDefinition
    {
        public DotEffect poison;
        [Min(1f)] public float targetRange = 40f;
        [Min(0.5f), Tooltip("How close to the aim point the source enemy must be.")] public float aimTolerance = 3f;
        [Min(1f)] public float spreadRadius = 10f;
        [Min(0f), Tooltip("Damage coefficient the copied stacks are based on.")] public float damageCoefficient = 1f;
        [Min(1), Tooltip("Applied even if the source has fewer stacks.")] public int minStacks = 1;
        public GameObject spreadVfx;

        private static readonly List<Health> candidates = new();
        private static readonly List<Health> spread = new();

        public override float MaxRange => targetRange;

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            if (poison == null)
                return;
            var source = FindSource(context, activation.Target);
            if (source == null)
                return;

            int stacks = Mathf.Max(minStacks, DotSystem.CountStacks(source, poison.damageType));
            Vector3 center = TargetFinder.Center(source);
            VfxNetwork.Spawn(spreadVfx, center, Quaternion.identity, spreadRadius * 0.5f);
            spread.Clear();
            spread.AddRange(SkillUtility.FindHostiles(context, center, spreadRadius));
            foreach (var target in spread)
            {
                var info = context.CreateDamage(damageCoefficient, poison.damageType, TargetFinder.Center(target), Vector3.up);
                for (int i = 0; i < stacks; i++)
                    DotSystem.Apply(target, poison, info);
                if (target != source)
                    VfxNetwork.Spawn(spreadVfx, TargetFinder.Center(target), Quaternion.identity, 1f);
            }
        }

        private Health FindSource(SkillContext context, Vector3 aimPoint)
        {
            Health best = null;
            float bestSqr = aimTolerance * aimTolerance;
            candidates.Clear();
            candidates.AddRange(SkillUtility.FindHostiles(context, aimPoint, aimTolerance));
            foreach (var health in candidates)
            {
                if ((health.transform.position - context.Transform.position).sqrMagnitude > targetRange * targetRange)
                    continue;
                float sqr = (TargetFinder.Center(health) - aimPoint).sqrMagnitude;
                if (sqr <= bestSqr + 4f)
                {
                    bestSqr = sqr;
                    best = health;
                }
            }
            return best;
        }
    }
}
