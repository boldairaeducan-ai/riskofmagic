using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using UnityEngine;

namespace RiskOfMagic.Skills
{
    /// <summary>Server helpers shared by skill types.</summary>
    public static class SkillUtility
    {
        private static readonly List<Health> targets = new();

        /// <summary>Pushes hostile enemies within radius away from origin (along the NavMesh).</summary>
        public static void Knockback(SkillContext context, Vector3 origin, float radius, float distance, float duration = 0.25f)
        {
            if (distance <= 0f || radius <= 0f)
                return;
            TargetFinder.FindHostiles(origin + Vector3.up * 0.5f, radius, context.Team, targets, context.HitMask);
            foreach (var health in targets)
            {
                if (!health.TryGetComponent(out EnemyBrain brain))
                    continue;
                Vector3 away = health.transform.position - origin;
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : context.Transform.forward;
                brain.Push(away * distance, duration);
            }
        }

        /// <summary>Hostiles within radius of center (reuses a shared list: copy it if you keep it).</summary>
        public static List<Health> FindHostiles(SkillContext context, Vector3 center, float radius)
        {
            TargetFinder.FindHostiles(center, radius, context.Team, targets, context.HitMask);
            return targets;
        }
    }
}
