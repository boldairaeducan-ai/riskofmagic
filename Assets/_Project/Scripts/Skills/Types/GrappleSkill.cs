using System.Collections;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Flèche-grappin (owner-authoritative movement): a rope is shot along the aim. If it catches the level within range,
    /// the caster is pulled to the anchor (gravity off, fall cancelled) and gets a small lift on arrival to climb ledges.
    /// If nothing is caught, once the rope reaches its max range the caster is launched upwards anyway.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Grapple", fileName = "Skill_Grapple")]
    public class GrappleSkill : SkillDefinition
    {
        [Min(1f)] public float range = 25f;
        [Min(1f), Tooltip("Rope travel speed (m/s): the pull starts when it reaches the anchor.")] public float ropeSpeed = 90f;
        [Min(1f)] public float pullSpeed = 32f;
        [Min(0.1f)] public float maxPullTime = 1.2f;
        [Min(0.1f), Tooltip("The pull stops this close to the anchor.")] public float stopDistance = 1.6f;
        [Min(0f), Tooltip("Upward velocity on arrival (climbs the ledge).")] public float arrivalLift = 6f;
        [Header("Nothing caught")]
        [Min(0f), Tooltip("Upward velocity when the rope catches nothing.")] public float missLift = 15f;
        [Min(0f), Tooltip("Push along the flat aim when the rope catches nothing.")] public float missForward = 5f;
        [Header("Feedback")]
        [Tooltip("Line effect (rope) drawn from the caster to the anchor, local on every peer.")] public GameObject ropeVfx;

        private static readonly RaycastHit[] hits = new RaycastHit[16];

        public override float MaxRange => 0f; // no "out of range" crosshair: the rope always does something

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            Vector3 origin = context.Look.CameraPivot.position;
            Vector3 direction = activation.Direction.sqrMagnitude > 0.001f ? activation.Direction.normalized : context.Transform.forward;
            bool caught = false;
            Vector3 anchor = origin + direction * range;
            int count = Physics.RaycastNonAlloc(origin, direction, hits, range, context.Controller.AimMask, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (context.IsSelf(hits[i].collider) || hits[i].distance >= best)
                    continue;
                best = hits[i].distance;
                anchor = hits[i].point;
                caught = true;
            }
            activation.Origin = origin;
            activation.Target = anchor;
            activation.Charge = caught ? 1f : 0f; // replicated flag: anchor found
        }

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnOwnerExecute(context, activation);
            DrawRope(context, activation.Target);
            context.Run(Pull(context, activation.Target, activation.Charge > 0.5f, activation.Direction));
        }

        public override void OnRemoteExecute(SkillContext context, in SkillActivation activation)
        {
            base.OnRemoteExecute(context, activation);
            DrawRope(context, activation.Target);
        }

        private void DrawRope(SkillContext context, Vector3 anchor)
        {
            if (ropeVfx != null)
                VfxUtility.SpawnLine(ropeVfx, new[] { context.BodyCenter + Vector3.up * 0.3f, anchor });
        }

        private IEnumerator Pull(SkillContext context, Vector3 anchor, bool caught, Vector3 aim)
        {
            var motor = context.Motor;
            var body = context.Transform;
            float travel = Vector3.Distance(context.BodyCenter, anchor) / ropeSpeed;
            if (travel > 0f)
                yield return new WaitForSeconds(travel);
            if (motor == null || !context.Avatar.IsStanding)
                yield break;

            if (!caught)
            {
                // Nothing to hold on to: the rope still yanks the archer upwards.
                Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up);
                motor.SetVerticalVelocity(missLift);
                if (flat.sqrMagnitude > 0.001f)
                    motor.AddImpulse(flat.normalized * missForward);
                yield break;
            }

            float end = Time.time + maxPullTime;
            while (Time.time < end && context.Avatar.IsStanding)
            {
                Vector3 toAnchor = anchor - (body.position + Vector3.up);
                if (toAnchor.magnitude <= stopDistance)
                    break;
                motor.Dash(toAnchor.normalized * pullSpeed, 0.1f, true); // refreshed every frame while pulling
                yield return null;
            }
            motor.Dash(Vector3.zero, 0f, false);
            motor.SetVerticalVelocity(arrivalLift);
        }
    }
}
