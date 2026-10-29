using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    public enum DashDirection : byte
    {
        MoveInput,
        AimFlat,
        Aim
    }

    /// <summary>Rolls, charges, leaps. Optional damage along the path.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Dash", fileName = "Skill_Dash")]
    public class DashSkill : SkillDefinition
    {
        [Header("Dash")]
        public DashDirection direction = DashDirection.MoveInput;
        [Min(0f)] public float speed = 25f;
        [Min(0.01f)] public float duration = 0.25f;
        public bool ignoreGravity = true;
        public float upwardVelocity;

        [Header("Contact damage (0 = none)")]
        [Min(0f)] public float damageCoefficient;
        [Min(0.1f)] public float damageRadius = 1.6f;
        public DamageType damageType = DamageType.Physical;
        [Min(0.02f)] public float damageTickInterval = 0.05f;

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            Transform t = context.Transform;
            Vector3 dir = direction switch
            {
                DashDirection.MoveInput => t.right * context.Input.Move.x + t.forward * context.Input.Move.y,
                DashDirection.AimFlat => Vector3.ProjectOnPlane(activation.Direction, Vector3.up),
                _ => activation.Direction
            };
            activation.Direction = dir.sqrMagnitude > 0.01f ? dir.normalized : t.forward;
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            context.Motor.Dash(activation.Direction * speed + Vector3.up * upwardVelocity, duration, ignoreGravity);
        }

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            if (damageCoefficient > 0f)
                context.Run(ContactDamage(context));
        }

        private IEnumerator ContactDamage(SkillContext context)
        {
            var alreadyHit = new HashSet<Health>();
            float end = Time.time + duration + 0.1f; // a bit of slack for replication delay
            var wait = new WaitForSeconds(damageTickInterval);
            while (Time.time < end && context.Avatar.IsAlive)
            {
                context.DamageArea(context.BodyCenter, damageRadius, damageCoefficient, damageType, alreadyHit);
                yield return wait;
            }
        }
    }
}
