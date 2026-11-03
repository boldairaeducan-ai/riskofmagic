using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Soft colored screen tint while the local player is shielded, flashing when the shield absorbs a hit.</summary>
    public class ShieldOverlayWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField, Range(0f, 1f)] private float idleAlpha = 0.35f;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 4f;
        [SerializeField, Min(0.01f)] private float hitFlashDuration = 0.25f;

        private float hitAt = float.NegativeInfinity;

        protected override void OnBind() => Player.Health.Damaged += OnDamaged;

        protected override void OnUnbind()
        {
            Player.Health.Damaged -= OnDamaged;
            if (group != null)
                group.alpha = 0f;
        }

        private void OnDamaged(Combat.DamageInfo info)
        {
            if (Player.Health.IsShielded)
                hitAt = Time.time;
        }

        protected override void Tick()
        {
            if (group == null)
                return;
            float flash = Mathf.Clamp01(1f - (Time.time - hitAt) / hitFlashDuration);
            float target = Player.Health.IsShielded ? Mathf.Max(idleAlpha, flash) : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, fadeSpeed * Time.deltaTime);
        }
    }
}
