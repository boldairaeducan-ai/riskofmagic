using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Interaction;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Skills;
using UnityEngine;

namespace RiskOfMagic.UI
{
    /// <summary>Prototype IMGUI HUD: health, gold, skills, prompt, run timer. Replace with real UI later.</summary>
    public class DebugHud : MonoBehaviour
    {
        private static readonly string[] SlotKeys = { "LMB", "RMB", "Shift", "R" };

        private GUIStyle centered;
        private GUIStyle box;

        private void OnGUI()
        {
            var player = PlayerAvatar.Local;
            if (player == null)
                return;

            centered ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 12, wordWrap = true };

            DrawCrosshair(player);
            DrawVitals(player);
            DrawSkills(player);
            DrawRunInfo();
        }

        private void DrawCrosshair(PlayerAvatar player)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GUI.Label(new Rect(cx - 10, cy - 12, 20, 24), "+", centered);

            var skills = player.Skills;
            if (skills != null && skills.IsCharging)
            {
                GUI.Box(new Rect(cx - 50, cy + 20, 100, 8), GUIContent.none);
                GUI.Box(new Rect(cx - 50, cy + 20, 100 * skills.ChargeProgress, 8), GUIContent.none);
            }

            if (player.TryGetComponent(out PlayerInteractor interactor))
            {
                string prompt = interactor.FocusPrompt;
                if (!string.IsNullOrEmpty(prompt))
                    GUI.Label(new Rect(cx - 200, cy + 40, 400, 30), prompt, centered);
            }

            if (!player.IsAlive)
                GUI.Label(new Rect(cx - 200, cy - 80, 400, 40), "MORT", centered);
        }

        private void DrawVitals(PlayerAvatar player)
        {
            string character = player.Character != null ? player.Character.displayName : "?";
            GUI.Label(new Rect(20, Screen.height - 90, 400, 24), character);
            GUI.Label(new Rect(20, Screen.height - 66, 400, 24), $"PV {player.Health.Current:0} / {player.Health.Max:0}");
            GUI.Label(new Rect(20, Screen.height - 42, 400, 24), $"Or {player.Wallet.Gold}");
        }

        private void DrawSkills(PlayerAvatar player)
        {
            var skills = player.Skills;
            if (skills == null)
                return;

            const float size = 90f;
            float x0 = Screen.width * 0.5f - size * 2f - 15f;
            float y = Screen.height - size - 20f;
            for (int i = 0; i < SkillController.SlotCount; i++)
            {
                var state = skills.GetSlot((SkillSlot)i);
                if (state == null)
                    continue;

                float multiplier = player.Stats.CooldownMultiplier;
                float remaining = state.GetCooldownRemaining(multiplier);
                string status = state.HasStock ? (state.MaxStock > 1 ? $"x{state.Stock}" : "PRÊT") : $"{remaining:0.0}s";
                if (state.MaxStock > 1 && !state.HasStock)
                    status = $"x0 {remaining:0.0}s";
                GUI.Box(new Rect(x0 + i * (size + 10f), y, size, size), $"{SlotKeys[i]}\n{state.Definition.displayName}\n{status}", box);
            }
        }

        private void DrawRunInfo()
        {
            var run = RunDifficulty.Instance;
            if (run == null)
                return;
            int seconds = Mathf.FloorToInt(run.ElapsedSeconds);
            string text = $"{seconds / 60:00}:{seconds % 60:00}  |  {run.Difficulty}  |  coef {run.Coefficient:0.00}  |  niv. ennemis {run.EnemyLevel}  |  ennemis {EnemyActor.Active.Count}";
            GUI.Label(new Rect(Screen.width - 620, 10, 610, 24), text);
        }
    }
}
