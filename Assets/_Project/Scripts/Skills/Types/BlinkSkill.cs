using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Instant teleport along the aim, stopped by obstacles.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Blink", fileName = "Skill_Blink")]
    public class BlinkSkill : SkillDefinition
    {
        [Min(0f)] public float distance = 12f;
        [Min(0.05f)] public float collisionRadius = 0.4f;
        public LayerMask obstacleMask = Physics.DefaultRaycastLayers;
        public bool flattenDirection;
        public GameObject arrivalVfx;

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            Vector3 dir = flattenDirection ? Vector3.ProjectOnPlane(activation.Direction, Vector3.up).normalized : activation.Direction;
            Vector3 start = context.BodyCenter;
            float travel = Physics.SphereCast(start, collisionRadius, dir, out var hit, distance, obstacleMask, QueryTriggerInteraction.Ignore)
                ? hit.distance
                : distance;
            Vector3 center = start + dir * travel;
            activation.Origin = context.Transform.position;
            activation.Target = Physics.Raycast(center, Vector3.down, out var ground, 1f, obstacleMask, QueryTriggerInteraction.Ignore)
                ? ground.point
                : center - Vector3.up;
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            context.Motor.Teleport(activation.Target, context.Transform.rotation);
            context.Motor.SetVerticalVelocity(0f);
            VfxUtility.Spawn(arrivalVfx, activation.Target, Quaternion.identity);
        }

        public override void OnRemoteExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnRemoteExecute(context, activation);
            VfxUtility.Spawn(arrivalVfx, activation.Target, Quaternion.identity);
        }
    }
}
