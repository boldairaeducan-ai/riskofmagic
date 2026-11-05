using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Selectable lobby card (character, skill option, difficulty): icon, title, text, selection frame and a lock
    /// overlay showing the unlocking challenge and its progress. Pure view: MainMenuController fills it.
    /// </summary>
    public class LobbyCard : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text iconLetter;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField, Tooltip("Shown when selected.")] private GameObject selectedFrame;
        [SerializeField] private GameObject lockOverlay;
        [SerializeField] private TMP_Text lockLabel;

        public Button Button => button;

        /// <param name="lockText">Null/empty = unlocked.</param>
        public void Set(string title, string body, Sprite sprite, Color fallbackColor, bool selected, string lockText)
        {
            bool locked = !string.IsNullOrEmpty(lockText);
            if (titleLabel != null)
                titleLabel.text = title;
            if (bodyLabel != null)
                bodyLabel.text = body ?? string.Empty;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? (locked ? new Color(0.4f, 0.4f, 0.4f) : Color.white) : fallbackColor * (locked ? 0.5f : 1f);
            }
            if (iconLetter != null)
                iconLetter.text = sprite == null && !string.IsNullOrEmpty(title) ? title.Substring(0, 1).ToUpperInvariant() : string.Empty;
            if (selectedFrame != null)
                selectedFrame.SetActive(selected && !locked);
            if (lockOverlay != null)
                lockOverlay.SetActive(locked);
            if (lockLabel != null)
                lockLabel.text = lockText ?? string.Empty;
        }
    }
}
