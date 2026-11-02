using RiskOfMagic.Items;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Item names floating above nearby pickups, tinted by rarity.</summary>
    public class PickupLabelsWidget : MonoBehaviour
    {
        [SerializeField] private TMP_Text template;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float heightOffset = 0.8f;

        private TemplatePool<TMP_Text> pool;
        private RectTransform space;

        private void Awake()
        {
            space = (RectTransform)transform;
            if (template != null)
                pool = new TemplatePool<TMP_Text>(template, transform);
        }

        private void LateUpdate()
        {
            if (pool == null)
                return;

            var cam = ScreenProjection.GetGameplayCamera();
            pool.Begin();
            foreach (var pickup in ItemPickup.Active)
            {
                var item = pickup.Item;
                if (item == null || !ScreenProjection.TryProject(cam, space, pickup.VisualPosition + Vector3.up * heightOffset, maxDistance, out var position))
                    continue;
                var label = pool.Next();
                label.rectTransform.anchoredPosition = position;
                label.text = item.displayName;
                label.color = ItemDefinition.GetRarityColor(item.rarity);
            }
            pool.End();
        }
    }
}
