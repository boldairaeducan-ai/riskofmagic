using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Killed enemies explode (scales with the owner's base damage).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Explode On Kill", fileName = "Item_ExplodeOnKill")]
    public class ExplodeOnKillItem : ItemDefinition
    {
        public DamageType damageType = DamageType.Fire;
        [Min(0f)] public float coefficientFirstStack = 1.5f;
        [Min(0f)] public float coefficientPerExtraStack = 0.75f;
        [Min(0f)] public float radiusFirstStack = 6f;
        [Min(0f)] public float radiusPerExtraStack = 1.5f;
        public GameObject explosionVfx;

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            float radius = Stacked(radiusFirstStack, radiusPerExtraStack, stacks);
            float amount = OwnerBaseDamage(owner) * Stacked(coefficientFirstStack, coefficientPerExtraStack, stacks);
            AreaDamage.Apply(proc.Position, radius, ProcDamage(owner, amount, damageType, proc.Position));
            VfxNetwork.Spawn(explosionVfx, proc.Position, Quaternion.identity, radius);
        }
    }
}
