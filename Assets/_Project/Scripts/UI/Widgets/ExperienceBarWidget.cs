using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Discreet level display: thin experience bar + "Niv. N"; a level up only makes the label pulse.</summary>
    public class ExperienceBarWidget : HudWidget
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField, Min(0.1f)] private float fillSpeed = 2f;
        [SerializeField, Min(0f)] private float pulseScale = 0.25f;
        [SerializeField, Min(0.05f)] private float pulseDuration = 0.6f;

        private float shownProgress;
        private int shownLevel;
        private float pulseStart = -10f;

        protected override void OnBind()
        {
            shownLevel = Player.Level;
            shownProgress = Player.Experience != null ? Player.Experience.Progress : 0f;
            Refresh(true);
        }

        protected override void Tick() => Refresh(false);

        private void Refresh(bool instant)
        {
            var experience = Player.Experience;
            int level = Player.Level;
            float target = experience != null ? experience.Progress : 0f;
            if (level != shownLevel)
            {
                // Level up: finish the bar, restart from empty, pulse the label (no popup).
                if (level > shownLevel)
                    pulseStart = Time.unscaledTime;
                shownLevel = level;
                shownProgress = 0f;
            }
            shownProgress = instant ? target : Mathf.MoveTowards(shownProgress, target, fillSpeed * Time.unscaledDeltaTime);
            SetFill(fill, shownProgress);

            if (levelLabel == null)
                return;
            string text = $"Niv. {level}";
            if (levelLabel.text != text)
                levelLabel.text = text;
            float t = (Time.unscaledTime - pulseStart) / pulseDuration;
            levelLabel.transform.localScale = Vector3.one * (1f + (t < 1f ? pulseScale * Mathf.Sin(t * Mathf.PI) : 0f));
        }
    }
}
