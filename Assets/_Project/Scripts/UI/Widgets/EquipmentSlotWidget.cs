using RiskOfMagic.Equipment;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Equipment slot (key F): icon (EquipmentDefinition.icon), cooldown overlay and timer.</summary>
    public class EquipmentSlotWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private UnityEngine.UI.Image icon;
        [SerializeField] private TMP_Text placeholderName;
        [SerializeField] private RectTransform cooldownOverlay;
        [SerializeField] private TMP_Text cooldownLabel;

        private PlayerEquipment equipment;

        protected override void OnBind()
        {
            equipment = Player.Equipment;
            if (equipment != null)
                equipment.Changed += Refresh;
            Refresh();
        }

        protected override void OnUnbind()
        {
            if (equipment != null)
                equipment.Changed -= Refresh;
            equipment = null;
            if (group != null)
                group.alpha = 0f;
        }

        private void Refresh()
        {
            var current = equipment != null ? equipment.Current : null;
            if (group != null)
                group.alpha = current != null ? 1f : 0f;
            if (current == null)
                return;

            var sprite = current.icon;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.color = sprite != null ? Color.white : new Color(1f, 0.55f, 0.25f, 0.15f);
            }
            if (placeholderName != null)
            {
                placeholderName.text = current.displayName;
                placeholderName.gameObject.SetActive(sprite == null);
            }
        }

        protected override void Tick()
        {
            if (equipment == null || equipment.Current == null)
                return;
            float remaining = equipment.CooldownRemaining;
            bool ready = remaining <= 0f;
            if (cooldownOverlay != null)
            {
                cooldownOverlay.gameObject.SetActive(!ready);
                if (!ready)
                    SetFill(cooldownOverlay, 1f - equipment.CooldownProgress, vertical: true);
            }
            if (cooldownLabel != null)
                cooldownLabel.text = ready ? string.Empty : Mathf.CeilToInt(remaining).ToString();
        }
    }
}
