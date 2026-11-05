using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>Freezes (no movement, no attacks) every enemy in a cone in front of the caster.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Cone Control", fileName = "Skill_ConeControl")]
    public class ConeControlSkill : SkillDefinition
    {
        [Min(0f)] public float range = 20f;
        [Range(1f, 90f), Tooltip("Half-angle of the cone.")] public float coneAngle = 35f;
        [Min(0f)] public float freezeDuration = 4f;

        [Header("Optional damage")]
        [Min(0f)] public float damageCoefficient;
        public DamageType damageType = DamageType.Arcane;
        [Tooltip("Optional damage over time applied to every enemy caught.")] public DotEffect dot;

        private static readonly List<Health> targets = new();

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            Vector3 eye = context.Look.CameraPivot.position;
            Vector3 aim = activation.Direction;
            aim.y = 0f; // horizontal cone: enemies above/below the crosshair are still caught
            aim = aim.sqrMagnitude > 0.001f ? aim.normalized : context.Transform.forward;

            TargetFinder.FindHostiles(eye, range, context.Team, targets, context.HitMask);
            foreach (var health in targets)
            {
                Vector3 toTarget = health.transform.position - context.Transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(aim, toTarget) > coneAngle)
                    continue;

                if (health.TryGetComponent(out EnemyActor enemy))
                    enemy.Freeze(freezeDuration);
                var info = context.CreateDamage(damageCoefficient > 0f ? damageCoefficient : 1f, damageType, TargetFinder.Center(health), toTarget.normalized);
                if (damageCoefficient > 0f)
                    health.ApplyDamage(info);
                if (dot != null)
                    DotSystem.Apply(health, dot, info);
            }
        }
    }
}
