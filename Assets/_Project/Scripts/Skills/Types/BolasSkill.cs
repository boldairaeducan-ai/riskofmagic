using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Skills.Types
{
    /// <summary>
    /// Charged bolas throw: hits the nearest enemies in a cone in front of the caster (more targets the longer it is
    /// charged), deals damage and ensnares them (StatusType.Ensnared: slowed; reduced slow on bosses and elites).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Skills/Bolas", fileName = "Skill_Bolas")]
    public class BolasSkill : SkillDefinition
    {
        [Min(0f)] public float range = 18f;
        [Range(1f, 90f), Tooltip("Half-angle of the cone.")] public float coneAngle = 30f;
        [Min(0f)] public float damageCoefficient = 1.5f;
        public DamageType damageType = DamageType.Physical;

        [Header("Charge")]
        [Min(1)] public int minTargets = 1;
        [Min(1)] public int maxTargets = 4;

        [Header("Ensnare")]
        [Min(0.1f)] public float ensnareDuration = 3f;

        [Header("Feedback")]
        [Tooltip("Spawned on every enemy hit (all peers).")] public GameObject hitVfx;

        private static readonly List<Health> found = new();
        private static readonly List<(Health health, float distance)> inCone = new();

        /// <summary>Number of enemies caught for a charge in [0, 1].</summary>
        public int TargetCount(float charge) =>
            Mathf.Clamp(minTargets + Mathf.FloorToInt(Mathf.Clamp01(charge) * (maxTargets - minTargets) + 0.0001f), minTargets, Mathf.Max(minTargets, maxTargets));

        public override void OnServerExecute(SkillContext context, in SkillActivation activation)
        {
            Vector3 eye = context.Look.CameraPivot.position;
            Vector3 aim = activation.Direction;
            aim.y = 0f; // horizontal cone: enemies above/below the crosshair are still caught
            aim = aim.sqrMagnitude > 0.001f ? aim.normalized : context.Transform.forward;

            TargetFinder.FindHostiles(eye, range, context.Team, found, context.HitMask);
            inCone.Clear();
            foreach (var health in found)
            {
                Vector3 toTarget = health.transform.position - context.Transform.position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(aim, toTarget) > coneAngle)
                    continue;
                inCone.Add((health, toTarget.sqrMagnitude));
            }
            inCone.Sort((a, b) => a.distance.CompareTo(b.distance));

            int count = Mathf.Min(TargetCount(activation.Charge), inCone.Count);
            for (int i = 0; i < count; i++)
            {
                var health = inCone[i].health;
                Vector3 center = TargetFinder.Center(health);
                Vector3 direction = (center - eye).normalized;
                if (damageCoefficient > 0f)
                    health.ApplyDamage(context.CreateDamage(damageCoefficient, damageType, center, direction));
                var enemy = health.GetComponentInParent<EnemyActor>();
                var status = enemy != null && enemy.Status != null ? enemy.Status : health.GetComponentInParent<StatusEffects>();
                if (status != null)
                {
                    bool resistant = enemy != null && (enemy.IsBoss || enemy.IsElite);
                    status.Apply(StatusType.Ensnared, ensnareDuration, addStacks: resistant ? 1 : 2, maxStacks: resistant ? 1 : 2);
                }
                if (hitVfx != null)
                    VfxNetwork.Spawn(hitVfx, center, Quaternion.LookRotation(direction));
            }
        }
    }
}
