using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Reddish screen while downed, with the bleed-out timer and the revive progress.</summary>
    public class DownedOverlayWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text label;
        [SerializeField] private CanvasGroup reviveGroup;
        [SerializeField] private RectTransform reviveFill;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 3f;
        [SerializeField, Tooltip("Pulse of the red overlay as the timer runs out.")] private float pulseSpeed = 4f;

        protected override void OnUnbind() => SetVisible(0f, false);

        protected override void Tick()
        {
            var downed = Player.Downed;
            bool isDowned = downed != null && downed.IsDowned;
            float target = isDowned ? 1f : 0f;
            if (isDowned && downed.BleedOutRemaining < 10f)
                target = 0.75f + 0.25f * Mathf.Sin(Time.time * pulseSpeed);
            float alpha = group != null ? Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime) : target;
            SetVisible(alpha, isDowned && downed.IsBeingRevived);

            if (!isDowned)
                return;
            if (label != null)
                label.text = downed.IsBeingRevived
                    ? "Un allié vous relève..."
                    : $"À TERRE\n<size=60%>Un allié peut vous relever · {Mathf.CeilToInt(downed.BleedOutRemaining)} s</size>";
            SetFill(reviveFill, downed.ReviveProgress);
        }

        private void SetVisible(float alpha, bool reviving)
        {
            if (group != null)
                group.alpha = alpha;
            if (reviveGroup != null)
                reviveGroup.alpha = reviving ? 1f : 0f;
        }
    }
}
