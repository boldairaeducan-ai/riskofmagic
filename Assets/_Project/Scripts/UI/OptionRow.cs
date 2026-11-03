using System;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>One option: label + ◀ value ▶. The behaviour is injected by OptionsMenu.</summary>
    public class OptionRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private UnityEngine.UI.Button previousButton;
        [SerializeField] private UnityEngine.UI.Button nextButton;

        private Func<string> display;
        private Action<int> step;

        public void Setup(string text, Func<string> displayValue, Action<int> onStep)
        {
            if (label != null)
                label.text = text;
            display = displayValue;
            step = onStep;
            if (previousButton != null)
            {
                previousButton.onClick.RemoveAllListeners();
                previousButton.onClick.AddListener(() => Step(-1));
            }
            if (nextButton != null)
            {
                nextButton.onClick.RemoveAllListeners();
                nextButton.onClick.AddListener(() => Step(1));
            }
            Refresh();
        }

        public void Refresh()
        {
            if (valueLabel != null && display != null)
                valueLabel.text = display();
        }

        private void Step(int direction)
        {
            step?.Invoke(direction);
            Refresh();
        }
    }
}
