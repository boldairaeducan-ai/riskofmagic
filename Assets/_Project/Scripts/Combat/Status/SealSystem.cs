using RiskOfMagic.Characters;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Paladin seals (server): hammer hits stack the Sealed status on enemies; Judgment (or the thrown shield) detonates
    /// them: extra holy damage per seal and a heal for the allies around the target.
    /// </summary>
    public static class SealSystem
    {
        public static StatusEffects FindStatus(Health target)
        {
            if (target == null)
                return null;
            var enemy = target.GetComponentInParent<EnemyActor>();
            return enemy != null && enemy.Status != null ? enemy.Status : target.GetComponentInParent<StatusEffects>();
        }

        /// <summary>Adds <paramref name="seals"/> seals (capped) and refreshes their duration.</summary>
        public static void Apply(Health target, int seals, int maxSeals, float duration)
        {
            if (seals <= 0 || target == null || !target.IsAlive)
                return;
            var status = FindStatus(target);
            if (status != null)
                status.Apply(StatusType.Sealed, duration, seals, maxSeals);
        }

        /// <summary>
        /// Consumes the target's seals: <paramref name="coefficientPerSeal"/> x the attacker's damage per seal, and heals the
        /// standing players within <paramref name="healRadius"/> of the target by <paramref name="healFractionPerSeal"/> of
        /// their max health per seal. Returns the number of seals consumed.
        /// </summary>
        public static int Detonate(Health target, in DamageInfo hit, float coefficientPerSeal, float healFractionPerSeal, float healRadius, GameObject vfx)
        {
            var status = FindStatus(target);
            int seals = status != null ? status.GetStacks(StatusType.Sealed) : 0;
            if (seals <= 0)
                return 0;
            status.Clear(StatusType.Sealed);
            Vector3 center = TargetFinder.Center(target);

            if (coefficientPerSeal > 0f && target.IsAlive && hit.TryGetSource(out var source) && source.TryGetComponent(out CharacterStats stats))
            {
                var info = hit;
                info.Amount = stats.RollDamage(coefficientPerSeal * seals, out bool critical);
                info.IsCritical = critical;
                info.Point = center;
                target.ApplyDamage(info);
            }

            if (healFractionPerSeal > 0f)
                foreach (var player in PlayerAvatar.All)
                {
                    if (player == null || !player.IsStanding || (player.transform.position - center).sqrMagnitude > healRadius * healRadius)
                        continue;
                    player.Health.Heal(player.Health.Max * healFractionPerSeal * seals);
                }

            VfxNetwork.Spawn(vfx, center, Quaternion.identity, healRadius * 0.5f);
            return seals;
        }
    }
}
