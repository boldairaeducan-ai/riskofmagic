using System.Collections.Generic;
using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Killed enemies spread a damage over time to nearby enemies (burn...).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Dot Area On Kill", fileName = "Item_DotAreaOnKill")]
    public class DotAreaOnKillItem : ItemDefinition
    {
        public DotEffect dot;
        [Min(0f)] public float radiusFirstStack = 4f;
        [Min(0f)] public float radiusPerExtraStack = 4f;
        [Min(0f), Tooltip("Hit the DoT is based on, in owner base damage (burn: total = 1.5 x this).")] public float baseDamageCoefficient = 1f;
        [Min(0f)] public float baseDamagePerExtraStack;
        public GameObject vfx;

        private static readonly List<Health> targets = new();

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (dot == null)
                return;
            float radius = Stacked(radiusFirstStack, radiusPerExtraStack, stacks);
            TargetFinder.FindHostiles(proc.Position, radius, owner.Health.Team, targets);
            var source = ProcDamage(owner, OwnerBaseDamage(owner) * Stacked(baseDamageCoefficient, baseDamagePerExtraStack, stacks), dot.damageType, proc.Position);
            foreach (var target in targets)
                DotSystem.Apply(target, dot, source);
            VfxNetwork.Spawn(vfx, proc.Position, Quaternion.identity, radius);
        }
    }
}
