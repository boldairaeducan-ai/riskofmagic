using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Registry of the UI Toolkit menu assets (Resources/MenuUI): panel settings, screen layouts and the row/card
    /// templates cloned at runtime. Filled by the editor builder (empty slots only): point a slot to another UXML to
    /// swap a layout without touching code.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/UI/Menu UI Assets", fileName = "MenuUI")]
    public class MenuUiAssets : ScriptableObject
    {
        public const string ResourcePath = "MenuUI";

        [Tooltip("Shared by every menu document (theme, scaling, sort order above the uGUI HUD).")]
        public PanelSettings panelSettings;

        [Header("Screens")]
        public VisualTreeAsset mainMenu;
        public VisualTreeAsset pauseMenu;
        public VisualTreeAsset loadingScreen;

        [Header("Templates (cloned at runtime)")]
        public VisualTreeAsset lobbyCard;
        public VisualTreeAsset optionChoice;
        public VisualTreeAsset optionSlider;
        public VisualTreeAsset optionToggle;
        public VisualTreeAsset optionKey;

        private static MenuUiAssets instance;

        public static MenuUiAssets Instance => instance != null ? instance : instance = Resources.Load<MenuUiAssets>(ResourcePath);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => instance = null;
    }
}
