using RiskOfMagic.Items;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>"Item obtained" banner (icon, name, description).</summary>
    public class ItemNotificationWidget : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private UnityEngine.UI.Image frame;
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField, Min(0f)] private float duration = 3.5f;
        [SerializeField, Min(0.01f)] private float fadeDuration = 0.4f;

        private float shownAt = float.NegativeInfinity;

        private void OnEnable() => PlayerInventory.LocalItemGained += Show;
        private void OnDisable() => PlayerInventory.LocalItemGained -= Show;

        private void Show(ItemDefinition item, int count)
        {
            if (item == null)
                return;
            var color = ItemDefinition.GetRarityColor(item.rarity);
            var sprite = item.icon != null ? item.icon : theme != null ? theme.defaultItemIcon : null;

            if (frame != null)
                frame.color = color;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : color;
            }
            if (titleLabel != null)
            {
                titleLabel.text = count > 1 ? $"{item.displayName}  x{count}" : item.displayName;
                titleLabel.color = color;
            }
            if (descriptionLabel != null)
                descriptionLabel.text = item.description;
            shownAt = Time.time;
        }

        private void Update()
        {
            if (group == null)
                return;
            float age = Time.time - shownAt;
            group.alpha = age < duration ? Mathf.Clamp01(age / fadeDuration) : Mathf.Clamp01(1f - (age - duration) / fadeDuration);
        }
    }
}
