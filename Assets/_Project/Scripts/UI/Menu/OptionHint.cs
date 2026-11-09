using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Option row focus: its description (shown by OptionsMenu) and a highlight while hovered or while one of its
    /// controls is selected (keyboard / gamepad).
    /// </summary>
    public class OptionHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Graphic highlight;
        [SerializeField, Range(0f, 1f)] private float highlightAlpha = 0.07f;

        public string Description { get; set; }
        public bool Hovered { get; private set; }

        public void OnPointerEnter(PointerEventData e) => Hovered = true;
        public void OnPointerExit(PointerEventData e) => Hovered = false;

        private void OnDisable() => Hovered = false;

        public void SetHighlighted(bool value)
        {
            if (highlight == null)
                return;
            var color = highlight.color;
            float target = value ? highlightAlpha : 0f;
            color.a = Mathf.MoveTowards(color.a, target, highlightAlpha * 10f * Time.unscaledDeltaTime);
            highlight.color = color;
        }
    }
}
