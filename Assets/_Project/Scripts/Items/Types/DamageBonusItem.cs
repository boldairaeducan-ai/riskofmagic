using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>
    /// Conditional outgoing damage bonus: close targets (Croc de boucher), critical hits (Lame affûtée) or targets under
    /// a damage over time (Venin de vipère).
    /// </summary>
    public class DamageBonusItem : ItemDefinition
    {
        public enum Condition : byte { CloseRange, Critical, TargetHasDot }

        public Condition condition = Condition.CloseRange;
        [Min(0f)] public float bonusFirstStack = 0.3f;
        [Min(0f)] public float bonusPerExtraStack = 0.3f;
        [Min(0f), Tooltip("CloseRange: max distance between the owner and the target (m).")] public float radius = 4f;

        private const int DotTypes = (1 << (int)DamageType.Bleed) | (1 << (int)DamageType.Fire) | (1 << (int)DamageType.Poison);

        public override float ModifyOutgoingDamage(PlayerAvatar owner, Health victim, in DamageInfo info, float amount, int stacks)
        {
            if (victim == null)
                return amount;
            bool applies = condition switch
            {
                Condition.CloseRange => (victim.transform.position - owner.transform.position).sqrMagnitude <= radius * radius,
                Condition.Critical => info.IsCritical,
                Condition.TargetHasDot => (victim.DotMask & DotTypes) != 0,
                _ => false
            };
            return applies ? amount * (1f + Stacked(bonusFirstStack, bonusPerExtraStack, stacks)) : amount;
        }
    }
}
