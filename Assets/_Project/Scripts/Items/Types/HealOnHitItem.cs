using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Items.Types
{
    [CreateAssetMenu(menuName = "Risk of Magic/Items/Heal On Hit", fileName = "Item_HealOnHit")]
    public class HealOnHitItem : ItemDefinition
    {
        [Min(0f)] public float healPerStack = 1f;

        public override void OnHitServer(PlayerAvatar owner, in ProcEvent proc, int stacks)
            => owner.Health.Heal(healPerStack * stacks * proc.Damage.ProcCoefficient);
    }
}
