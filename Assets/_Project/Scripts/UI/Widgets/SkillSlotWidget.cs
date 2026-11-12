using RiskOfMagic.Core;
using RiskOfMagic.Player;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>One skill: icon (SkillDefinition.icon), cooldown overlay, stocks, key hint. Driven by SkillBarWidget.</summary>
    public class SkillSlotWidget : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private SkillSlot slot;
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMP_Text placeholderName;
        [SerializeField] private RectTransform cooldownOverlay;
        [SerializeField] private TMP_Text cooldownLabel;
        [SerializeField] private TMP_Text stockLabel;
        [SerializeField] private TMP_Text keyLabel;
        [SerializeField] private string keyText = "LMB";

        private PlayerAvatar player;

        public SkillSlot Slot => slot;

        public void Bind(PlayerAvatar avatar)
        {
            player = avatar;
            RefreshKey();
            RiskOfMagic.Player.GameInput.BindingsChanged -= RefreshKey;
            RiskOfMagic.Player.GameInput.BindingsChanged += RefreshKey;
            RefreshDefinition();
        }

        public void Unbind()
        {
            player = null;
            RiskOfMagic.Player.GameInput.BindingsChanged -= RefreshKey;
        }

        private void OnDestroy() => RiskOfMagic.Player.GameInput.BindingsChanged -= RefreshKey;

        /// <summary>Follows the bindings (Options → Raccourcis) and the keyboard layout.</summary>
        private void RefreshKey()
        {
            if (keyLabel == null)
                return;
            string bound = RiskOfMagic.Player.GameInput.SkillKey((int)slot);
            keyLabel.text = string.IsNullOrEmpty(bound) ? keyText : bound;
        }

        public void RefreshDefinition()
        {
            var state = player != null && player.Skills != null ? player.Skills.GetSlot(slot) : null;
            gameObject.SetActive(state != null);
            if (state == null)
                return;

            var definition = state.Definition;
            var sprite = definition.icon != null ? definition.icon : theme != null ? theme.defaultSkillIcon : null;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.08f);
            }
            if (placeholderName != null)
            {
                placeholderName.text = definition.displayName;
                placeholderName.gameObject.SetActive(sprite == null);
            }
        }

        private void Update()
        {
            if (player == null || player.Skills == null)
                return;
            var state = player.Skills.GetSlot(slot);
            if (state == null)
                return;

            float multiplier = player.Stats.CooldownMultiplier;
            bool ready = state.HasStock;
            float remaining = state.GetCooldownRemaining(multiplier);

            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(!ready);
                if (!ready)
                    HudFill(cooldownOverlay, 1f - state.GetRechargeProgress(multiplier));
            }
            if (cooldownLabel != null)
                cooldownLabel.text = ready ? string.Empty : remaining >= 1f ? Mathf.CeilToInt(remaining).ToString() : remaining.ToString("0.0");
            if (stockLabel != null)
                stockLabel.text = state.MaxStock > 1 ? state.Stock.ToString() : string.Empty;
        }

        private static void HudFill(RectTransform rect, float ratio)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(1f, Mathf.Clamp01(ratio));
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
