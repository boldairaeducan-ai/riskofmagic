using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Base for HUD elements driven by the local player. Bound/unbound by HudController.</summary>
    public abstract class HudWidget : MonoBehaviour
    {
        [SerializeField] protected HudTheme theme;

        protected PlayerAvatar Player { get; private set; }

        public void Bind(PlayerAvatar player)
        {
            Player = player;
            OnBind();
        }

        public void Unbind()
        {
            if (Player != null)
                OnUnbind();
            Player = null;
        }

        protected virtual void OnBind() { }
        protected virtual void OnUnbind() { }
        protected virtual void Tick() { }

        private void Update()
        {
            if (Player != null)
                Tick();
        }

        protected static void SetFill(RectTransform fill, float ratio, bool vertical = false)
        {
            if (fill == null)
                return;
            ratio = Mathf.Clamp01(ratio);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = vertical ? new Vector2(1f, ratio) : new Vector2(ratio, 1f);
            fill.offsetMin = fill.offsetMax = Vector2.zero;
        }
    }
}
