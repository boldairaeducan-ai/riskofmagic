using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    /// <summary>Bought with gold, drops a random equipment (picked up with E).</summary>
    public class EquipmentBarrel : PurchaseInteractable
    {
        [SerializeField] private Vector3 dropOffset = new(0f, 0f, 1.6f);
        [SerializeField] private GameObject closedVisual;

        protected override void OnPurchased(PlayerAvatar buyer)
        {
            var equipment = GameCatalog.Instance.RollEquipment();
            if (equipment != null)
                ItemPickup.SpawnEquipment(equipment, transform.TransformPoint(dropOffset));
        }

        protected override void OnAvailabilityChanged(bool available)
        {
            if (closedVisual != null)
                closedVisual.SetActive(available);
        }
    }
}
