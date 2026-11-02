using System.Collections.Generic;
using RiskOfMagic.Core;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    public static class TargetFinder
    {
        private static readonly Collider[] buffer = new Collider[128];

        /// <summary>Closest living Health of another team, ignoring <paramref name="exclude"/>.</summary>
        public static Health FindNearestHostile(Vector3 from, float radius, Team team, ICollection<Health> exclude = null,
            int mask = Physics.DefaultRaycastLayers)
        {
            int count = Physics.OverlapSphereNonAlloc(from, radius, buffer, mask, QueryTriggerInteraction.Ignore);
            Health best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!CombatUtility.TryGetDamageable(buffer[i], out var target))
                    continue;
                var health = target.Health;
                if (health == null || !health.IsAlive || health.Team == team || (exclude != null && exclude.Contains(health)))
                    continue;
                float sqr = (health.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = health;
                }
            }
            return best;
        }

        public static Vector3 Center(Health health) => health.transform.position + Vector3.up;
    }
}
