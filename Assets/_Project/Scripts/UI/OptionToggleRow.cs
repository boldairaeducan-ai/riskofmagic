using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>Option row with an animated on/off switch.</summary>
    public class OptionToggleRow : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text stateLabel;
        [SerializeField] private Button button;
        [SerializeField] private Image track;
        [SerializeField] private RectTransform knob;
        [SerializeField] private float knobTravel = 22f;

        private Func<bool> get;
        private Action<bool> set;
        private float t;

        public void Setup(string text, Func<bool> getter, Action<bool> setter)
        {
            if (label != null)
                label.text = text;
            get = getter;
            set = setter;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                set(!get());
                Refresh();
            });
            Refresh();
            t = get() ? 1f : 0f;
            Apply();
        }

        public void Refresh()
        {
            if (stateLabel != null && get != null)
                stateLabel.text = get() ? "Activé" : "Désactivé";
        }

        private void Update()
        {
            if (get == null)
                return;
            t = Mathf.MoveTowards(t, get() ? 1f : 0f, 8f * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            float eased = Mathf.SmoothStep(0f, 1f, t);
            if (knob != null)
                knob.anchoredPosition = new Vector2(Mathf.Lerp(-knobTravel * 0.5f, knobTravel * 0.5f, eased), 0f);
            if (track != null && theme != null)
                track.color = Color.Lerp(theme.barBackgroundColor, theme.accentColor, eased);
        }
    }
}
