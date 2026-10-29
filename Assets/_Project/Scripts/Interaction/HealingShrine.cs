using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    public class HealingShrine : PurchaseInteractable
    {
        [SerializeField, Range(0f, 1f)] private float healFraction = 0.5f;

        protected override void OnPurchased(PlayerAvatar buyer) => buyer.Health.Heal(buyer.Health.Max * healFraction);
    }
}
