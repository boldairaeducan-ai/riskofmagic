using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    public enum AreaCenter : byte
    {
        Caster,
        AimPoint
    }

    /// <summary>Spins, slams, rains of arrows, ground zones: one or several damage ticks in a sphere.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Area", fileName = "Skill_Area")]
    public class AreaSkill : SkillDefinition
    {
        [Header("Area")]
        public AreaCenter center = AreaCenter.Caster;
        [Min(0f)] public float maxRange = 40f;
        [Min(0.1f)] public float radius = 5f;
        public bool followCaster = true;

        [Header("Damage")]
        public DamageType damageType = DamageType.Physical;
        [Min(0f)] public float damageCoefficient = 1f;
        [Min(1)] public int ticks = 1;
        [Min(0f)] public float initialDelay;
        [Min(0f)] public float tickInterval = 0.2f;
        public bool hitOncePerTarget;

        [Header("Caster")]
        [Min(0f)] public float casterUpwardVelocity;

        [Header("Feedback")]
        public GameObject tickVfx;

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            if (center == AreaCenter.Caster)
            {
                activation.Target = context.Transform.position;
                return;
            }

            Vector3 from = context.Transform.position;
            Vector3 offset = activation.Target - from;
            if (offset.magnitude > maxRange)
                activation.Target = from + offset.normalized * maxRange;
            activation.Target = context.ProjectToGround(activation.Target);
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            if (casterUpwardVelocity > 0f)
                context.Motor.SetVerticalVelocity(casterUpwardVelocity);
            context.Run(VisualTicks(context, activation.Target));
        }

        public override void OnRemoteExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnRemoteExecute(context, activation);
            context.Run(VisualTicks(context, activation.Target));
        }

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
            => context.Run(DamageTicks(context, activation.Target));

        private Vector3 GetCenter(SkillContext context, Vector3 target)
            => center == AreaCenter.Caster && followCaster ? context.Transform.position : target;

        private IEnumerator DamageTicks(SkillContext context, Vector3 target)
        {
            var alreadyHit = hitOncePerTarget ? new HashSet<Health>() : null;
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);

            for (int i = 0; i < ticks; i++)
            {
                if (center == AreaCenter.Caster && !context.Avatar.IsAlive)
                    yield break;
                context.DamageArea(GetCenter(context, target) + Vector3.up * 0.5f, radius, damageCoefficient, damageType, alreadyHit);
                if (i < ticks - 1)
                    yield return new WaitForSeconds(tickInterval);
            }
        }

        private IEnumerator VisualTicks(SkillContext context, Vector3 target)
        {
            if (tickVfx == null)
                yield break;
            if (initialDelay > 0f)
                yield return new WaitForSeconds(initialDelay);
            for (int i = 0; i < ticks; i++)
            {
                VfxUtility.Spawn(tickVfx, GetCenter(context, target), Quaternion.identity, radius);
                if (i < ticks - 1)
                    yield return new WaitForSeconds(tickInterval);
            }
        }
    }
}
