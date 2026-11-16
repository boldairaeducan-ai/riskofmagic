using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Selectable lobby card (Templates/LobbyCard.uxml): icon (or initial letter on a colored square), title, text,
    /// selected state and a lock overlay showing the unlocking challenge. Pure view: MainMenuController fills it.
    /// </summary>
    public sealed class LobbyCardView
    {
        public Button Root { get; }

        private readonly VisualElement icon;
        private readonly Label letter;
        private readonly Label title;
        private readonly Label body;
        private readonly Label lockLabel;

        public LobbyCardView(VisualElement root, string modifierClass)
        {
            Root = root as Button ?? root?.Q<Button>();
            if (Root == null)
            {
                Debug.LogError("[Risk of Magic] LobbyCard.uxml must have a Button as its root.");
                return;
            }
            if (!string.IsNullOrEmpty(modifierClass))
                Root.AddToClassList(modifierClass);
            icon = Root.Q("icon");
            letter = Root.Q<Label>("iconLetter");
            title = Root.Q<Label>("title");
            body = Root.Q<Label>("body");
            lockLabel = Root.Q<Label>("lockLabel");
        }

        public void SetVisible(bool visible) => MenuUi.SetShown(Root, visible);

        /// <param name="lockText">Null/empty = unlocked.</param>
        public void Set(string titleText, string bodyText, Sprite sprite, Color fallbackColor, bool selected, string lockText)
        {
            if (Root == null)
                return;
            bool locked = !string.IsNullOrEmpty(lockText);
            MenuUi.SetText(title, titleText);
            MenuUi.SetText(body, bodyText);
            MenuUi.SetImage(icon, sprite);
            if (icon != null)
            {
                // No sprite: square in the character / difficulty color with its initial.
                var color = fallbackColor * (locked ? 0.5f : 1f);
                color.a = 1f;
                icon.style.backgroundColor = sprite != null ? new StyleColor(StyleKeyword.Null) : new StyleColor(color);
            }
            MenuUi.SetText(letter, sprite == null && !string.IsNullOrEmpty(titleText) ? titleText.Substring(0, 1).ToUpperInvariant() : string.Empty);
            Root.EnableInClassList("card--selected", selected && !locked);
            Root.EnableInClassList("card--locked", locked);
            MenuUi.SetText(lockLabel, lockText);
        }
    }
}
