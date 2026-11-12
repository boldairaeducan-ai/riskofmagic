using RiskOfMagic.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>Options → Raccourcis: action name + its key; click the key, then press the new one (Escape cancels).</summary>
    public class OptionKeyRow : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text categoryLabel;
        [SerializeField] private Button keyButton;
        [SerializeField] private Image keyBackground;
        [SerializeField] private TMP_Text keyLabel;

        private GameInput.Rebindable binding;
        private bool waiting;
        private float pulse;

        public void Setup(GameInput.Rebindable target)
        {
            binding = target;
            if (label != null)
                label.text = target.Label;
            if (categoryLabel != null)
                categoryLabel.text = target.Category;
            keyButton.onClick.RemoveAllListeners();
            keyButton.onClick.AddListener(Listen);
            GameInput.BindingsChanged -= Refresh;
            GameInput.BindingsChanged += Refresh;
            Refresh();
        }

        private void OnDestroy() => GameInput.BindingsChanged -= Refresh;

        private void OnDisable()
        {
            if (waiting)
                GameInput.CancelRebind();
        }

        private void Listen()
        {
            if (binding == null || GameInput.IsRebinding)
                return;
            waiting = true;
            pulse = 0f;
            if (keyLabel != null)
                keyLabel.text = "Appuyez sur une touche…";
            GameInput.StartRebind(binding, () =>
            {
                waiting = false;
                Refresh();
            });
        }

        public void Refresh()
        {
            if (binding == null || keyLabel == null || waiting)
                return;
            keyLabel.text = binding.Display;
            if (keyBackground != null && theme != null)
                keyBackground.color = theme.buttonColor;
        }

        private void Update()
        {
            if (!waiting || keyBackground == null || theme == null)
                return;
            pulse += Time.unscaledDeltaTime * 4f;
            keyBackground.color = Color.Lerp(theme.buttonColor, theme.accentColor, 0.5f + 0.5f * Mathf.Sin(pulse));
        }
    }
}
