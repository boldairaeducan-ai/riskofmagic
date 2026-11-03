using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Reusable "item card" (icon, name, description, price/action button) for choice and shop menus.</summary>
    public class OfferCardWidget : MonoBehaviour
    {
        [SerializeField] private UnityEngine.UI.Image frame;
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMP_Text placeholderName;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private UnityEngine.UI.Button button;
        [SerializeField] private TMP_Text buttonLabel;

        public UnityEngine.UI.Button Button => button;

        public void Set(string title, string description, Sprite sprite, Color color, string action, bool interactable)
        {
            if (frame != null)
                frame.color = color;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(color.r, color.g, color.b, 0.2f);
            }
            if (placeholderName != null)
            {
                placeholderName.text = title;
                placeholderName.gameObject.SetActive(sprite == null);
            }
            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.color = color;
            }
            if (descriptionLabel != null)
                descriptionLabel.text = description;
            if (buttonLabel != null)
                buttonLabel.text = action;
            if (button != null)
                button.interactable = interactable;
        }
    }
}
