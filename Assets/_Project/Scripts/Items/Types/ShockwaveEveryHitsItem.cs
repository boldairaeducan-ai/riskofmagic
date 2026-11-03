using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Every N proc-capable hits, a shockwave around the owner.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Shockwave Every Hits", fileName = "Item_ShockwaveEveryHits")]
    public class ShockwaveEveryHitsItem : ItemDefinition
    {
        [Min(1)] public int hitsRequired = 5;
        [Min(0f)] public float coefficientFirstStack = 3f;
        [Min(0f)] public float coefficientPerExtraStack = 1.5f;
        [Min(0f)] public float radius = 6f;
        public DamageType damageType = DamageType.Physical;
        public GameObject vfx;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (owner.Inventory.IncrementCounter(this) % hitsRequired != 0)
                return;
            Vector3 center = owner.transform.position;
            float amount = OwnerBaseDamage(owner) * Stacked(coefficientFirstStack, coefficientPerExtraStack, stacks);
            AreaDamage.Apply(center + Vector3.up * 0.5f, radius, ProcDamage(owner, amount, damageType, center));
            VfxNetwork.Spawn(vfx, center, Quaternion.identity, radius);
        }
    }
}
