using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>
    /// A stacking status on hit: on the owner after a critical hit (Talisman de frénésie: Frenzy) or on the target hit
    /// (Brise-armure: Shattered). Max stacks = first + per extra item stack. Item / DoT damage never triggers it.
    /// </summary>
    public class StatusOnHitItem : ItemDefinition
    {
        public StatusType status = StatusType.Frenzy;
        [Tooltip("On: the owner gets the status. Off: the target hit.")] public bool onOwner = true;
        [Tooltip("Only critical hits trigger it.")] public bool criticalOnly = true;
        [Min(0.1f)] public float duration = 3f;
        [Min(1)] public int maxStacksFirst = 3;
        [Min(0)] public int maxStacksPerExtra = 2;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (proc.Damage.ProcCoefficient <= 0f || (criticalOnly && !proc.Damage.IsCritical))
                return;
            var target = onOwner ? owner.Status : SealSystem.FindStatus(proc.Victim);
            if (target == null || (!onOwner && (proc.Victim == null || !proc.Victim.IsAlive)))
                return;
            int max = Mathf.Clamp(maxStacksFirst + maxStacksPerExtra * (stacks - 1), 1, 255);
            target.Apply(status, duration, 1, max);
        }
    }
}
