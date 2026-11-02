using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Every proc-capable hit explodes for a fraction of its damage.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Explode On Hit", fileName = "Item_ExplodeOnHit")]
    public class ExplodeOnHitItem : ItemDefinition
    {
        public DamageType damageType = DamageType.Arcane;
        [Min(0f), Tooltip("Fraction of the triggering hit.")] public float damageFraction = 0.6f;
        [Min(0f)] public float radiusFirstStack = 4f;
        [Min(0f)] public float radiusPerExtraStack = 2.5f;
        public GameObject explosionVfx;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            float radius = Stacked(radiusFirstStack, radiusPerExtraStack, stacks);
            float amount = proc.Damage.Amount * damageFraction * proc.Damage.ProcCoefficient;
            AreaDamage.Apply(proc.Position, radius, ProcDamage(owner, amount, damageType, proc.Position));
            VfxNetwork.Spawn(explosionVfx, proc.Position, Quaternion.identity, radius);
        }
    }
}
