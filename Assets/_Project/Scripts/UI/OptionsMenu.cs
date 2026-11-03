using System;
using System.Collections.Generic;
using System.Linq;
using RiskOfMagic.Settings;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Options screen built at runtime from a header and a row template. Every change is applied and saved
    /// immediately (GameSettings.Apply). Add an option with AddChoice / AddRange / AddToggle in Build().
    /// </summary>
    public class OptionsMenu : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TMP_Text headerTemplate;
        [SerializeField] private OptionRow rowTemplate;
        [SerializeField] private UnityEngine.UI.Button resetButton;
        [SerializeField] private UnityEngine.UI.Button backButton;

        private readonly List<OptionRow> rows = new();
        private Action onClosed;
        private bool built;

        private void Awake()
        {
            if (resetButton != null)
                resetButton.onClick.AddListener(ResetToDefaults);
            if (backButton != null)
                backButton.onClick.AddListener(Close);
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            gameObject.SetActive(true);
            if (!built)
                Build();
            RefreshAll();
        }

        public void Close()
        {
            GameSettings.Save();
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        private void Build()
        {
            built = true;
            headerTemplate.gameObject.SetActive(false);
            rowTemplate.gameObject.SetActive(false);

            AddHeader("Affichage");
            var resolutions = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Distinct()
                .OrderBy(r => r.x).ThenBy(r => r.y)
                .ToList();
            if (resolutions.Count == 0)
                resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            AddChoice("Résolution", resolutions.Select(r => $"{r.x} x {r.y}").ToArray(),
                () => Mathf.Max(0, resolutions.FindIndex(r => r.x == GameSettings.ResolutionWidth && r.y == GameSettings.ResolutionHeight)),
                i =>
                {
                    GameSettings.ResolutionWidth = resolutions[i].x;
                    GameSettings.ResolutionHeight = resolutions[i].y;
                });

            var modes = new[] { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
            AddChoice("Mode d'affichage", new[] { "Plein écran", "Plein écran fenêtré", "Fenêtré" },
                () => Mathf.Max(0, Array.IndexOf(modes, GameSettings.DisplayMode)),
                i => GameSettings.DisplayMode = modes[i]);

            AddChoice("Qualité graphique", QualitySettings.names,
                () => GameSettings.QualityLevel,
                i => GameSettings.QualityLevel = i);

            AddToggle("Synchronisation verticale", () => GameSettings.VSync, v => GameSettings.VSync = v);

            var limits = GameSettings.FrameRateLimits;
            AddChoice("Limite d'images/s", limits.Select(l => l > 0 ? l.ToString() : "Illimitée").ToArray(),
                () => Mathf.Max(0, Array.IndexOf(limits, GameSettings.FrameRateLimit)),
                i => GameSettings.FrameRateLimit = limits[i]);

            AddRange("Champ de vision", 60f, 110f, 5f, () => GameSettings.FieldOfView, v => GameSettings.FieldOfView = v, v => $"{v:0}°");

            AddHeader("Audio");
            AddRange("Volume général", 0f, 1f, 0.05f, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
            AddRange("Musique", 0f, 1f, 0.05f, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);
            AddRange("Effets sonores", 0f, 1f, 0.05f, () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v, Percent);

            AddHeader("Contrôles");
            AddRange("Sensibilité souris", 0.1f, 3f, 0.1f, () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => $"x{v:0.0}");
            AddRange("Sensibilité manette", 0.1f, 3f, 0.1f, () => GameSettings.StickSensitivity, v => GameSettings.StickSensitivity = v, v => $"x{v:0.0}");
            AddToggle("Inverser l'axe vertical", () => GameSettings.InvertY, v => GameSettings.InvertY = v);

            AddHeader("Interface");
            AddToggle("Chiffres de dégâts", () => GameSettings.ShowDamageNumbers, v => GameSettings.ShowDamageNumbers = v);
            AddToggle("Marqueurs de touche", () => GameSettings.ShowHitMarkers, v => GameSettings.ShowHitMarkers = v);
            AddRange("Tremblements de caméra", 0f, 1f, 0.1f, () => GameSettings.CameraShake, v => GameSettings.CameraShake = v, Percent);
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        private void AddHeader(string text)
        {
            var header = Instantiate(headerTemplate, content);
            header.text = text;
            header.gameObject.SetActive(true);
        }

        private void AddChoice(string label, string[] choices, Func<int> get, Action<int> set)
        {
            AddRow(label, () => choices.Length > 0 ? choices[Mathf.Clamp(get(), 0, choices.Length - 1)] : "-", direction =>
            {
                if (choices.Length == 0)
                    return;
                int next = (get() + direction + choices.Length) % choices.Length; // wraps around
                set(next);
            });
        }

        private void AddRange(string label, float min, float max, float step, Func<float> get, Action<float> set, Func<float, string> format)
        {
            AddRow(label, () => format(get()), direction =>
            {
                float value = Mathf.Clamp(Mathf.Round((get() + direction * step) / step) * step, min, max);
                set(value);
            });
        }

        private void AddToggle(string label, Func<bool> get, Action<bool> set)
            => AddRow(label, () => get() ? "Oui" : "Non", _ => set(!get()));

        private void AddRow(string label, Func<string> display, Action<int> step)
        {
            var row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.Setup(label, display, direction =>
            {
                step(direction);
                GameSettings.Apply();
            });
            rows.Add(row);
        }

        private void ResetToDefaults()
        {
            GameSettings.ResetToDefaults();
            RefreshAll();
        }

        private void RefreshAll()
        {
            foreach (var row in rows)
                row.Refresh();
        }
    }
}
