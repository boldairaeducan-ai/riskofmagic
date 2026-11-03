using RiskOfMagic.Combat;
using RiskOfMagic.Player;
using RiskOfMagic.Settings;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Crosshair "X" flashing when the local player hits (crit: accent, kill: danger and bigger).</summary>
    public class HitMarkerWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private UnityEngine.UI.Graphic[] bars = new UnityEngine.UI.Graphic[0];
        [SerializeField, Min(0.01f)] private float hitDuration = 0.15f;
        [SerializeField, Min(0.01f)] private float killDuration = 0.35f;
        [SerializeField] private float killScale = 1.5f;

        private float shownAt = float.NegativeInfinity;
        private float duration = 0.15f;
        private float scale = 1f;
        private bool lastWasKill;

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
            Health.AnyDied += OnAnyDied;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDied -= OnAnyDied;
        }

        private static bool IsMine(in DamageInfo info)
            => PlayerAvatar.Local != null && info.HasSource && info.SourceId == PlayerAvatar.Local.NetworkObjectId;

        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            // Damage over time and item procs don't flash the marker (too noisy).
            if (!GameSettings.ShowHitMarkers || !IsMine(info) || info.ProcCoefficient <= 0f || (Time.time - shownAt < duration && lastWasKill))
                return;
            Show(info.IsCritical && theme != null ? theme.accentColor : theme != null ? theme.textColor : Color.white, hitDuration, 1f, false);
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            if (GameSettings.ShowHitMarkers && IsMine(info))
                Show(theme != null ? theme.dangerColor : Color.red, killDuration, killScale, true);
        }

        private void Show(Color color, float showDuration, float showScale, bool kill)
        {
            foreach (var bar in bars)
                if (bar != null)
                    bar.color = color;
            shownAt = Time.time;
            duration = showDuration;
            scale = showScale;
            lastWasKill = kill;
        }

        protected override void OnUnbind()
        {
            if (group != null)
                group.alpha = 0f;
        }

        protected override void Tick()
        {
            float t = (Time.time - shownAt) / duration;
            if (group != null)
                group.alpha = t < 1f ? 1f - t * t : 0f;
            transform.localScale = Vector3.one * (scale * Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(t * 3f)));
        }
    }
}
