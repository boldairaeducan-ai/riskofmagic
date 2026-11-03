using System;
using UnityEngine;

namespace RiskOfMagic.Settings
{
    /// <summary>
    /// Player options, saved in PlayerPrefs and applied at startup. Change a value, then call Apply():
    /// it applies display/audio settings, saves, and raises Changed (camera FOV, sensitivity, UI...).
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "rom.settings.";
        public static readonly int[] FrameRateLimits = { 30, 60, 120, 144, 240, 0 };

        /// <summary>Raised after Apply().</summary>
        public static event Action Changed;

        // Display
        public static int ResolutionWidth { get; set; }
        public static int ResolutionHeight { get; set; }
        public static FullScreenMode DisplayMode { get; set; } = FullScreenMode.FullScreenWindow;
        public static int QualityLevel { get; set; }
        public static bool VSync { get; set; } = true;
        /// <summary>0 = unlimited.</summary>
        public static int FrameRateLimit { get; set; }
        public static float FieldOfView { get; set; } = 80f;

        // Audio (0..1). Music/effects are exposed for the future audio mixer.
        public static float MasterVolume { get; set; } = 1f;
        public static float MusicVolume { get; set; } = 0.8f;
        public static float EffectsVolume { get; set; } = 1f;

        // Controls (multipliers of the base sensitivities in PlayerLook)
        public static float MouseSensitivity { get; set; } = 1f;
        public static float StickSensitivity { get; set; } = 1f;
        public static bool InvertY { get; set; }

        // Interface
        public static bool ShowDamageNumbers { get; set; } = true;
        public static bool ShowHitMarkers { get; set; } = true;
        /// <summary>0 = no camera shake.</summary>
        public static float CameraShake { get; set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            Changed = null;
            Load();
            ApplyDisplayAndAudio();
        }

        public static void Load()
        {
            var current = Screen.currentResolution;
            ResolutionWidth = PlayerPrefs.GetInt(Prefix + "width", current.width);
            ResolutionHeight = PlayerPrefs.GetInt(Prefix + "height", current.height);
            DisplayMode = (FullScreenMode)PlayerPrefs.GetInt(Prefix + "displayMode", (int)FullScreenMode.FullScreenWindow);
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(Prefix + "quality", QualitySettings.GetQualityLevel()), 0, QualitySettings.names.Length - 1);
            VSync = PlayerPrefs.GetInt(Prefix + "vsync", 1) == 1;
            FrameRateLimit = PlayerPrefs.GetInt(Prefix + "fpsLimit", 0);
            FieldOfView = PlayerPrefs.GetFloat(Prefix + "fov", 80f);

            MasterVolume = PlayerPrefs.GetFloat(Prefix + "masterVolume", 1f);
            MusicVolume = PlayerPrefs.GetFloat(Prefix + "musicVolume", 0.8f);
            EffectsVolume = PlayerPrefs.GetFloat(Prefix + "effectsVolume", 1f);

            MouseSensitivity = PlayerPrefs.GetFloat(Prefix + "mouseSensitivity", 1f);
            StickSensitivity = PlayerPrefs.GetFloat(Prefix + "stickSensitivity", 1f);
            InvertY = PlayerPrefs.GetInt(Prefix + "invertY", 0) == 1;

            ShowDamageNumbers = PlayerPrefs.GetInt(Prefix + "damageNumbers", 1) == 1;
            ShowHitMarkers = PlayerPrefs.GetInt(Prefix + "hitMarkers", 1) == 1;
            CameraShake = PlayerPrefs.GetFloat(Prefix + "cameraShake", 1f);
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(Prefix + "width", ResolutionWidth);
            PlayerPrefs.SetInt(Prefix + "height", ResolutionHeight);
            PlayerPrefs.SetInt(Prefix + "displayMode", (int)DisplayMode);
            PlayerPrefs.SetInt(Prefix + "quality", QualityLevel);
            PlayerPrefs.SetInt(Prefix + "vsync", VSync ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "fpsLimit", FrameRateLimit);
            PlayerPrefs.SetFloat(Prefix + "fov", FieldOfView);

            PlayerPrefs.SetFloat(Prefix + "masterVolume", MasterVolume);
            PlayerPrefs.SetFloat(Prefix + "musicVolume", MusicVolume);
            PlayerPrefs.SetFloat(Prefix + "effectsVolume", EffectsVolume);

            PlayerPrefs.SetFloat(Prefix + "mouseSensitivity", MouseSensitivity);
            PlayerPrefs.SetFloat(Prefix + "stickSensitivity", StickSensitivity);
            PlayerPrefs.SetInt(Prefix + "invertY", InvertY ? 1 : 0);

            PlayerPrefs.SetInt(Prefix + "damageNumbers", ShowDamageNumbers ? 1 : 0);
            PlayerPrefs.SetInt(Prefix + "hitMarkers", ShowHitMarkers ? 1 : 0);
            PlayerPrefs.SetFloat(Prefix + "cameraShake", CameraShake);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            foreach (var key in new[]
                     {
                         "width", "height", "displayMode", "quality", "vsync", "fpsLimit", "fov", "masterVolume", "musicVolume",
                         "effectsVolume", "mouseSensitivity", "stickSensitivity", "invertY", "damageNumbers", "hitMarkers", "cameraShake"
                     })
                PlayerPrefs.DeleteKey(Prefix + key);
            Load();
            Apply();
        }

        /// <summary>Applies display/audio, saves and notifies listeners.</summary>
        public static void Apply()
        {
            ApplyDisplayAndAudio();
            Save();
            Changed?.Invoke();
        }

        private static void ApplyDisplayAndAudio()
        {
            if (ResolutionWidth > 0 && ResolutionHeight > 0)
                Screen.SetResolution(ResolutionWidth, ResolutionHeight, DisplayMode);
            QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = FrameRateLimit > 0 ? FrameRateLimit : -1;
            AudioListener.volume = Mathf.Clamp01(MasterVolume);
        }
    }
}
