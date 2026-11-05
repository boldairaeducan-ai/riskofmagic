using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Fiole d'adrénaline: attack speed for a few seconds after a kill (Adrenaline status, 1 stack = 1%).</summary>
    public class AdrenalineItem : ItemDefinition
    {
        [Min(0f)] public float bonusFirstStack = 0.25f;
        [Min(0f)] public float bonusPerExtraStack = 0.15f;
        [Min(0.1f)] public float duration = 3f;

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (owner.Status == null)
                return;
            int percent = Mathf.Clamp(Mathf.RoundToInt(Stacked(bonusFirstStack, bonusPerExtraStack, stacks) * 100f), 1, 255);
            owner.Status.Apply(StatusType.Adrenaline, duration, percent, percent); // refresh, no cumulative stacking
        }
    }
}
