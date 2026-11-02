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
        [SerializeField, Min(0f)] private float commonWeight = 79.2f;
        [SerializeField, Min(0f)] private float rareWeight = 19.8f;
        [SerializeField, Min(0f)] private float legendaryWeight = 1f;
        [SerializeField] private ItemPickup pickupPrefab;
        [SerializeField] private Vector3 dropOffset = new(0f, 0f, 1.8f);

        [Header("Visuals")]
        [SerializeField] private GameObject closedVisual;

        protected override void OnPurchased(PlayerAvatar buyer)
        {
            var item = GameCatalog.Instance.RollItem(RollRarity());
            if (item == null || pickupPrefab == null)
            {
                Debug.LogWarning($"[ChestInteractable] {name}: no item or pickup prefab.", this);
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
        }

        private ItemRarity RollRarity()
        {
            float roll = Random.value * (commonWeight + rareWeight + legendaryWeight);
            if (roll < legendaryWeight)
                return ItemRarity.Legendary;
            return roll < legendaryWeight + rareWeight ? ItemRarity.Rare : ItemRarity.Common;
        }
    }
}
