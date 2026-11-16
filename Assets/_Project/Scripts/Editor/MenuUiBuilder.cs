using RiskOfMagic.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.Editor
{
    /// <summary>
    /// UI Toolkit menu assets: the shared PanelSettings (created once, then yours to tune) and Resources/MenuUI
    /// (MenuUiAssets: screens and templates, only empty slots are filled so a slot pointed elsewhere is kept).
    /// The UXML / USS files themselves are hand-written in UI/Menus and never generated.
    /// </summary>
    public static class MenuUiBuilder
    {
        public const string Folder = "Assets/_Project/UI/Menus";
        public const string PanelSettingsPath = Folder + "/MenuPanelSettings.asset";
        public const string ThemePath = Folder + "/MenuTheme.tss";
        public const string AssetsPath = "Assets/_Project/Resources/MenuUI.asset";

        [MenuItem("Tools/Risk of Magic/Refresh Menu UI Assets")]
        public static void RefreshMenu() => Selection.activeObject = EnsureAssets();

        public static MenuUiAssets EnsureAssets()
        {
            AssetDatabase.ImportAsset(Folder, ImportAssetOptions.ImportRecursive);
            var panel = EnsurePanelSettings();

            var assets = AssetDatabase.LoadAssetAtPath<MenuUiAssets>(AssetsPath);
            if (assets == null)
            {
                assets = ScriptableObject.CreateInstance<MenuUiAssets>();
                AssetDatabase.CreateAsset(assets, AssetsPath);
            }
            if (assets.panelSettings == null)
                assets.panelSettings = panel;
            assets.mainMenu = Fill(assets.mainMenu, "MainMenu.uxml");
            assets.pauseMenu = Fill(assets.pauseMenu, "PauseMenu.uxml");
            assets.loadingScreen = Fill(assets.loadingScreen, "LoadingScreen.uxml");
            assets.lobbyCard = Fill(assets.lobbyCard, "Templates/LobbyCard.uxml");
            assets.optionChoice = Fill(assets.optionChoice, "Templates/OptionChoice.uxml");
            assets.optionSlider = Fill(assets.optionSlider, "Templates/OptionSlider.uxml");
            assets.optionToggle = Fill(assets.optionToggle, "Templates/OptionToggle.uxml");
            assets.optionKey = Fill(assets.optionKey, "Templates/OptionKey.uxml");
            EditorUtility.SetDirty(assets);
            AssetDatabase.SaveAssets();
            return assets;
        }

        private static VisualTreeAsset Fill(VisualTreeAsset current, string relativePath)
        {
            if (current != null)
                return current;
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Folder}/{relativePath}");
            if (asset == null)
                Debug.LogError($"[Risk of Magic] Missing UXML: {Folder}/{relativePath}");
            return asset;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            bool created = settings == null;
            if (created)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                settings.referenceResolution = new Vector2Int(1920, 1080);
                settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                settings.match = 0.5f;
                settings.sortingOrder = 100f; // above the uGUI HUD canvas
                settings.clearColor = false;
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }
            if (settings.themeStyleSheet == null)
            {
                settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
                if (settings.themeStyleSheet == null)
                    Debug.LogError($"[Risk of Magic] Missing theme: {ThemePath}");
                EditorUtility.SetDirty(settings);
            }
            return settings;
        }
    }
}
