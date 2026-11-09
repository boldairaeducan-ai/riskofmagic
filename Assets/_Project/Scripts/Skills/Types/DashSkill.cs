using System.Collections;
using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    public enum DashDirection : byte
    {
        MoveInput,
        AimFlat,
        Aim,
        /// <summary>Opposite of the flat aim (back hop).</summary>
        BackwardFlat
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
        [Tooltip("Optional damage over time applied on contact.")] public DotEffect dot;

        [Min(0f), Tooltip("Enemies touched are pushed this far.")] public float contactKnockbackDistance;
        [Range(0f, 1f), Tooltip("Allies touched heal this fraction of their max health (once).")] public float allyHealOnContact;

        [Header("Knockback at the start (0 = none)")]
        [Min(0f)] public float startKnockbackRadius;
        [Min(0f)] public float startKnockbackDistance;

        public override void PrepareActivation(SkillContext context, SkillSlotState state, ref SkillActivation activation)
        {
            Transform t = context.Transform;
            Vector3 dir = direction switch
            {
                DashDirection.MoveInput => t.right * context.Input.Move.x + t.forward * context.Input.Move.y,
                DashDirection.AimFlat => Vector3.ProjectOnPlane(activation.Direction, Vector3.up),
                DashDirection.BackwardFlat => -Vector3.ProjectOnPlane(activation.Direction, Vector3.up),
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
            SkillUtility.Knockback(context, context.Transform.position, startKnockbackRadius, startKnockbackDistance);
            if (damageCoefficient > 0f || allyHealOnContact > 0f)
                context.Run(ContactDamage(context));
        }

        private IEnumerator ContactDamage(SkillContext context)
        {
            var alreadyHit = new HashSet<Health>();
            var healed = new HashSet<PlayerAvatar>();
            float end = Time.time + duration + 0.1f; // a bit of slack for replication delay
            var wait = new WaitForSeconds(damageTickInterval);
            while (Time.time < end && context.Avatar.IsAlive)
            {
                if (damageCoefficient > 0f)
                {
                    int before = alreadyHit.Count;
                    context.DamageArea(context.BodyCenter, damageRadius, damageCoefficient, damageType, alreadyHit, dot);
                    if (contactKnockbackDistance > 0f && alreadyHit.Count > before)
                        SkillUtility.Knockback(context, context.Transform.position, damageRadius, contactKnockbackDistance);
                }
                if (allyHealOnContact > 0f)
                    foreach (var player in PlayerAvatar.All)
                    {
                        if (player == null || player == context.Avatar || !player.IsStanding || healed.Contains(player))
                            continue;
                        if ((player.transform.position - context.Transform.position).sqrMagnitude > damageRadius * damageRadius * 1.5f)
                            continue;
                        healed.Add(player);
                        player.Health.Heal(player.Health.Max * allyHealOnContact);
                    }
                yield return wait;
            }
        }
    }
}
