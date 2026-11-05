using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Bonus damage against low-health enemies.</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Executioner", fileName = "Item_Executioner")]
    public class ExecutionerItem : ItemDefinition
    {
        [Range(0f, 1f)] public float healthThreshold = 0.3f;
        [Min(0f)] public float bonusPerStack = 0.2f;
        [Tooltip("Off: bonus below the threshold (executioner). On: bonus above it (brise-garde, healthy targets).")]
        public bool aboveThreshold;

        public override float ModifyOutgoingDamage(PlayerAvatar owner, Health victim, in DamageInfo info, float amount, int stacks)
        {
            bool applies = aboveThreshold ? victim.Normalized > healthThreshold : victim.Normalized < healthThreshold;
            return applies ? amount * (1f + bonusPerStack * stacks) : amount;
        }
    }
}
