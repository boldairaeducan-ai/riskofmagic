using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Pulls font/color from the HudTheme.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public class ThemedText : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private HudFont font;
        [SerializeField] private bool applyColor = true;
        [SerializeField] private HudColor color = HudColor.Text;

        private void OnEnable()
        {
            Apply();
#if UNITY_EDITOR
            HudTheme.Changed += ApplyDelayed;
#endif
        }

#if UNITY_EDITOR
        private void OnDisable() => HudTheme.Changed -= ApplyDelayed;
        private void OnValidate() => ApplyDelayed();

        // Changing graphics inside OnValidate triggers warnings: defer to the next editor tick.
        private void ApplyDelayed() => UnityEditor.EditorApplication.delayCall += Apply;
#endif

        public void Apply()
        {
            if (theme == null || this == null)
                return;
            var text = GetComponent<TMP_Text>();
            var value = theme.GetFont(font);
            if (value != null)
                text.font = value;
            if (applyColor)
                text.color = theme.GetColor(color);
        }
    }
}
