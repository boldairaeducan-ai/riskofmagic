using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Carapace d'ambre: each kill grants barrier points (capped by max health), lost after a while without a kill.</summary>
    public class BarrierOnKillItem : ItemDefinition
    {
        [Min(0f)] public float barrierFirstStack = 5f;
        [Min(0f)] public float barrierPerExtraStack = 5f;
        [Range(0f, 1f), Tooltip("Cap as a fraction of max health.")] public float maxHealthCap = 0.2f;
        [Min(0.1f), Tooltip("The barrier vanishes this long after the last kill.")] public float duration = 5f;

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
            => owner.Health.AddBarrier(Stacked(barrierFirstStack, barrierPerExtraStack, stacks), owner.Health.Max * maxHealthCap, duration);
    }
}
