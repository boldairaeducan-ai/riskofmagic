using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Heal On Kill", fileName = "Item_HealOnKill")]
    public class HealOnKillItem : ItemDefinition
    {
        [Min(0f)] public float healFirstStack = 8f;
        [Min(0f)] public float healPerExtraStack = 6f;

        public override void OnKillServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
            => owner.Health.Heal(Stacked(healFirstStack, healPerExtraStack, stacks));
    }
}
