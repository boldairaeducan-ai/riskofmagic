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

        /// <summary>Hostile closest to the aim point (within tolerance) and within range of the caster, or null.</summary>
        public static Health FindAimedHostile(SkillContext context, Vector3 aimPoint, float range, float tolerance)
        {
            Health best = null;
            float bestSqr = float.MaxValue;
            foreach (var health in FindHostiles(context, aimPoint, tolerance))
            {
                if ((health.transform.position - context.Transform.position).sqrMagnitude > range * range)
                    continue;
                float sqr = (TargetFinder.Center(health) - aimPoint).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = health;
                }
            }
            return best;
        }

        /// <summary>Hostiles within radius of center (reuses a shared list: copy it if you keep it).</summary>
        public static List<Health> FindHostiles(SkillContext context, Vector3 center, float radius)
        {
            TargetFinder.FindHostiles(center, radius, context.Team, targets, context.HitMask);
            return targets;
        }
    }
}
