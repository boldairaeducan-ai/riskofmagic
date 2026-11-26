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

        protected override bool SealedDuringBossFight => true;

        protected override void OnPurchased(PlayerAvatar buyer)
        {
            var item = GameCatalog.Instance.RollItem(rarityWeights.Roll());
            if (item == null)
            {
                Debug.LogWarning($"[ChestInteractable] {name}: no item to drop.", this);
                return;
            }

            Vector3 launch = transform.position + Vector3.up * 1.2f; // the item pops out of the chest
            if (pickupPrefab == null)
            {
                var prefab = GameCatalog.Instance != null ? GameCatalog.Instance.PickupPrefab : null;
                if (prefab == null)
                {
                    ItemPickup.SpawnItem(item, transform.TransformPoint(dropOffset));
                    return;
                }
                pickupPrefab = prefab;
            }
            var pickup = Instantiate(pickupPrefab, transform.TransformPoint(dropOffset), Quaternion.identity);
            pickup.Initialize(item);
            pickup.SetLaunchOrigin(launch);
            pickup.NetworkObject.Spawn(true);
        }

        protected override void OnAvailabilityChanged(bool available)
        {
            if (closedVisual != null)
                closedVisual.SetActive(available);
            OpenAnimation.Apply(this, available); // imported animated model: opens only when bought
            if (!available && IsSpawned && Time.timeSinceLevelLoad > 1f)
            {
                // Opening juice: a jolt and a golden burst (every peer).
                RiskOfMagic.Feedback.Punch.Play(transform, 0.25f, 0.35f);
                RiskOfMagic.Feedback.HitSparks.Burst(transform.position + Vector3.up * 1f, Vector3.up, new Color(1f, 0.8f, 0.3f), 26, 1.1f, 1.2f);
            }
        }
    }
}
