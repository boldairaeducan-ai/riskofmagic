using RiskOfMagic.Player;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// In-game pause menu (Esc / Start), UI Toolkit (UI/Menus/PauseMenu.uxml) on a UIDocument inside the HUD.
    /// Solo (host alone): the game really pauses (timeScale 0, run clock frozen). Online: overlay only, the game goes on.
    /// Abandon (host) ends the run as a defeat (summary screen).
    /// </summary>
    public class PauseMenu : HudWidget
    {
        [SerializeField] private UIDocument document;
        [SerializeField, Tooltip("Empty = Resources/MenuUI.")] private MenuUiAssets assets;

        private VisualElement boundRoot;
        private VisualElement screen;
        private VisualElement mainPanel;
        private VisualElement confirmPanel;
        private Label subtitleLabel;
        private Label confirmLabel;
        private Button resumeButton;
        private Button abandonButton;
        private Button forceWaveButton;
        private OptionsScreen options;

        private System.Action pendingConfirm;
        private bool pausedTime;
        private bool open;

        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        private static bool IsSolo
        {
            get
            {
                var manager = NetworkManager.Singleton;
                return manager != null && manager.IsServer && manager.ConnectedClientsIds.Count <= 1;
            }
        }

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void OnEnable()
        {
            Instance = this;
            PlayerLook.EscapeHandledExternally = true;
            EnsureBound();
        }

        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
                PlayerLook.EscapeHandledExternally = false;
            }
            ResumeTime();
        }

        private void OnDestroy() => options?.Dispose();

        /// <summary>
        /// (Re)binds the named elements of the document. The UIDocument rebuilds its tree when it is re-enabled, so
        /// this runs again whenever its root changes.
        /// </summary>
        private bool EnsureBound()
        {
            if (document == null)
                document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root == null)
                return false;
            if (root == boundRoot)
                return screen != null;
            boundRoot = root;
            if (assets == null)
                assets = MenuUiAssets.Instance;

            screen = MenuUi.Require<VisualElement>(root, "pauseScreen");
            mainPanel = MenuUi.Require<VisualElement>(root, "main");
            confirmPanel = MenuUi.Require<VisualElement>(root, "confirm");
            subtitleLabel = MenuUi.Require<Label>(root, "subtitleLabel");
            confirmLabel = MenuUi.Require<Label>(root, "confirmLabel");
            resumeButton = Listen(root, "resumeButton", Close);
            Listen(root, "optionsButton", OpenOptions);
            abandonButton = Listen(root, "abandonButton", () => Confirm("Abandonner la partie ?\nLa run se termine en défaite pour tous les joueurs.", Abandon));
            Listen(root, "menuButton", () => Confirm(IsHost
                ? "Retourner au menu principal ?\nLa partie sera fermée pour tous les joueurs."
                : "Quitter la partie et retourner au menu principal ?", ReturnToMenu));
            Listen(root, "quitButton", () => Confirm("Quitter le jeu ?", QuitGame));
            forceWaveButton = Listen(root, "forceWaveButton", ForceWave);
            Listen(root, "confirmYesButton", () =>
            {
                var action = pendingConfirm;
                CloseConfirm();
                action?.Invoke();
            });
            Listen(root, "confirmNoButton", CloseConfirm);
            options = assets != null ? new OptionsScreen(MenuUi.Require<VisualElement>(root, "options"), assets) : null;
            MenuSounds.Register(root);
            SetVisible(open);
            return screen != null;
        }

        private static Button Listen(VisualElement root, string name, System.Action action)
        {
            var button = MenuUi.Require<Button>(root, name);
            if (button != null)
                button.clicked += action;
            return button;
        }

        protected override void OnUnbind()
        {
            open = false;
            SetVisible(false);
            ResumeTime();
        }

        protected override void Tick()
        {
            if (!EnsureBound())
                return;
            options?.Tick(handleBack: false); // Esc is handled below

            var stages = StageManager.Instance;
            bool gameOver = stages != null && stages.IsGameOver;
            if (gameOver && open)
            {
                CloseWithoutCursor(); // the summary screen takes over
                return;
            }

            if (!Player.Input.ToggleCursorPressed || gameOver || GameInput.MenuKeysBlocked)
                return;
            if (options != null && options.IsOpen)
                options.Close();
            else if (MenuUi.IsShown(confirmPanel))
                CloseConfirm();
            else if (open)
                Close();
            else if (Player.Look.IsCursorLocked)
                Open(); // other menus (merchant, choice) own the cursor while they're open
        }

        private void Open()
        {
            open = true;
            SetVisible(true);
            ShowMain();
            Player.Look.SetCursorLocked(false);
            bool host = IsHost;
            MenuUi.SetShown(abandonButton, host);
            MenuUi.SetShown(forceWaveButton, host && Debug.isDebugBuild);
            MenuUi.SetText(subtitleLabel, IsSolo ? "Jeu en pause" : "La partie continue");
            if (IsSolo)
                PauseTime();
        }

        public void Close()
        {
            CloseWithoutCursor();
            if (Player != null)
                Player.Look.SetCursorLocked(true);
        }

        private void CloseWithoutCursor()
        {
            open = false;
            if (options != null && options.IsOpen)
                options.Close();
            CloseConfirm();
            SetVisible(false);
            ResumeTime();
        }

        private void PauseTime()
        {
            if (pausedTime)
                return;
            pausedTime = true;
            Time.timeScale = 0f;
            if (RunDifficulty.Instance != null)
                RunDifficulty.Instance.SetPaused(true);
        }

        private void ResumeTime()
        {
            if (!pausedTime)
                return;
            pausedTime = false;
            Time.timeScale = 1f;
            if (RunDifficulty.Instance != null)
                RunDifficulty.Instance.SetPaused(false);
        }

        private void OpenOptions()
        {
            if (options == null)
                return;
            MenuUi.Hide(mainPanel);
            options.Open(ShowMain);
        }

        private void ShowMain()
        {
            MenuUi.SetShown(mainPanel, open, animate: true);
            if (open)
                MenuUi.Focus(resumeButton);
        }

        private void Confirm(string message, System.Action action)
        {
            if (confirmPanel == null)
            {
                action?.Invoke();
                return;
            }
            pendingConfirm = action;
            MenuUi.SetText(confirmLabel, message);
            MenuUi.Hide(mainPanel);
            MenuUi.Show(confirmPanel);
            MenuUi.FocusFirst(confirmPanel);
        }

        private void CloseConfirm()
        {
            bool wasShown = MenuUi.IsShown(confirmPanel);
            pendingConfirm = null;
            MenuUi.Hide(confirmPanel);
            if (wasShown)
                ShowMain();
        }

        private void Abandon()
        {
            CloseWithoutCursor();
            if (StageManager.Instance != null)
                StageManager.Instance.EndRun(won: false);
        }

        private void ReturnToMenu()
        {
            CloseWithoutCursor();
            RiskOfMagic.Flow.GameFlow.ReturnToMainMenu();
        }

        private static void QuitGame()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void ForceWave()
        {
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.ForceWave();
        }

        private void SetVisible(bool visible)
        {
            if (visible)
                MenuUi.Show(screen);
            else
                MenuUi.Hide(screen);
        }
    }
}
