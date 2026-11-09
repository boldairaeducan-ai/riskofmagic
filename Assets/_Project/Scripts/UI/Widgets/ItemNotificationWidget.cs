using RiskOfMagic.Items;
using RiskOfMagic.Meta;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>"Item obtained" banner (icon, name, description); also announces completed challenges.</summary>
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

        private static readonly Color ChallengeColor = new(1f, 0.82f, 0.3f);

        private void OnEnable()
        {
            PlayerInventory.LocalItemGained += Show;
            MetaProgression.ChallengeCompleted += ShowChallenge;
            HudAnnouncements.Raised += ShowMessage;
        }

        private void OnDisable()
        {
            PlayerInventory.LocalItemGained -= Show;
            MetaProgression.ChallengeCompleted -= ShowChallenge;
            HudAnnouncements.Raised -= ShowMessage;
        }

        private void ShowMessage(string title, string description, Color color)
        {
            if (frame != null)
                frame.color = color;
            if (icon != null)
            {
                icon.sprite = theme != null ? theme.defaultItemIcon : null;
                icon.color = color;
            }
            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.color = color;
            }
            if (descriptionLabel != null)
                descriptionLabel.text = description;
            shownAt = Time.time;
        }

        private void ShowChallenge(ChallengeDefinition challenge)
        {
            if (frame != null)
                frame.color = ChallengeColor;
            if (icon != null)
            {
                icon.sprite = theme != null ? theme.defaultItemIcon : null;
                icon.color = ChallengeColor;
            }
            if (titleLabel != null)
            {
                titleLabel.text = $"Défi accompli : {challenge.displayName}";
                titleLabel.color = ChallengeColor;
            }
            if (descriptionLabel != null)
                descriptionLabel.text = $"Débloqué — {challenge.RewardName}";
            shownAt = Time.time;
        }

        private void Show(ItemDefinition item, int count)
        {
            if (item == null)
                return;
            var color = ItemDefinition.GetRarityColor(item.rarity);
            var sprite = RiskOfMagic.Core.ArtLibrary.ItemIcon(item, theme != null ? theme.defaultItemIcon : null);

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
