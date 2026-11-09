using RiskOfMagic.Combat;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Pas de l'ombre: teleports behind the aimed enemy (owner-authoritative movement); the next hit is a guaranteed
    /// critical (Ambush status). Cannot be cast without a target in range.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Shadow Step", fileName = "Skill_ShadowStep")]
    public class ShadowStepSkill : SkillDefinition
    {
        [Min(1f)] public float range = 20f;
        [Min(0.5f)] public float aimTolerance = 3f;
        [Min(0.5f)] public float distanceBehind = 1.6f;
        [Min(0.1f)] public float ambushDuration = 4f;
        public GameObject vfx;

        public override float MaxRange => range;

        public override bool CanActivate(SkillContext context, SkillSlotState state)
            => SkillUtility.FindAimedHostile(context, AimPoint(context), range, aimTolerance) != null;

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            var target = SkillUtility.FindAimedHostile(context, activation.Target, range, aimTolerance);
            if (target == null)
                return;
            Vector3 back = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up);
            back = back.sqrMagnitude > 0.001f ? back.normalized : -Vector3.ProjectOnPlane(activation.Direction, Vector3.up).normalized;
            Vector3 destination = target.transform.position - back * distanceBehind;
            activation.Origin = context.Transform.position;
            activation.Target = context.ProjectToGround(destination + Vector3.up, 4f);
            activation.Direction = (target.transform.position - activation.Target).normalized;
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            Vector3 flat = Vector3.ProjectOnPlane(activation.Direction, Vector3.up);
            var rotation = flat.sqrMagnitude > 0.001f ? Quaternion.LookRotation(flat) : context.Transform.rotation;
            context.Motor.Teleport(activation.Target, rotation);
        }

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            if (context.Avatar.Status != null)
                context.Avatar.Status.Apply(StatusType.Ambush, ambushDuration);
            VfxNetwork.Spawn(vfx, activation.Origin, Quaternion.identity);
            VfxNetwork.Spawn(vfx, activation.Target, Quaternion.identity);
        }

        private static Vector3 AimPoint(SkillContext context)
        {
            Ray aim = context.Look.AimRay;
            return Physics.Raycast(aim, out var hit, 60f, context.HitMask, QueryTriggerInteraction.Ignore) && !context.IsSelf(hit.collider)
                ? hit.point
                : aim.GetPoint(20f);
        }
    }
}
