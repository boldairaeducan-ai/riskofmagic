using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>Option row with a slider (snapped to a step) and the formatted value.</summary>
    public class OptionSliderRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private Slider slider;

        private Func<float> get;
        private Action<float> set;
        private Func<float, string> format;
        private float step;
        private bool refreshing;

        public void Setup(string text, float min, float max, float stepSize, Func<float> getter, Action<float> setter, Func<float, string> formatter)
        {
            if (label != null)
                label.text = text;
            get = getter;
            set = setter;
            format = formatter;
            step = stepSize;
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = false;
            slider.onValueChanged.RemoveAllListeners();
            slider.onValueChanged.AddListener(OnChanged);
            Refresh();
        }

        public void Refresh()
        {
            refreshing = true;
            slider.SetValueWithoutNotify(get());
            refreshing = false;
            if (valueLabel != null)
                valueLabel.text = format(get());
        }

        private void OnChanged(float value)
        {
            if (refreshing)
                return;
            float snapped = step > 0f ? Mathf.Round(value / step) * step : value;
            snapped = Mathf.Clamp(snapped, slider.minValue, slider.maxValue);
            set(snapped);
            if (valueLabel != null)
                valueLabel.text = format(snapped);
        }
    }
}
