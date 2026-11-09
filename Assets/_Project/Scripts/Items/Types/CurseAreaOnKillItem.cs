using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>A kill curses the enemies around the victim (Curse status: weaker, slower, takes more damage).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Curse Area On Kill", fileName = "Item_CurseAreaOnKill")]
    public class CurseAreaOnKillItem : ItemDefinition
    {
        [Min(0f)] public float radiusFirstStack = 4f;
        [Min(0f)] public float radiusPerExtraStack = 1f;
        [Min(0.1f)] public float curseDuration = 4f;
        public GameObject vfx;

        private static readonly List<Health> targets = new();

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            float radius = Stacked(radiusFirstStack, radiusPerExtraStack, stacks);
            TargetFinder.FindHostiles(proc.Position, radius, owner.Health.Team, targets);
            foreach (var target in targets)
                if (target != null && target.IsAlive && target.TryGetComponent(out StatusEffects status))
                    status.Apply(StatusType.Curse, curseDuration);
            VfxNetwork.Spawn(vfx, proc.Position, Quaternion.identity, radius);
        }
    }
}
