using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Meta;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// End-of-run screen (victory or defeat): run stats, one row per player (stats + final items).
    /// Only the host can restart (same players); everyone can go back to the menu.
    /// </summary>
    public class GameOverWidget : HudWidget
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text statsLabel;
        [SerializeField, Tooltip("Disabled template, cloned per player.")] private RectTransform playerRowTemplate;
        [SerializeField, Tooltip("Disabled template inside the row's items container, cloned per item.")] private Image itemIconTemplate;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;
        [SerializeField, Min(0.01f)] private float fadeSpeed = 2f;
        [SerializeField, Min(1)] private int maxItemIcons = 24;

        private readonly List<GameObject> rows = new();
        private bool shown;

        private void Awake()
        {
            if (restartButton != null)
                restartButton.onClick.AddListener(Restart);
            if (quitButton != null)
                quitButton.onClick.AddListener(Quit);
            if (playerRowTemplate != null)
                playerRowTemplate.gameObject.SetActive(false);
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
                    Refresh(stages);
            }

            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, over ? 1f : 0f, fadeSpeed * Time.deltaTime);
        }

        private void Refresh(StageManager stages)
        {
            if (restartButton != null)
                restartButton.gameObject.SetActive(NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);

            if (titleLabel != null)
            {
                titleLabel.text = stages.IsVictory ? "VICTOIRE" : "DÉFAITE";
                if (theme != null)
                    titleLabel.color = theme.GetColor(stages.IsVictory ? HudColor.Gold : HudColor.Danger);
            }

            if (statsLabel != null)
            {
                int seconds = Mathf.FloorToInt(stages.FinalTime);
                string loops = stages.Loop > 0 ? $"   ·   Boucles : {stages.Loop}" : string.Empty;
                statsLabel.text = $"Temps : {seconds / 60:00}:{seconds % 60:00}   ·   Étape : {stages.StageNumber}{loops}\n" +
                                  $"Difficulté : {RunDifficulty.CurrentCoefficient:0.00}   ·   Ennemis tués : {stages.Kills}";
                var completed = MetaProgression.CompletedThisRun;
                if (completed.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var challenge in completed)
                        names.Add(challenge.displayName);
                    statsLabel.text += $"\n<size=80%><color=#E8C060>Défis accomplis : {string.Join(", ", names)}</color></size>";
                }
            }

            BuildRows();
        }

        private void BuildRows()
        {
            foreach (var row in rows)
                if (row != null)
                    Destroy(row);
            rows.Clear();
            if (playerRowTemplate == null)
                return;

            // One row per client (a stale or duplicated avatar must never show twice).
            var players = new List<PlayerAvatar>();
            foreach (var candidate in PlayerAvatar.All)
                if (candidate != null && candidate.IsSpawned && !players.Exists(p => p.OwnerClientId == candidate.OwnerClientId))
                    players.Add(candidate);
            players.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
            foreach (var player in players)
            {
                var row = Instantiate(playerRowTemplate, playerRowTemplate.parent);
                row.gameObject.SetActive(true);
                rows.Add(row.gameObject);
                FillRow(row, player);
            }
        }

        private void FillRow(RectTransform row, PlayerAvatar player)
        {
            var labels = row.GetComponentsInChildren<TMP_Text>(true);
            string character = player.Character != null ? player.Character.displayName : "?";
            string you = player.IsOwner ? " (vous)" : string.Empty;
            var stats = player.RunStats;
            if (labels.Length > 0)
                labels[0].text = $"Joueur {player.OwnerClientId + 1}{you} — {character}";
            if (labels.Length > 1)
                labels[1].text = stats == null ? string.Empty
                    : $"Dégâts {FormatNumber(stats.DamageDealt)}   ·   Kills {stats.Kills}   ·   Morts {stats.Deaths}   ·   Or {stats.GoldEarned}   ·   Objets {CountItems(player.Inventory)}";

            if (itemIconTemplate == null)
                return;
            // The icon template lives in the row template: find its clone in this row by name.
            var iconTemplate = FindChild(row, itemIconTemplate.name);
            if (iconTemplate == null)
                return;
            iconTemplate.gameObject.SetActive(false);
            var catalog = GameCatalog.Instance;
            if (catalog == null || player.Inventory == null)
                return;

            int shownIcons = 0;
            for (int i = 0; i < catalog.Items.Count && shownIcons < maxItemIcons; i++)
            {
                int stack = player.Inventory.GetCountAt(i);
                var item = catalog.Items[i];
                if (stack <= 0 || item == null)
                    continue;
                var icon = Instantiate(iconTemplate, iconTemplate.parent);
                icon.gameObject.SetActive(true);
                var image = icon.GetComponent<Image>();
                image.sprite = RiskOfMagic.Core.ArtLibrary.ItemIcon(item, theme != null ? theme.GetSprite(HudSprite.DefaultItemIcon) : null);
                image.color = item.icon != null ? Color.white : ItemDefinition.GetRarityColor(item.rarity);
                var count = icon.GetComponentInChildren<TMP_Text>(true);
                if (count != null)
                    count.text = stack > 1 ? $"x{stack}" : string.Empty;
                shownIcons++;
            }
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == childName)
                    return t;
            return null;
        }

        private static int CountItems(PlayerInventory inventory)
        {
            if (inventory == null)
                return 0;
            int total = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
                total += inventory.GetCountAt(i);
            return total;
        }

        private static string FormatNumber(float value) => value >= 10000f ? $"{value / 1000f:0.#}k" : $"{value:0}";

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

        private static void Quit() => RiskOfMagic.Flow.GameFlow.ReturnToMainMenu();
    }
}
