using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Critical hits also deal a fraction of their damage around the target.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Crit Splash", fileName = "Item_CritSplash")]
    public class CritSplashItem : ItemDefinition
    {
        public DamageType damageType = DamageType.Arcane;
        [Min(0f), Tooltip("Fraction of the critical hit.")] public float damageFirstStack = 0.5f;
        [Min(0f)] public float damagePerExtraStack = 0.25f;
        [Min(0f)] public float radius = 3f;
        public GameObject vfx;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (!proc.Damage.IsCritical)
                return;
            float amount = proc.Damage.Amount * Stacked(damageFirstStack, damagePerExtraStack, stacks) * proc.Damage.ProcCoefficient;
            AreaDamage.Apply(proc.Position, radius, ProcDamage(owner, amount, damageType, proc.Position));
            VfxNetwork.Spawn(vfx, proc.Position, Quaternion.identity, radius);
        }
    }
}
