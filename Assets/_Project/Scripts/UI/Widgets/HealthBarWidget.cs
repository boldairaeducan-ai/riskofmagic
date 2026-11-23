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

        [SerializeField] private Color barrierColor = new(1f, 0.72f, 0.2f, 0.9f);

        private float trailRatio;
        private RectTransform barrierFill;

        protected override void OnBind()
        {
            EnsureBarrierFill();
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
            UpdateLabel(current, max);
        }

        private void UpdateLabel(float current, float max)
        {
            if (label == null)
                return;
            string text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            if (Player != null && Player.Health.Barrier > 0f)
                text += $"  <color=#FFB833>+{Mathf.CeilToInt(Player.Health.Barrier)}</color>";
            if (Player != null && Player.Health.IsShielded)
                text += $"  <size=80%>· Bouclier {Player.Health.ShieldTimeRemaining:0.0}s</size>";
            if (label.text != text)
                label.text = text;
        }

        protected override void Tick()
        {
            UpdateLabel(Player.Health.Current, Player.Health.Max);
            float ratio = Player.Health.Normalized;
            trailRatio = trailRatio > ratio ? Mathf.MoveTowards(trailRatio, ratio, trailSpeed * Time.deltaTime) : ratio;
            SetFill(trail, trailRatio);
            UpdateBarrier(ratio);
        }

        /// <summary>Barrier segment drawn right after the health fill (created at runtime: no HUD rebuild needed).</summary>
        private void EnsureBarrierFill()
        {
            if (barrierFill != null || fill == null)
                return;
            var go = new GameObject("BarrierFill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            barrierFill = (RectTransform)go.transform;
            barrierFill.SetParent(fill.parent, false);
            barrierFill.SetSiblingIndex(fill.GetSiblingIndex() + 1);
            var image = go.GetComponent<UnityEngine.UI.Image>();
            image.color = barrierColor;
            image.raycastTarget = false;
            go.SetActive(false);
        }

        private void UpdateBarrier(float healthRatio)
        {
            if (barrierFill == null)
                return;
            float max = Player.Health.Max;
            float barrierRatio = max > 0f ? Mathf.Clamp01(Player.Health.Barrier / max) : 0f;
            bool visible = barrierRatio > 0f;
            if (barrierFill.gameObject.activeSelf != visible)
                barrierFill.gameObject.SetActive(visible);
            if (!visible)
                return;
            float start = Mathf.Min(healthRatio, 1f - barrierRatio);
            barrierFill.anchorMin = new Vector2(start, 0f);
            barrierFill.anchorMax = new Vector2(start + barrierRatio, 1f);
            barrierFill.offsetMin = barrierFill.offsetMax = Vector2.zero;
        }
    }
}
