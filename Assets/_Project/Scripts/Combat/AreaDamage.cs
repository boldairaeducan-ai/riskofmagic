using System.Collections.Generic;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    public static class AreaDamage
    {
        private static readonly Collider[] buffer = new Collider[128];
        private static readonly HashSet<Health> hitThisCall = new();

        /// <summary>
        /// Server only. Damages every hostile Health in the sphere once (weak points ignored).
        /// <paramref name="alreadyHit"/> lets multi-tick effects hit each target only once.
        /// </summary>
        public static int Apply(Vector3 center, float radius, DamageInfo template, LayerMask mask, HashSet<Health> alreadyHit = null)
        {
            int count = Physics.OverlapSphereNonAlloc(center, radius, buffer, mask, QueryTriggerInteraction.Ignore);
            hitThisCall.Clear();
            int applied = 0;

            for (int i = 0; i < count; i++)
            {
                if (!CombatUtility.TryGetDamageable(buffer[i], out var target))
                    continue;
                var health = target.Health;
                if (health == null || !health.IsAlive || health.Team == template.SourceTeam)
                    continue;
                if (!hitThisCall.Add(health) || (alreadyHit != null && !alreadyHit.Add(health)))
                    continue;

                var info = template;
                info.Point = buffer[i].ClosestPoint(center);
                info.Direction = (info.Point - center).normalized;
                health.ApplyDamage(info);
                applied++;
            }
            return applied;
        }

        public static int Apply(Vector3 center, float radius, DamageInfo template, HashSet<Health> alreadyHit = null)
            => Apply(center, radius, template, Physics.DefaultRaycastLayers, alreadyHit);
    }
}
