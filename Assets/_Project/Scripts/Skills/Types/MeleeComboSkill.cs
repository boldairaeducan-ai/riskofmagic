using System;
using System.Collections;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Chained melee swings; the step advances if recast within comboResetTime.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Melee Combo", fileName = "Skill_MeleeCombo")]
    public class MeleeComboSkill : SkillDefinition
    {
        [Serializable]
        public struct Step
        {
            [Min(0f)] public float damageCoefficient;
            [Min(0.1f)] public float radius;
            [Min(0f)] public float reach;
            [Min(0f)] public float hitDelay;
            [Min(0f)] public float lunge;
        }

        public DamageType damageType = DamageType.Physical;
        public Step[] steps =
        {
            new() { damageCoefficient = 1.3f, radius = 1.6f, reach = 1.6f, hitDelay = 0.1f },
            new() { damageCoefficient = 1.3f, radius = 1.6f, reach = 1.6f, hitDelay = 0.1f },
            new() { damageCoefficient = 2.6f, radius = 2.2f, reach = 1.8f, hitDelay = 0.15f, lunge = 6f }
        };
        [Min(0f)] public float comboResetTime = 0.9f;
        public GameObject swingVfx;

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            bool continues = Time.time - state.LastActivationTime <= comboResetTime;
            state.Step = continues ? (state.Step + 1) % steps.Length : 0;
            activation.Step = (byte)state.Step;
            activation.Target = activation.Origin + activation.Direction * GetStep(activation).reach;
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            var step = GetStep(activation);
            if (step.lunge > 0f)
            {
                Vector3 flat = Vector3.ProjectOnPlane(activation.Direction, Vector3.up).normalized;
                context.Motor.AddImpulse(flat * step.lunge);
            }
            SpawnSwing(activation, step);
        }

        public override void OnRemoteExecute(SkillContext context, in SkillActivation activation) => SpawnSwing(activation, GetStep(activation));

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
            => context.Run(HitRoutine(context, activation.Direction, GetStep(activation)));

        private IEnumerator HitRoutine(SkillContext context, Vector3 direction, Step step)
        {
            if (step.hitDelay > 0f)
                yield return new WaitForSeconds(step.hitDelay / Mathf.Max(0.01f, context.Stats.AttackSpeed));
            if (!context.Avatar.IsAlive)
                yield break;
            Vector3 center = context.Look.CameraPivot.position + direction * step.reach;
            context.DamageArea(center, step.radius, step.damageCoefficient, damageType);
        }

        private Step GetStep(in SkillActivation activation) => steps[Mathf.Min(activation.Step, steps.Length - 1)];

        private void SpawnSwing(in SkillActivation activation, Step step)
            => VfxUtility.Spawn(swingVfx, activation.Origin + activation.Direction * step.reach, Quaternion.LookRotation(activation.Direction), step.radius);
    }
}
