using UnityEngine;

namespace RiskOfMagic.UI
{
    public class ChargeBarWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform fill;

        protected override void OnUnbind() => SetVisible(false);

        protected override void Tick()
        {
            var skills = Player.Skills;
            bool charging = skills != null && skills.IsCharging;
            SetVisible(charging);
            if (charging)
                SetFill(fill, skills.ChargeProgress);
        }

        private void SetVisible(bool visible)
        {
            if (group != null)
                group.alpha = visible ? 1f : 0f;
        }
    }
}
