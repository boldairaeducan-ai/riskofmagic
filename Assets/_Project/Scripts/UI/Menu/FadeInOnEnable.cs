using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Screen transition: fades in and slides from a small offset each time the object is enabled.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class FadeInOnEnable : MonoBehaviour
    {
        [SerializeField] private Vector2 offset = new(-24f, 0f);
        [SerializeField, Min(0.01f)] private float duration = 0.22f;

        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 basePosition;
        private float elapsed;
        private bool captured;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
        }

        private void OnEnable()
        {
            if (!captured)
            {
                basePosition = rect.anchoredPosition;
                captured = true;
            }
            elapsed = 0f;
            Apply(0f);
        }

        private void OnDisable() => Apply(1f);

        private void Update()
        {
            if (elapsed >= duration)
                return;
            elapsed += Time.unscaledDeltaTime;
            Apply(Mathf.Clamp01(elapsed / duration));
        }

        private void Apply(float t)
        {
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            if (group != null)
                group.alpha = eased;
            if (rect != null)
                rect.anchoredPosition = basePosition + offset * (1f - eased);
        }
    }
}
