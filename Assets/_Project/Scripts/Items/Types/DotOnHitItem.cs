using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Chance on hit to apply a damage over time based on the owner's base damage (bleed...).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Dot On Hit", fileName = "Item_DotOnHit")]
    public class DotOnHitItem : ItemDefinition
    {
        public DotEffect dot;
        [Range(0f, 1f)] public float chanceFirstStack = 0.1f;
        [Range(0f, 1f)] public float chancePerExtraStack = 0.1f;
        [Min(0f), Tooltip("Base damage multiplier fed to the DoT (its tick fraction x ticks gives the total).")]
        public float baseDamageCoefficient = 1f;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (dot == null || proc.Victim == null || !proc.Victim.IsAlive)
                return;
            if (Random.value > Stacked(chanceFirstStack, chancePerExtraStack, stacks) * proc.Damage.ProcCoefficient)
                return;
            DotSystem.Apply(proc.Victim, dot, ProcDamage(owner, OwnerBaseDamage(owner) * baseDamageCoefficient, dot.damageType, proc.Position));
        }
    }
}
