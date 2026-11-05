using RiskOfMagic.Player;
using RiskOfMagic.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// In-game pause menu (Esc / Start). Solo (host alone): the game really pauses (timeScale 0, run clock frozen).
    /// Online: overlay only, the game goes on. Abandon (host) ends the run as a defeat (summary screen).
    /// </summary>
    public class PauseMenu : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private GameObject mainPanel;
        [SerializeField] private TMP_Text subtitleLabel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button optionsButton;
        [SerializeField] private Button abandonButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Button quitButton;
        [SerializeField, Tooltip("Debug builds only, host.")] private Button forceWaveButton;
        [SerializeField] private OptionsMenu options;

        [Header("Confirmation")]
        [SerializeField] private GameObject confirmPanel;
        [SerializeField] private TMP_Text confirmLabel;
        [SerializeField] private Button confirmYesButton;
        [SerializeField] private Button confirmNoButton;

        private System.Action pendingConfirm;
        private bool pausedTime;

        public static PauseMenu Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        private bool open;

        private static bool IsSolo
        {
            get
            {
                var manager = NetworkManager.Singleton;
                return manager != null && manager.IsServer && manager.ConnectedClientsIds.Count <= 1;
            }
        }

        private static bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        private void Awake()
        {
            Listen(resumeButton, Close);
            Listen(optionsButton, OpenOptions);
            Listen(abandonButton, () => Confirm("Abandonner la partie ?\nLa run se termine en défaite pour tous les joueurs.", Abandon));
            Listen(menuButton, () => Confirm(IsHost
                ? "Retourner au menu principal ?\nLa partie sera fermée pour tous les joueurs."
                : "Quitter la partie et retourner au menu principal ?", ReturnToMenu));
            Listen(quitButton, () => Confirm("Quitter le jeu ?", QuitGame));
            Listen(forceWaveButton, ForceWave);
            Listen(confirmYesButton, () =>
            {
                var action = pendingConfirm;
                CloseConfirm();
                action?.Invoke();
            });
            Listen(confirmNoButton, CloseConfirm);
            SetVisible(false);
        }

        private void OnEnable()
        {
            Instance = this;
            PlayerLook.EscapeHandledExternally = true;
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

        protected override void OnUnbind()
        {
            open = false;
            SetVisible(false);
            ResumeTime();
        }

        protected override void Tick()
        {
            var stages = StageManager.Instance;
            bool gameOver = stages != null && stages.IsGameOver;
            if (gameOver && open)
            {
                CloseWithoutCursor(); // the summary screen takes over
                return;
            }

            if (!Player.Input.ToggleCursorPressed || gameOver)
                return;
            if (options != null && options.gameObject.activeSelf)
                options.Close();
            else if (confirmPanel != null && confirmPanel.activeSelf)
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
            if (abandonButton != null)
                abandonButton.gameObject.SetActive(host);
            if (forceWaveButton != null)
                forceWaveButton.gameObject.SetActive(host && Debug.isDebugBuild);
            if (subtitleLabel != null)
                subtitleLabel.text = IsSolo ? "Jeu en pause" : "La partie continue";
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
            if (options != null && options.gameObject.activeSelf)
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
            if (mainPanel != null)
                mainPanel.SetActive(false);
            options.Open(ShowMain);
        }

        private void ShowMain()
        {
            if (mainPanel != null)
                mainPanel.SetActive(open);
        }

        private void Confirm(string message, System.Action action)
        {
            pendingConfirm = action;
            if (confirmLabel != null)
                confirmLabel.text = message;
            if (mainPanel != null)
                mainPanel.SetActive(false);
            if (confirmPanel != null)
                confirmPanel.SetActive(true);
            else
                action?.Invoke();
        }

        private void CloseConfirm()
        {
            pendingConfirm = null;
            if (confirmPanel != null)
                confirmPanel.SetActive(false);
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
            if (group == null)
                return;
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }
    }
}
