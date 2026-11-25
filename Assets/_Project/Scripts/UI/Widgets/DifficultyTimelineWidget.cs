using RiskOfMagic.Run;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Risk of Rain style difficulty timeline (top left): a strip of named tiers scrolling under a fixed marker as the
    /// run difficulty grows (time, stages). One tier = <see cref="tierStep"/> of RunDifficulty.Progress above the start.
    /// Segments are built at runtime: only the viewport and labels come from the HUD prefab.
    /// </summary>
    public class DifficultyTimelineWidget : HudWidget
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private TMP_Text tierLabel;
        [SerializeField] private TMP_Text detailsLabel;
        [SerializeField, Min(20f)] private float segmentWidth = 120f;
        [SerializeField, Min(0.05f), Tooltip("Run progress per tier (Normal: +0.2 per minute, so 1 = ~5 min).")] private float tierStep = 1f;
        [SerializeField, Range(0f, 1f), Tooltip("Marker position in the viewport.")] private float markerAnchor = 0.3f;

        private static readonly string[] TierNames =
        {
            "Paisible", "Agité", "Hostile", "Périlleux", "Brutal", "Funeste", "Cauchemar", "Apocalypse", "Fin des temps"
        };

        private static readonly Color[] TierColors =
        {
            new(0.30f, 0.62f, 0.32f), new(0.55f, 0.68f, 0.25f), new(0.80f, 0.70f, 0.20f), new(0.88f, 0.50f, 0.16f),
            new(0.85f, 0.28f, 0.14f), new(0.70f, 0.12f, 0.16f), new(0.50f, 0.10f, 0.35f), new(0.30f, 0.06f, 0.30f),
            new(0.10f, 0.02f, 0.06f)
        };

        private RectTransform strip;
        private int lastTier = -1;
        private int lastLevel = -1;

        private void Awake() => BuildStrip();

        private void BuildStrip()
        {
            if (viewport == null || strip != null)
                return;
            strip = NewRect("Strip", viewport);
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(0f, 1f);
            strip.pivot = new Vector2(0f, 0.5f);
            strip.sizeDelta = new Vector2(segmentWidth * (TierNames.Length + 2), 0f);

            for (int i = 0; i < TierNames.Length; i++)
            {
                bool last = i == TierNames.Length - 1;
                var segment = NewRect(TierNames[i], strip);
                segment.anchorMin = new Vector2(0f, 0f);
                segment.anchorMax = new Vector2(0f, 1f);
                segment.pivot = new Vector2(0f, 0.5f);
                segment.anchoredPosition = new Vector2(i * segmentWidth, 0f);
                segment.sizeDelta = new Vector2((last ? 3f : 1f) * segmentWidth - 2f, 0f); // the last tier never ends
                var image = segment.gameObject.AddComponent<Image>();
                image.color = TierColors[i];
                image.raycastTarget = false;

                var label = NewRect("Label", segment);
                label.anchorMin = Vector2.zero;
                label.anchorMax = new Vector2(last ? 1f / 3f : 1f, 1f);
                label.offsetMin = new Vector2(4f, 0f);
                label.offsetMax = new Vector2(-4f, 0f);
                var text = label.gameObject.AddComponent<TextMeshProUGUI>();
                if (theme != null && theme.bodyFont != null)
                    text.font = theme.bodyFont;
                text.text = TierNames[i];
                text.fontSize = 15f;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(1f, 1f, 1f, 0.9f);
                text.raycastTarget = false;
            }

            // Fixed marker: "now".
            var marker = NewRect("Marker", viewport);
            marker.anchorMin = new Vector2(markerAnchor, 0f);
            marker.anchorMax = new Vector2(markerAnchor, 1f);
            marker.pivot = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(3f, 6f);
            var markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.color = Color.white;
            markerImage.raycastTarget = false;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        protected override void Tick()
        {
            var run = RunDifficulty.Instance;
            if (run == null || !run.IsSpawned || strip == null)
                return;

            float start = RiskOfMagic.Core.GameBalance.Instance.difficulty.startingCoefficient;
            float position = Mathf.Max(0f, (run.Progress - start) / tierStep);
            int tier = Mathf.Min(TierNames.Length - 1, Mathf.FloorToInt(position));
            float width = viewport.rect.width;
            strip.anchoredPosition = new Vector2(width * markerAnchor - position * segmentWidth, 0f);

            if (tier != lastTier && tierLabel != null)
            {
                tierLabel.text = TierNames[tier];
                tierLabel.color = Color.Lerp(TierColors[tier], Color.white, 0.35f);
            }
            lastTier = tier;

            int level = run.EnemyLevel;
            if (level != lastLevel && detailsLabel != null)
                detailsLabel.text = $"Niveau ennemis {level}";
            lastLevel = level;
        }
    }
}
