using System.Collections.Generic;
using RiskOfMagic.Core;
using UnityEngine;

namespace RiskOfMagic.Combat
{
    /// <summary>
    /// Server: damage taken by a target under StatusType.Marked (Marque de mort) echoes to the enemies around it
    /// (GameBalance.statuses.markedEchoFraction within markedEchoRadius), credited to the same attacker. Echo damage
    /// never procs items and never echoes again.
    /// </summary>
    public static class MarkedEcho
    {
        private static bool echoing;
        private static readonly HashSet<Health> exclude = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            Health.ServerDamaged -= OnServerDamaged;
            Health.ServerDamaged += OnServerDamaged;
        }

        private static void OnServerDamaged(Health victim, DamageInfo info)
        {
            if (echoing || victim == null || info.Amount <= 0f || info.IgnoreModifiers)
                return;
            var status = victim.GetComponentInParent<StatusEffects>();
            if (status == null || !status.Has(StatusType.Marked))
                return;
            var balance = GameBalance.Instance.statuses;
            if (balance.markedEchoFraction <= 0f || balance.markedEchoRadius <= 0f)
                return;

            Vector3 center = victim.transform.position + Vector3.up;
            var echo = info;
            echo.Amount = info.Amount * balance.markedEchoFraction;
            echo.ProcCoefficient = 0f;
            echo.IsCritical = false;
            echo.Point = center;
            exclude.Clear();
            exclude.Add(victim);
            echoing = true;
            try
            {
                AreaDamage.Apply(center, balance.markedEchoRadius, echo, Physics.DefaultRaycastLayers, exclude);
            }
            finally
            {
                echoing = false;
            }
        }
    }
}
