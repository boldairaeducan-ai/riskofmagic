using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    public class HealthBarWidget : HudWidget
    {
        [SerializeField] private RectTransform fill;
        [SerializeField] private UnityEngine.UI.Image fillImage;
        [SerializeField] private RectTransform trail;
        [SerializeField] private TMP_Text label;
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;
        [SerializeField, Min(0f)] private float trailSpeed = 0.6f;

        private float trailRatio;

        protected override void OnBind()
        {
            Player.Health.HealthChanged += OnHealthChanged;
            trailRatio = Player.Health.Normalized;
            OnHealthChanged(Player.Health.Current, Player.Health.Max);
        }

        protected override void OnUnbind() => Player.Health.HealthChanged -= OnHealthChanged;

        private void OnHealthChanged(float current, float max)
        {
            float ratio = max > 0f ? current / max : 0f;
            SetFill(fill, ratio);
            if (fillImage != null && theme != null)
                fillImage.color = ratio <= lowHealthThreshold ? theme.healthLowColor : theme.healthColor;
            if (label != null)
                label.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        protected override void Tick()
        {
            float ratio = Player.Health.Normalized;
            trailRatio = trailRatio > ratio ? Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime) : ratio;
            SetFill(trail, trailRatio);
        }
    }
}
