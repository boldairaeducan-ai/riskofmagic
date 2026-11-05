using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Couronne des âmes: each kill adds damage until the end of the stage (SoulHarvest status, 1 stack = 1%).</summary>
    public class SoulHarvestItem : ItemDefinition
    {
        [Min(1)] public int percentPerKillPerStack = 1;
        [Range(1, 255)] public int maxPercent = 60;

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (owner.Status != null)
                owner.Status.Apply(StatusType.SoulHarvest, 0f, percentPerKillPerStack * stacks, maxPercent);
        }
    }
}
