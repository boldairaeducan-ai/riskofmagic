using RiskOfMagic.Run;
using TMPro;
using UnityEngine;

namespace RiskOfMagic.UI
{
    public class RunInfoWidget : HudWidget
    {
        [SerializeField] private TMP_Text timerLabel;
        [SerializeField] private TMP_Text detailsLabel;

        private int lastSeconds = -1;
        private int lastLevel = -1;
        private int lastStage = -1;

        protected override void Tick()
        {
            var run = RunDifficulty.Instance;
            if (run == null)
                return;

            int seconds = Mathf.FloorToInt(run.ElapsedSeconds);
            if (seconds != lastSeconds && timerLabel != null)
                timerLabel.text = $"{seconds / 60:00}:{seconds % 60:00}";
            lastSeconds = seconds;

            int level = run.EnemyLevel;
            int stage = StageManager.Instance != null ? StageManager.Instance.StageNumber : 1;
            if ((level != lastLevel || stage != lastStage) && detailsLabel != null)
                detailsLabel.text = $"Étape {stage}  ·  {DifficultyName(run.Difficulty)}  ·  Niveau ennemis {level}";
            lastLevel = level;
            lastStage = stage;
        }

        private static string DifficultyName(DifficultyLevel difficulty) => difficulty switch
        {
            DifficultyLevel.Easy => "Facile",
            DifficultyLevel.Hard => "Difficile",
            _ => "Normal"
        };
    }
}
