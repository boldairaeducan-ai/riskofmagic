using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Interaction
{
    /// <summary>Bought with gold, drops a random item whose rarity is rolled from weights.</summary>
    public class ChestInteractable : PurchaseInteractable
    {
        [Header("Loot")]
        [SerializeField] protected RarityWeights rarityWeights = new(70f, 24f, 5f, 1f);
        [SerializeField] private ItemPickup pickupPrefab;
        [SerializeField] private Vector3 dropOffset = new(0f, 0f, 1.8f);

        [Header("Visuals")]
        [SerializeField] private GameObject closedVisual;

        protected override void OnPurchased(PlayerAvatar buyer)
        {
            var item = GameCatalog.Instance.RollItem(rarityWeights.Roll());
            if (item == null)
            {
                Debug.LogWarning($"[ChestInteractable] {name}: no item to drop.", this);
                return;
            }

            if (pickupPrefab == null)
            {
                ItemPickup.SpawnItem(item, transform.TransformPoint(dropOffset));
                return;
            }
            var pickup = Instantiate(pickupPrefab, transform.TransformPoint(dropOffset), Quaternion.identity);
            pickup.Initialize(item);
            pickup.NetworkObject.Spawn(true);
        }

        protected override void OnAvailabilityChanged(bool available)
        {
            if (closedVisual != null)
                closedVisual.SetActive(available);
            OpenAnimation.Apply(this, available); // imported animated model: opens only when bought
        }
    }
}
