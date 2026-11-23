using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>
    /// Résine collante: every hit slows the target (Resin status, one stack per item stack, 10% each in GameBalance).
    /// Several owners: the strongest slow wins.
    /// </summary>
    public class SlowOnHitItem : ItemDefinition
    {
        [Min(1)] public int maxStacks = 5;
        [Min(0.1f)] public float duration = 2f;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (proc.Victim == null || !proc.Victim.IsAlive || proc.Damage.ProcCoefficient <= 0f)
                return;
            var enemy = proc.Victim.GetComponentInParent<EnemyActor>();
            var status = enemy != null && enemy.Status != null ? enemy.Status : proc.Victim.GetComponentInParent<StatusEffects>();
            if (status == null)
                return;
            int wanted = Mathf.Clamp(Mathf.Max(stacks, status.GetStacks(StatusType.Resin)), 1, maxStacks);
            status.Apply(StatusType.Resin, duration, wanted, wanted);
        }
    }
}
