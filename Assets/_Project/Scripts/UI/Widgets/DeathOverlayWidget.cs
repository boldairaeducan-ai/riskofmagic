using UnityEngine;

namespace RiskOfMagic.UI
{
    public class DeathOverlayWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 2f;

        protected override void OnUnbind()
        {
            if (group != null)
                group.alpha = 0f;
        }

        protected override void Tick()
        {
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, Player.IsAlive ? 0f : 1f, fadeSpeed * Time.deltaTime);
        }
    }
}
