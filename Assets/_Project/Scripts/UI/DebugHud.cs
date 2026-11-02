using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
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

        [SerializeField, Min(0f)] private float notificationDuration = 3f;
        [SerializeField, Min(0f)] private float pickupLabelDistance = 20f;

        private GUIStyle centered;
        private GUIStyle box;
        private GUIStyle small;
        private string notification;
        private Color notificationColor;
        private float notificationEnd;

        private void OnEnable() => PlayerInventory.LocalItemGained += OnLocalItemGained;
        private void OnDisable() => PlayerInventory.LocalItemGained -= OnLocalItemGained;

        private void OnLocalItemGained(ItemDefinition item, int count)
        {
            if (item == null)
                return;
            notification = $"{item.displayName} (x{count})\n{item.description}";
            notificationColor = ItemDefinition.GetRarityColor(item.rarity);
            notificationEnd = Time.time + notificationDuration;
        }

        private void OnGUI()
        {
            var player = PlayerAvatar.Local;
            if (player == null)
                return;

            centered ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16 };
            box ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 12, wordWrap = true };
            small ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };

            DrawCrosshair(player);
            DrawVitals(player);
            DrawSkills(player);
            DrawRunInfo();
            DrawInventory(player);
            DrawPickupLabels(player);
            DrawNotification();
        }

        private void DrawInventory(PlayerAvatar player)
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return;

            var previous = GUI.color;
            float y = Screen.height - 30f;
            for (int i = player.Inventory.SlotCount - 1; i >= 0; i--)
            {
                int count = player.Inventory.GetCountAt(i);
                var item = count > 0 ? catalog.GetItem(i) : null;
                if (item == null)
                    continue;
                GUI.color = ItemDefinition.GetRarityColor(item.rarity);
                GUI.Label(new Rect(Screen.width - 260f, y, 250f, 22f), $"{item.displayName}  x{count}");
                y -= 20f;
            }
            GUI.color = previous;
        }

        private void DrawPickupLabels(PlayerAvatar player)
        {
            var cam = player.Look.Camera;
            if (cam == null)
                return;

            var previous = GUI.color;
            foreach (var pickup in ItemPickup.Active)
            {
                var item = pickup.Item;
                if (item == null)
                    continue;
                Vector3 screen = cam.WorldToScreenPoint(pickup.VisualPosition + Vector3.up * 0.8f);
                if (screen.z <= 0f || screen.z > pickupLabelDistance)
                    continue;
                GUI.color = ItemDefinition.GetRarityColor(item.rarity);
                GUI.Label(new Rect(screen.x - 100f, Screen.height - screen.y - 12f, 200f, 24f), item.displayName, small);
            }
            GUI.color = previous;
        }

        private void DrawNotification()
        {
            if (string.IsNullOrEmpty(notification) || Time.time > notificationEnd)
                return;
            var previous = GUI.color;
            GUI.color = notificationColor;
            GUI.Label(new Rect(Screen.width * 0.5f - 250f, 60f, 500f, 50f), notification, centered);
            GUI.color = previous;
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
