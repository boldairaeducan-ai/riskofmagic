using RiskOfMagic.Interaction;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    public class InteractionPromptWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;

        private PlayerInteractor interactor;

        protected override void OnBind() => interactor = Player.GetComponent<PlayerInteractor>();

        protected override void OnUnbind()
        {
            interactor = null;
            SetText(string.Empty);
        }

        protected override void Tick() => SetText(interactor != null ? interactor.FocusPrompt : string.Empty);

        private void SetText(string text)
        {
            bool visible = !string.IsNullOrEmpty(text);
            if (group != null)
                group.alpha = visible ? 1f : 0f;
            if (label != null && label.text != text)
                label.text = text;
        }
    }
}
