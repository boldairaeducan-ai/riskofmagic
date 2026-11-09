using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Enemies
{
    /// <summary>Server helpers shared by the boss attacks.</summary>
    public static class EnemyBossUtility
    {
        private static readonly List<Health> targets = new();

        /// <summary>Ground point under <paramref name="around"/> (or the point itself).</summary>
        public static Vector3 Ground(Vector3 around, LayerMask mask)
            => Physics.Raycast(around + Vector3.up * 4f, Vector3.down, out var hit, 40f, mask, QueryTriggerInteraction.Ignore) ? hit.point : around;

        /// <summary>Damages (and optionally gives a status to) every hostile of <paramref name="actor"/> in the radius.</summary>
        public static int HitArea(EnemyActor actor, Vector3 center, float radius, float damage, DamageType type, float maxHeight = 3f,
            bool applyStatus = false, StatusType status = StatusType.Chilled, float statusDuration = 1f)
        {
            int count = 0;
            TargetFinder.FindHostiles(center, radius, actor.Health.Team, targets);
            foreach (var health in targets)
            {
                if (health == null || !health.IsAlive || Mathf.Abs(health.transform.position.y - center.y) > maxHeight)
                    continue;
                Vector3 point = TargetFinder.Center(health);
                var info = DamageInfo.Create(damage * actor.DamageMultiplier, type, actor.NetworkObject, actor.Health.Team, point,
                    (point - center).normalized);
                health.ApplyDamage(info);
                if (applyStatus && health.TryGetComponent(out StatusEffects effects))
                    effects.Apply(status, statusDuration);
                count++;
            }
            return count;
        }

        /// <summary>Standing players within <paramref name="range"/> of <paramref name="from"/>.</summary>
        public static void PlayersInRange(Vector3 from, float range, List<PlayerAvatar> results)
        {
            results.Clear();
            foreach (var player in PlayerAvatar.All)
                if (player != null && player.IsStanding && (player.transform.position - from).sqrMagnitude <= range * range)
                    results.Add(player);
        }
    }
}
