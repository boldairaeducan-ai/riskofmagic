using RiskOfMagic.Combat;
using RiskOfMagic.Combat.Projectiles;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Lame jumelle: chance that a hit strikes a second time for the same damage.</summary>
    public class DoubleHitItem : ItemDefinition
    {
        [Range(0f, 1f)] public float chanceFirstStack = 0.15f;
        [Range(0f, 1f)] public float chancePerExtraStack = 0.1f;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
        {
            if (proc.Victim == null || !proc.Victim.IsAlive)
                return;
            if (Random.value > Mathf.Clamp01(Stacked(chanceFirstStack, chancePerExtraStack, stacks)) * proc.Damage.ProcCoefficient)
                return;
            proc.Victim.ApplyDamage(ProcDamage(owner, proc.Damage.Amount, proc.Damage.Type, proc.Position, proc.Damage.Direction));
        }
    }
}
