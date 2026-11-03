using RiskOfMagic.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>End-of-run screen. Only the host/server can restart; everyone can leave.</summary>
    public class GameOverWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text statsLabel;
        [SerializeField] private UnityEngine.UI.Button restartButton;
        [SerializeField] private UnityEngine.UI.Button quitButton;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 2f;

        private bool shown;

        private void Awake()
        {
            if (restartButton != null)
                restartButton.onClick.AddListener(Restart);
            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);
            SetInteractive(false);
        }

        protected override void OnUnbind()
        {
            shown = false;
            if (group != null)
                group.alpha = 0f;
            SetInteractive(false);
        }

        protected override void Tick()
        {
            var stages = StageManager.Instance;
            bool over = stages != null && stages.IsGameOver;

            if (over != shown)
            {
                shown = over;
                SetInteractive(over);
                Player.Look.SetCursorLocked(!over);
                if (over)
                    RefreshStats(stages);
            }

            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, over ? 1f : 0f, fadeSpeed * Time.deltaTime);
        }

        private void RefreshStats(StageManager stages)
        {
            if (restartButton != null)
                restartButton.gameObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
            if (statsLabel == null)
                return;
            int seconds = Mathf.FloorToInt(stages.FinalTime);
            statsLabel.text = $"Étape atteinte : {stages.StageNumber}\nTemps survécu : {seconds / 60:00}:{seconds % 60:00}\nEnnemis tués : {stages.Kills}";
        }

        private void SetInteractive(bool interactive)
        {
            if (group == null)
                return;
            group.interactable = interactive;
            group.blocksRaycasts = interactive;
        }

        private static void Restart()
        {
            if (StageManager.Instance != null)
                StageManager.Instance.RestartRun();
        }

        private static void Quit()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();
        }
    }
}
