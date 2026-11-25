using System;
using System.Collections.Generic;
using System.Linq;
using RiskOfMagic.Player;
using RiskOfMagic.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Options screen (Options.uxml), shared by the main menu and the pause menu. One tab per section; rows are cloned
    /// from the Templates/Option*.uxml templates. Every change is applied immediately (GameSettings.Apply) and saved
    /// on close. Hovering / focusing a row shows its description. Tab / LB-RB switch tabs.
    /// To add an option: one AddChoice / AddSlider / AddToggle line in Build().
    /// </summary>
    public sealed class OptionsScreen
    {
        private const string FocusedRowClass = "option-row--focused";
        private const string ActiveTabClass = "tab--active";

        private readonly VisualElement root;
        private readonly MenuUiAssets assets;
        private readonly VisualElement tabBar;
        private readonly ScrollView pages;
        private readonly Label hint;
        private readonly Button resetButton;
        private readonly string resetText;

        private readonly List<Action> refreshers = new();
        private readonly List<(Button tab, VisualElement page)> tabs = new();
        private VisualElement currentPage;
        private Action onClosed;
        private bool built;
        private int tabIndex;
        private float resetArmedUntil;
        private const float ResetConfirmWindow = 3f;

        public bool IsOpen => MenuUi.IsShown(root);

        /// <param name="root">The element to show / hide (the Options.uxml instance).</param>
        public OptionsScreen(VisualElement root, MenuUiAssets assets)
        {
            this.root = root;
            this.assets = assets;
            tabBar = MenuUi.Require<VisualElement>(root, "tabs");
            pages = MenuUi.Require<ScrollView>(root, "pages");
            hint = MenuUi.Require<Label>(root, "hint");
            resetButton = MenuUi.Require<Button>(root, "resetButton");
            var backButton = MenuUi.Require<Button>(root, "backButton");
            if (resetButton != null)
            {
                resetText = resetButton.text;
                resetButton.clicked += OnReset;
            }
            if (backButton != null)
                backButton.clicked += Close;
            // Keep the focused row in view (keyboard / gamepad).
            pages?.RegisterCallback<FocusInEvent>(e =>
            {
                if (e.target is VisualElement element && element != pages && pages.contentContainer.Contains(element))
                    pages.ScrollTo(element);
            });
            MenuSounds.Register(root);
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            if (!built)
                Build();
            GameInput.BindingsChanged -= RefreshAll;
            GameInput.BindingsChanged += RefreshAll;
            RefreshAll();
            MenuUi.Show(root);
            ShowTab(tabIndex);
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            if (GameInput.IsRebinding)
                GameInput.CancelRebind();
            GameInput.BindingsChanged -= RefreshAll;
            GameSettings.Save();
            DisarmReset();
            MenuUi.Hide(root);
            var closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        /// <summary>Called every frame by the owner. <paramref name="handleBack"/>: Esc / B closes the screen.</summary>
        public void Tick(bool handleBack)
        {
            if (!IsOpen)
                return;
            if (resetArmedUntil > 0f && Time.unscaledTime > resetArmedUntil)
                DisarmReset();
            if (GameInput.MenuKeysBlocked)
                return; // the key belongs to the rebind (Escape cancels it)
            if (handleBack && MenuUi.BackPressed())
            {
                Close();
                return;
            }
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
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

        public void Dispose() => GameInput.BindingsChanged -= RefreshAll;

        // ---------------------------------------------------------------- Content

        private void Build()
        {
            built = true;

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

            AddTab("Raccourcis");
            foreach (var binding in GameInput.Rebindables)
                AddKey(binding);

            AddTab("Interface");
            AddToggle("Chiffres de dégâts", "Affiche les dégâts infligés au-dessus des ennemis.",
                () => GameSettings.ShowDamageNumbers, v => GameSettings.ShowDamageNumbers = v);
            AddToggle("Marqueurs de touche", "Croix de visée qui confirme chaque coup porté.",
                () => GameSettings.ShowHitMarkers, v => GameSettings.ShowHitMarkers = v);
            AddSlider("Tremblements de caméra", "Intensité des secousses (explosions, coups reçus).", 0f, 1f, 0.1f,
                () => GameSettings.CameraShake, v => GameSettings.CameraShake = v, Percent);

            AddTab("Debug");
            AddToggle("Afficher les FPS", "Images par seconde et durée d'une image, en haut à droite de l'écran.",
                () => GameSettings.ShowFps, v => GameSettings.ShowFps = v);
            AddToggle("Tout débloquer (test)", "Classes, compétences alternatives, objets et mode Difficile disponibles. " +
                                               "Les défis continuent de progresser. Pris en compte au prochain passage par le salon.",
                () => RiskOfMagic.Meta.MetaProgression.DebugUnlockAll, v => RiskOfMagic.Meta.MetaProgression.DebugUnlockAll = v);
        }

        private static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        // ---------------------------------------------------------------- Tabs

        private void AddTab(string title)
        {
            var tab = new Button { text = title, name = title };
            tab.AddToClassList("tab");
            tabBar?.Add(tab);
            MenuSounds.Wire(tab);
            var page = new VisualElement { name = title };
            page.AddToClassList("option-page");
            pages?.Add(page);
            int index = tabs.Count;
            tab.clicked += () => ShowTab(index);
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
                tabs[i].tab.EnableInClassList(ActiveTabClass, i == tabIndex);
                MenuUi.SetShown(tabs[i].page, i == tabIndex);
            }
            if (pages != null)
                pages.scrollOffset = Vector2.zero;
            MenuUi.SetText(hint, string.Empty);
            MenuUi.FocusFirst(tabs[tabIndex].page);
        }

        // ---------------------------------------------------------------- Rows

        private VisualElement AddRow(VisualTreeAsset template, string label, string description)
        {
            var row = MenuUi.Clone(template, currentPage);
            if (row == null)
                return null;
            MenuUi.SetText(row.Q<Label>("label"), label);
            row.RegisterCallback<PointerEnterEvent>(_ => MenuUi.SetText(hint, description));
            row.RegisterCallback<FocusInEvent>(_ =>
            {
                row.AddToClassList(FocusedRowClass);
                MenuUi.SetText(hint, description);
            });
            row.RegisterCallback<FocusOutEvent>(_ => row.RemoveFromClassList(FocusedRowClass));
            return row;
        }

        private void AddChoice(string label, string description, string[] choices, Func<int> get, Action<int> set)
        {
            var row = AddRow(assets.optionChoice, label, description);
            if (row == null)
                return;
            var value = row.Q<Label>("value");
            void Refresh() => MenuUi.SetText(value, choices.Length > 0 ? choices[Mathf.Clamp(get(), 0, choices.Length - 1)] : "-");
            void Step(int direction)
            {
                if (choices.Length == 0)
                    return;
                set((get() + direction + choices.Length) % choices.Length); // wraps around
                GameSettings.Apply();
                Refresh();
            }
            var previous = row.Q<Button>("previous");
            var next = row.Q<Button>("next");
            if (previous != null)
                previous.clicked += () => Step(-1);
            if (next != null)
                next.clicked += () => Step(1);
            refreshers.Add(Refresh);
        }

        private void AddSlider(string label, string description, float min, float max, float step, Func<float> get, Action<float> set,
            Func<float, string> format)
        {
            var row = AddRow(assets.optionSlider, label, description);
            var slider = row?.Q<Slider>("slider");
            if (slider == null)
                return;
            var value = row.Q<Label>("value");
            slider.lowValue = min;
            slider.highValue = max;
            slider.pageSize = step;
            slider.RegisterValueChangedCallback(evt =>
            {
                float snapped = step > 0f ? Mathf.Round(evt.newValue / step) * step : evt.newValue;
                // Keyboard / gamepad nudges smaller than a step would round back: move one full step instead.
                if (step > 0f && Mathf.Approximately(snapped, evt.previousValue) && !Mathf.Approximately(evt.newValue, evt.previousValue))
                    snapped = evt.previousValue + Mathf.Sign(evt.newValue - evt.previousValue) * step;
                snapped = Mathf.Clamp(snapped, min, max);
                if (!Mathf.Approximately(snapped, evt.newValue))
                    slider.SetValueWithoutNotify(snapped);
                set(snapped);
                GameSettings.Apply();
                MenuUi.SetText(value, format(snapped));
            });
            refreshers.Add(() =>
            {
                slider.SetValueWithoutNotify(get());
                MenuUi.SetText(value, format(get()));
            });
        }

        private void AddToggle(string label, string description, Func<bool> get, Action<bool> set)
        {
            var row = AddRow(assets.optionToggle, label, description);
            var toggle = row?.Q<Button>("switch");
            if (toggle == null)
                return;
            var state = row.Q<Label>("state");
            void Refresh()
            {
                toggle.EnableInClassList("switch--on", get());
                MenuUi.SetText(state, get() ? "Activé" : "Désactivé");
            }
            toggle.clicked += () =>
            {
                set(!get());
                GameSettings.Apply();
                Refresh();
            };
            refreshers.Add(Refresh);
        }

        private void AddKey(GameInput.Rebindable binding)
        {
            var row = AddRow(assets.optionKey, binding.Label, "Cliquez sur la touche puis appuyez sur la nouvelle (Échap pour annuler). "
                                                              + "Une touche déjà utilisée est échangée avec l'ancienne. Manette non modifiable.");
            var key = row?.Q<Button>("key");
            if (key == null)
                return;
            MenuUi.SetText(row.Q<Label>("category"), binding.Category);
            bool waiting = false;
            void Refresh()
            {
                if (waiting)
                    return;
                key.text = binding.Display;
                key.RemoveFromClassList("key-button--listening");
            }
            key.clicked += () =>
            {
                if (GameInput.IsRebinding)
                    return;
                waiting = true;
                key.text = "Appuyez sur une touche…";
                key.AddToClassList("key-button--listening");
                GameInput.StartRebind(binding, () =>
                {
                    waiting = false;
                    Refresh();
                });
            };
            refreshers.Add(Refresh);
        }

        // ---------------------------------------------------------------- Reset

        /// <summary>Two clicks: the first arms the button ("Confirmer ?"), the second resets.</summary>
        private void OnReset()
        {
            if (resetArmedUntil <= 0f)
            {
                resetArmedUntil = Time.unscaledTime + ResetConfirmWindow;
                MenuUi.SetText(resetButton, "Confirmer la réinitialisation ?");
                return;
            }
            DisarmReset();
            GameSettings.ResetToDefaults();
            GameInput.ResetAll(); // shortcuts too
            RefreshAll();
        }

        private void DisarmReset()
        {
            resetArmedUntil = 0f;
            if (resetText != null)
                MenuUi.SetText(resetButton, resetText);
        }

        private void RefreshAll()
        {
            foreach (var refresh in refreshers)
                refresh();
        }
    }
}
