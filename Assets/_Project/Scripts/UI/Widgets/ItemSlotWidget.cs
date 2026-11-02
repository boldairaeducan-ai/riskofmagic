using RiskOfMagic.Items;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Item icon (ItemDefinition.icon) + rarity-tinted frame + stack count.</summary>
    public class ItemSlotWidget : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private UnityEngine.UI.Image frame;
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMP_Text placeholderName;
        [SerializeField] private TMP_Text countLabel;

        public void Set(ItemDefinition item, int count)
        {
            var rarityColor = ItemDefinition.GetRarityColor(item.rarity);
            var sprite = item.icon != null ? item.icon : theme != null ? theme.defaultItemIcon : null;

            if (frame != null)
                frame.color = rarityColor;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.25f);
            }
            if (placeholderName != null)
            {
                placeholderName.text = item.displayName;
                placeholderName.gameObject.SetActive(sprite == null);
            }
            if (countLabel != null)
                countLabel.text = count > 1 ? $"x{count}" : string.Empty;
        }
    }
}
