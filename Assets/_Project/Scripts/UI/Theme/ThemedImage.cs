using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Pulls sprite/color from the HudTheme. Remove it to customize one element by hand.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(UnityEngine.UI.Image))]
    public class ThemedImage : MonoBehaviour
    {
        [SerializeField] private HudTheme theme;
        [SerializeField] private bool applySprite = true;
        [SerializeField] private HudSprite sprite;
        [SerializeField] private bool applyColor = true;
        [SerializeField] private HudColor color;

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
            var image = GetComponent<UnityEngine.UI.Image>();
            if (applySprite)
            {
                var value = theme.GetSprite(sprite);
                image.sprite = value;
                image.type = value != null && value.border != Vector4.zero ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            }
            if (applyColor)
                image.color = theme.GetColor(color);
        }
    }
}
