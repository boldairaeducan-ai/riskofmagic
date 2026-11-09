using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Text-only menu entry: on hover/selection the label slides right, brightens to the accent color and an accent bar
    /// grows on its left; pressing squeezes it slightly. Mouse, keyboard and gamepad give the same feedback.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class MenuButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private RectTransform label;
        [SerializeField] private TMPro.TMP_Text text;
        [SerializeField] private RectTransform accentBar;
        [SerializeField] private float slide = 14f;
        [SerializeField] private float speed = 14f;

        private Button button;
        private bool hovered, selected, pressed;
        private float t;
        private Vector2 labelBase;

        /// <summary>Keeps the highlighted look (current tab).</summary>
        public bool Pinned { get; set; }
        public Button Button => button != null ? button : button = GetComponent<Button>();
        public TMPro.TMP_Text Text => text;

        private bool Active => Pinned || (Button.IsInteractable() && (hovered || selected));

        private void Awake()
        {
            button = GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (label != null)
                labelBase = label.anchoredPosition;
        }

        private void OnDisable()
        {
            hovered = selected = pressed = false;
            t = Pinned ? 1f : 0f;
            Apply();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            var system = EventSystem.current;
            if (!button.IsInteractable() || system == null)
                return;
            var current = system.currentSelectedGameObject;
            if (current != null && current.TryGetComponent(out TMPro.TMP_InputField field) && field.isFocused)
                return; // don't steal focus from a field being typed in
            system.SetSelectedGameObject(gameObject); // mouse and keys share one highlight
        }

        public void OnPointerExit(PointerEventData e) => hovered = false;
        public void OnSelect(BaseEventData e) => selected = true;
        public void OnDeselect(BaseEventData e) => selected = false;
        public void OnPointerDown(PointerEventData e) => pressed = true;
        public void OnPointerUp(PointerEventData e) => pressed = false;

        private void Update()
        {
            t = Mathf.MoveTowards(t, Active ? 1f : 0f, speed * Time.unscaledDeltaTime);
            Apply();
        }

        private void Apply()
        {
            float eased = 1f - (1f - t) * (1f - t);
            if (label != null)
            {
                label.anchoredPosition = labelBase + new Vector2(slide * eased, 0f);
                label.localScale = Vector3.one * (pressed && Active ? 0.97f : 1f);
            }
            if (text != null && theme != null)
            {
                var normal = button != null && !button.IsInteractable() ? theme.textMutedColor * new Color(1f, 1f, 1f, 0.5f) : theme.textColor;
                text.color = Color.Lerp(normal, theme.accentColor, eased);
            }
            if (accentBar != null)
                accentBar.localScale = new Vector3(eased, 1f, 1f);
        }
    }
}
