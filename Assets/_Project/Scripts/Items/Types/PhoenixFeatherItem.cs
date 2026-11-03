using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    /// <summary>Survive a lethal hit once per stack (the stack is consumed).</summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Phoenix Feather", fileName = "Item_PhoenixFeather")]
    public class PhoenixFeatherItem : ItemDefinition
    {
        [Range(0.05f, 1f)] public float reviveHealthFraction = 0.5f;

        public override bool TryPreventDeath(PlayerAvatar owner, int stacks, out float healthFraction)
        {
            healthFraction = reviveHealthFraction;
            return stacks > 0;
        }
    }
}
