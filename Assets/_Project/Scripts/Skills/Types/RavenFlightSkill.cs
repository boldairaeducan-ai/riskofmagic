using RiskOfMagic.Combat;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Envol de corbeaux: for a short time the caster turns into a swarm: invulnerable, faster (RavenForm status) and
    /// hovering (no gravity, keeps its altitude; PlayerMotor.Hover on the owner).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Raven Flight", fileName = "Skill_RavenFlight")]
    public class RavenFlightSkill : SkillDefinition
    {
        [Min(0.1f)] public float duration = 1.5f;
        [Tooltip("Small lift at the start (m/s), 0 = keeps the current height.")] public float liftVelocity = 3f;
        [Min(0f), Tooltip("Holding Jump during the flight rises at this speed (m/s).")] public float climbSpeed = 7f;

        public override void OnOwnerExecute(SkillContext context, in SkillActivation activation)
        {
            if (context.Motor != null)
                context.Motor.Hover(duration, liftVelocity, climbSpeed);
        }

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            context.Avatar.Health.AddShield(1f, duration);
            if (context.Avatar.Status != null)
                context.Avatar.Status.Apply(StatusType.RavenForm, duration);
        }
    }
}
