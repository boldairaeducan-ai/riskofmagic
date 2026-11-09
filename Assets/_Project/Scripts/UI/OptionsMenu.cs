using System;
using System.Collections.Generic;
using System.Linq;
using RiskOfMagic.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Options screen built at runtime: one tab per section, rows cloned from templates (choice ◀ ▶, slider, switch).
    /// Every change is applied immediately (GameSettings.Apply) and saved on close. Hovering / selecting a row shows
    /// its description. Tab / LB-RB switch tabs; Esc / B closes (the pause menu handles Esc itself in game).
    /// </summary>
    public class OptionsMenu : MonoBehaviour
    {
        [SerializeField] private MenuButton tabTemplate;
        [SerializeField] private ScrollRect scroll;
        [SerializeField, Tooltip("Cloned per tab (vertical layout, auto height).")] private RectTransform pageTemplate;
        [SerializeField] private OptionRow choiceTemplate;
        [SerializeField] private OptionSliderRow sliderTemplate;
        [SerializeField] private OptionToggleRow toggleTemplate;
        [SerializeField] private TMP_Text hintLabel;
        [SerializeField] private Button resetButton;
        [SerializeField] private TMP_Text resetLabel;
        [SerializeField] private Button backButton;
        [SerializeField, Min(0.5f)] private float resetConfirmWindow = 3f;

        private readonly List<Action> refreshers = new();
        private readonly List<OptionHint> hints = new();
        private readonly List<(MenuButton tab, RectTransform page)> tabs = new();
        private RectTransform currentPage;
        private Action onClosed;
        private bool built;
        private int tabIndex;
        private float resetArmedUntil;
        private string resetText;

        private void Awake()
        {
            if (resetButton != null)
                resetButton.onClick.AddListener(OnReset);
            if (backButton != null)
                backButton.onClick.AddListener(Close);
            if (resetLabel != null)
                resetText = resetLabel.text;
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            gameObject.SetActive(true);
            if (!built)
                Build();
            RefreshAll();
            ShowTab(tabIndex);
        }

        public void Close()
        {
            GameSettings.Save();
            DisarmReset();
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        private void Update()
        {
            UpdateFocus();
            if (resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil)
                DisarmReset();

            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            if (PauseMenu.Instance == null && ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame)))
            {
                Close();
                return;
            }
            int direction = 0;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
                direction = keyboard.shiftKey.isPressed ? -1 : 1;
            if (pad != null && pad.rightShoulder.wasPressedThisFrame)
                direction = 1;
            if (pad != null && pad.leftShoulder.wasPressedThisFrame)
                direction = -1;
            if (direction != 0 && tabs.Count > 0)
                ShowTab((tabIndex + direction + tabs.Count) % tabs.Count);
        }

        /// <summary>Row under the pointer, else the row of the selected control: highlight + description.</summary>
        private void UpdateFocus()
        {
            OptionHint focused = hints.FirstOrDefault(h => h.Hovered && h.isActiveAndEnabled);
            if (focused == null && EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                focused = EventSystem.current.currentSelectedGameObject.GetComponentInParent<OptionHint>();
            foreach (var hint in hints)
                hint.SetHighlighted(hint == focused);
            if (hintLabel != null)
                hintLabel.text = focused != null ? focused.Description : string.Empty;
        }

        private void Build()
        {
            built = true;
            tabTemplate.gameObject.SetActive(false);
            pageTemplate.gameObject.SetActive(false);
            choiceTemplate.gameObject.SetActive(false);
            sliderTemplate.gameObject.SetActive(false);
            toggleTemplate.gameObject.SetActive(false);

            AddTab("Affichage");
            var resolutions = Screen.resolutions
                .Select(r => new Vector2Int(r.width, r.height))
                .Distinct()
                .OrderBy(r => r.x).ThenBy(r => r.y)
                .ToList();
            if (resolutions.Count == 0)
                resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            AddChoice("Résolution", "Taille de l'image affichée.", resolutions.Select(r => $"{r.x} × {r.y}").ToArray(),
                () => Mathf.Max(0, resolutions.FindIndex(r => r.x == GameSettings.ResolutionWidth && r.y == GameSettings.ResolutionHeight)),
                i =>
                {
                    GameSettings.ResolutionWidth = resolutions[i].x;
                    GameSettings.ResolutionHeight = resolutions[i].y;
                });

            var modes = new[] { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
            AddChoice("Mode d'affichage", "Le plein écran fenêtré permet de changer de fenêtre instantanément.",
                new[] { "Plein écran", "Plein écran fenêtré", "Fenêtré" },
                () => Mathf.Max(0, Array.IndexOf(modes, GameSettings.DisplayMode)),
                i => GameSettings.DisplayMode = modes[i]);
            AddChoice("Qualité graphique", "Ombres, textures et effets. Baissez-la si le jeu manque de fluidité.", QualitySettings.names,
                () => GameSettings.QualityLevel,
                i => GameSettings.QualityLevel = i);
            AddToggle("Synchronisation verticale", "Supprime les déchirures d'image, au prix d'un léger délai.",
                () => GameSettings.VSync, v => GameSettings.VSync = v);
            var limits = GameSettings.FrameRateLimits;
            AddChoice("Limite d'images/s", "Ignorée quand la synchronisation verticale est active.",
                limits.Select(l => l > 0 ? l.ToString() : "Illimitée").ToArray(),
                () => Mathf.Max(0, Array.IndexOf(limits, GameSettings.FrameRateLimit)),
                i => GameSettings.FrameRateLimit = limits[i]);
            AddSlider("Champ de vision", "Angle de vue horizontal de la caméra.", 60f, 110f, 1f,
                () => GameSettings.FieldOfView, v => GameSettings.FieldOfView = v, v => $"{v:0}°");

            AddTab("Audio");
            AddSlider("Volume général", "Volume de tous les sons du jeu.", 0f, 1f, 0.05f, () => GameSettings.MasterVolume, v => GameSettings.MasterVolume = v, Percent);
            AddSlider("Musique", "Musiques des menus et des cartes.", 0f, 1f, 0.05f, () => GameSettings.MusicVolume, v => GameSettings.MusicVolume = v, Percent);
            AddSlider("Effets sonores", "Sorts, impacts, monstres et interface.", 0f, 1f, 0.05f, () => GameSettings.EffectsVolume, v => GameSettings.EffectsVolume = v, Percent);

            AddTab("Contrôles");
            AddSlider("Sensibilité souris", "Vitesse de rotation de la caméra à la souris.", 0.1f, 3f, 0.05f,
                () => GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => $"×{v:0.00}");
            AddSlider("Sensibilité manette", "Vitesse de rotation de la caméra au stick droit.", 0.1f, 3f, 0.05f,
                () => GameSettings.StickSensitivity, v => GameSettings.StickSensitivity = v, v => $"×{v:0.00}");
            AddToggle("Inverser l'axe vertical", "Pousser vers le haut fait regarder vers le bas.",
                () => GameSettings.InvertY, v => GameSettings.InvertY = v);

            AddTab("Interface");
            AddToggle("Chiffres de dégâts", "Affiche les dégâts infligés au-dessus des ennemis.",
                () => GameSettings.ShowDamageNumbers, v => GameSettings.ShowDamageNumbers = v);
            AddToggle("Marqueurs de touche", "Croix de visée qui confirme chaque coup porté.",
                () => GameSettings.ShowHitMarkers, v => GameSettings.ShowHitMarkers = v);
            AddSlider("Tremblements de caméra", "Intensité des secousses (explosions, coups reçus).", 0f, 1f, 0.1f,
                () => GameSettings.CameraShake, v => GameSettings.CameraShake = v, Percent);
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        private void AddTab(string title)
        {
            var tab = Instantiate(tabTemplate, tabTemplate.transform.parent);
            tab.gameObject.SetActive(true);
            tab.name = title;
            if (tab.Text != null)
                tab.Text.text = title;
            var page = Instantiate(pageTemplate, pageTemplate.parent);
            page.name = title;
            int index = tabs.Count;
            tab.Button.onClick.AddListener(() => ShowTab(index));
            tabs.Add((tab, page));
            currentPage = page;
        }

        private void ShowTab(int index)
        {
            if (tabs.Count == 0)
                return;
            tabIndex = Mathf.Clamp(index, 0, tabs.Count - 1);
            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].tab.Pinned = i == tabIndex;
                tabs[i].page.gameObject.SetActive(i == tabIndex);
            }
            var page = tabs[tabIndex].page;
            if (scroll != null)
            {
                scroll.content = page;
                scroll.verticalNormalizedPosition = 1f;
            }
            var first = page.GetComponentInChildren<Selectable>();
            if (EventSystem.current != null && first != null)
                EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private T AddRow<T>(T template, string description) where T : Component
        {
            var row = Instantiate(template, currentPage);
            row.gameObject.SetActive(true);
            if (row.TryGetComponent(out OptionHint hint))
            {
                hint.Description = description;
                hints.Add(hint);
            }
            return row;
        }

        private void AddChoice(string label, string description, string[] choices, Func<int> get, Action<int> set)
        {
            var row = AddRow(choiceTemplate, description);
            row.Setup(label, () => choices.Length > 0 ? choices[Mathf.Clamp(get(), 0, choices.Length - 1)] : "-", direction =>
            {
                if (choices.Length == 0)
                    return;
                set((get() + direction + choices.Length) % choices.Length); // wraps around
                GameSettings.Apply();
            });
            refreshers.Add(row.Refresh);
        }

        private void AddSlider(string label, string description, float min, float max, float step, Func<float> get, Action<float> set, Func<float, string> format)
        {
            var row = AddRow(sliderTemplate, description);
            row.Setup(label, min, max, step, get, v =>
            {
                set(v);
                GameSettings.Apply();
            }, format);
            refreshers.Add(row.Refresh);
        }

        private void AddToggle(string label, string description, Func<bool> get, Action<bool> set)
        {
            var row = AddRow(toggleTemplate, description);
            row.Setup(label, get, v =>
            {
                set(v);
                GameSettings.Apply();
            });
            refreshers.Add(row.Refresh);
        }

        /// <summary>Two clicks: the first arms the button ("Confirmer ?"), the second resets.</summary>
        private void OnReset()
        {
            if (resetArmedUntil <= 0f)
            {
                resetArmedUntil = Time.unscaledTime + resetConfirmWindow;
                if (resetLabel != null)
                    resetLabel.text = "Confirmer la réinitialisation ?";
                return;
            }
            DisarmReset();
            GameSettings.ResetToDefaults();
            RefreshAll();
        }

        private void DisarmReset()
        {
            resetArmedUntil = 0f;
            if (resetLabel != null && resetText != null)
                resetLabel.text = resetText;
        }

        private void RefreshAll()
        {
            foreach (var refresh in refreshers)
                refresh();
        }
    }
}
