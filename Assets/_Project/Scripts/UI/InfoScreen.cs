using System.Collections.Generic;
using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// In-game info screen (UI/Menus/InfoScreen.uxml), shown only while the Info key is held (Tab by default,
    /// rebindable). The game keeps running; the cursor is freed so the mouse can hover items instead of aiming.
    /// Shows the local character (stats, run stats, items with a tooltip, equipment) and every player (level, kills,
    /// damage, ping in multiplayer).
    /// </summary>
    public class InfoScreen : HudWidget
    {
        [SerializeField] private UIDocument document;
        [SerializeField, Tooltip("Empty = Resources/MenuUI.")] private MenuUiAssets assets;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private VisualElement boundRoot;
        private VisualElement screen;
        private VisualElement portrait, stats, runStats, itemGrid, equipmentIcon, tooltip, playerList;
        private Label characterName, levelLabel, equipmentLabel, tooltipName, tooltipRarity, tooltipDescription, runLabel;

        private readonly Dictionary<string, Label> statValues = new();
        private readonly List<VisualElement> playerRows = new();
        private bool shown;
        private bool unlockedCursor;
        private bool itemsDirty = true;
        private float nextRefresh;

        protected override void OnBind()
        {
            if (Player.Inventory != null)
                Player.Inventory.ItemsChanged += MarkItemsDirty;
        }

        protected override void OnUnbind()
        {
            if (Player != null && Player.Inventory != null)
                Player.Inventory.ItemsChanged -= MarkItemsDirty;
            SetShown(false);
        }

        private void MarkItemsDirty() => itemsDirty = true;

        protected override void Tick()
        {
            if (!EnsureBound())
                return;
            var stages = StageManager.Instance;
            bool held = GameInput.InfoScreen != null && GameInput.InfoScreen.IsPressed() && !GameInput.MenuKeysBlocked
                        && !PauseMenu.IsOpen && (stages == null || !stages.IsGameOver);
            if (held != shown)
                SetShown(held);
            if (!shown)
                return;
            if (itemsDirty)
                RebuildItems();
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + refreshInterval;
                RefreshCharacter();
                RefreshPlayers();
            }
        }

        private void SetShown(bool value)
        {
            if (value == shown)
                return;
            shown = value;
            MenuUi.SetShown(screen, value);
            if (Player == null)
                return;
            if (value)
            {
                // Free the cursor to hover items (aiming and skills stop while it is unlocked).
                unlockedCursor = Player.Look.IsCursorLocked;
                if (unlockedCursor)
                    Player.Look.SetCursorLocked(false);
                itemsDirty = true;
                nextRefresh = 0f;
                MenuUi.Hide(tooltip);
            }
            else if (unlockedCursor)
            {
                unlockedCursor = false;
                if (!PauseMenu.IsOpen)
                    Player.Look.SetCursorLocked(true);
            }
        }

        // ---------------------------------------------------------------- Binding

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
            screen = MenuUi.Require<VisualElement>(root, "infoScreen");
            portrait = root.Q("portrait");
            characterName = root.Q<Label>("characterName");
            levelLabel = root.Q<Label>("levelLabel");
            stats = MenuUi.Require<VisualElement>(root, "stats");
            runStats = MenuUi.Require<VisualElement>(root, "runStats");
            itemGrid = MenuUi.Require<VisualElement>(root, "itemGrid");
            equipmentIcon = root.Q("equipmentIcon");
            equipmentLabel = root.Q<Label>("equipmentLabel");
            tooltip = root.Q("itemTooltip");
            tooltipName = root.Q<Label>("tooltipName");
            tooltipRarity = root.Q<Label>("tooltipRarity");
            tooltipDescription = root.Q<Label>("tooltipDescription");
            runLabel = root.Q<Label>("runLabel");
            playerList = MenuUi.Require<VisualElement>(root, "playerList");
            statValues.Clear();
            playerRows.Clear();
            stats?.Clear();
            runStats?.Clear();
            playerList?.Clear();
            MenuUi.SetShown(screen, shown);
            return screen != null;
        }

        // ---------------------------------------------------------------- Character

        private void RefreshCharacter()
        {
            var character = Player.Character;
            var s = Player.Stats;
            MenuUi.SetText(characterName, character != null ? character.displayName : string.Empty);
            MenuUi.SetImage(portrait, character != null ? character.portrait : null);
            MenuUi.SetText(levelLabel, Player.Experience != null ? $"Niveau {Player.Experience.Level}" : string.Empty);

            int jumps = (character != null && character.movement != null ? character.movement.maxJumps : 1) + (s != null ? s.ExtraJumps : 0);
            Stat(stats, "Points de vie", $"{Player.Health.Current:0} / {Player.Health.Max:0}");
            if (s != null)
            {
                Stat(stats, "Régénération", $"{s.HealthRegen:0.#} PV/s");
                Stat(stats, "Dégâts", $"{s.BaseDamage * s.DamageMultiplier:0.#}");
                Stat(stats, "Vitesse d'attaque", $"×{s.AttackSpeed:0.00}");
                Stat(stats, "Coup critique", $"{s.CritChance * 100f:0} %");
                Stat(stats, "Vitesse de déplacement", $"{s.MoveSpeedMultiplier * 100f:0} %");
                Stat(stats, "Dégâts subis", $"{s.DamageTakenMultiplier * 100f:0} %{(s.FlatDamageReduction > 0f ? $" (−{s.FlatDamageReduction:0} par coup)" : string.Empty)}");
                Stat(stats, "Rechargement", $"{s.CooldownMultiplier * 100f:0} %");
            }
            Stat(stats, "Sauts", jumps.ToString());
            Stat(stats, "Or", Player.Wallet != null ? Player.Wallet.Gold.ToString("N0") : "0");

            var run = Player.RunStats;
            if (run != null)
            {
                Stat(runStats, "Ennemis tués", run.Kills.ToString("N0"));
                Stat(runStats, "Dégâts infligés", run.DamageDealt.ToString("N0"));
                Stat(runStats, "Mises à terre", run.Deaths.ToString("N0"));
                Stat(runStats, "Or gagné", run.GoldEarned.ToString("N0"));
            }

            var equipment = Player.Equipment != null ? Player.Equipment.Current : null;
            MenuUi.SetImage(equipmentIcon, equipment != null ? equipment.icon : null);
            MenuUi.SetText(equipmentLabel, equipment != null ? $"<b>{equipment.displayName}</b>\n{equipment.description}" : "Aucun");
        }

        /// <summary>One "label … value" row, cloned once from Templates/InfoStat.uxml then updated.</summary>
        private void Stat(VisualElement parent, string label, string value)
        {
            if (parent == null)
                return;
            string key = parent.name + "/" + label;
            if (!statValues.TryGetValue(key, out var valueLabel))
            {
                var row = MenuUi.Clone(assets != null ? assets.infoStat : null, parent);
                if (row == null)
                    return;
                MenuUi.SetText(row.Q<Label>("label"), label);
                valueLabel = row.Q<Label>("value");
                statValues[key] = valueLabel;
            }
            MenuUi.SetText(valueLabel, value);
        }

        // ---------------------------------------------------------------- Items

        private void RebuildItems()
        {
            itemsDirty = false;
            if (itemGrid == null || GameCatalog.Instance == null || Player.Inventory == null)
                return;
            itemGrid.Clear();
            var catalog = GameCatalog.Instance;
            // Rarest first, then by name.
            var owned = new List<(ItemDefinition item, int count)>();
            for (int i = 0; i < catalog.Items.Count; i++)
            {
                int count = Player.Inventory.GetCountAt(i);
                if (count > 0 && catalog.Items[i] != null)
                    owned.Add((catalog.Items[i], count));
            }
            owned.Sort((a, b) => a.item.rarity != b.item.rarity ? b.item.rarity.CompareTo(a.item.rarity) : string.CompareOrdinal(a.item.displayName, b.item.displayName));
            foreach (var (item, count) in owned)
            {
                var slot = MenuUi.Clone(assets != null ? assets.infoItem : null, itemGrid);
                if (slot == null)
                    return;
                MenuUi.SetImage(slot.Q("icon"), ArtLibrary.ItemIcon(item, null));
                MenuUi.SetText(slot.Q<Label>("count"), count > 1 ? $"×{count}" : string.Empty);
                slot.style.borderTopColor = slot.style.borderBottomColor = slot.style.borderLeftColor = slot.style.borderRightColor =
                    ItemDefinition.GetRarityColor(item.rarity); // rarity frame (data color)
                var captured = item;
                int capturedCount = count;
                slot.RegisterCallback<PointerEnterEvent>(_ => ShowTooltip(captured, capturedCount));
                slot.RegisterCallback<PointerLeaveEvent>(_ => MenuUi.Hide(tooltip));
            }
            if (owned.Count == 0)
                itemGrid.Add(new Label("Aucun objet pour l'instant.") { name = "empty" });
        }

        private void ShowTooltip(ItemDefinition item, int count)
        {
            MenuUi.SetText(tooltipName, count > 1 ? $"{item.displayName} ×{count}" : item.displayName);
            if (tooltipName != null)
                tooltipName.style.color = ItemDefinition.GetRarityColor(item.rarity);
            MenuUi.SetText(tooltipRarity, ItemDefinition.GetRarityName(item.rarity));
            MenuUi.SetText(tooltipDescription, item.description);
            MenuUi.Show(tooltip, animate: false);
        }

        // ---------------------------------------------------------------- Players

        private void RefreshPlayers()
        {
            var difficulty = RunDifficulty.Instance;
            var stages = StageManager.Instance;
            var parts = new List<string>();
            if (difficulty != null && difficulty.IsSpawned)
            {
                parts.Add(difficulty.Difficulty == DifficultyLevel.Hard ? "Difficile" : "Normal");
                int seconds = Mathf.FloorToInt(difficulty.ElapsedSeconds);
                parts.Add($"{seconds / 60:00}:{seconds % 60:00}");
                parts.Add($"ennemis niveau {difficulty.EnemyLevel}");
            }
            if (stages != null && stages.IsSpawned)
                parts.Insert(0, $"Étape {stages.StageNumber}");
            MenuUi.SetText(runLabel, string.Join("  ·  ", parts));

            var players = PlayerAvatar.All;
            var manager = NetworkManager.Singleton;
            bool multiplayer = players.Count > 1 || (manager != null && !manager.IsServer);
            while (playerRows.Count < players.Count)
            {
                var row = MenuUi.Clone(assets != null ? assets.infoPlayerRow : null, playerList);
                if (row == null)
                    return;
                playerRows.Add(row);
            }
            for (int i = 0; i < playerRows.Count; i++)
            {
                var row = playerRows[i];
                bool used = i < players.Count && players[i] != null;
                MenuUi.SetShown(row, used);
                if (!used)
                    continue;
                var player = players[i];
                bool me = player == PlayerAvatar.Local;
                string playerName = player.RunStats != null ? player.RunStats.DisplayName : null;
                if (string.IsNullOrEmpty(playerName))
                    playerName = $"Joueur {i + 1}";
                MenuUi.SetText(row.Q<Label>("name"), $"{playerName}{(me ? " (vous)" : string.Empty)}");
                string character = player.Character != null ? player.Character.displayName : "?";
                int level = player.Experience != null ? player.Experience.Level : 1;
                string state = !player.IsAlive ? " · mort" : player.IsDowned ? " · à terre" : string.Empty;
                MenuUi.SetText(row.Q<Label>("details"), $"{character} · niveau {level}{state}");
                var run = player.RunStats;
                MenuUi.SetText(row.Q<Label>("score"), run != null ? $"{run.Kills:N0} kills · {run.DamageDealt:N0} dégâts" : string.Empty);
                SetPing(row, multiplayer && run != null, run != null ? run.PingMs : 0, player.IsOwnedByServer);
            }
        }

        private static readonly string[] PingClasses = { "info-ping--good", "info-ping--ok", "info-ping--bad", "info-ping--terrible" };

        /// <summary>Classic signal bars: 4 under 60 ms, 3 under 120, 2 under 200, else 1.</summary>
        private static void SetPing(VisualElement row, bool visible, int ms, bool host)
        {
            var ping = row.Q("ping");
            MenuUi.SetShown(ping, visible);
            if (!visible || ping == null)
                return;
            int quality = ms < 60 ? 0 : ms < 120 ? 1 : ms < 200 ? 2 : 3;
            int bars = 4 - quality;
            for (int i = 0; i < PingClasses.Length; i++)
                ping.EnableInClassList(PingClasses[i], i == quality);
            for (int b = 1; b <= 4; b++)
                ping.Q($"bar{b}")?.EnableInClassList("info-ping-bar--on", b <= bars);
            MenuUi.SetText(ping.Q<Label>("pingLabel"), host ? "hôte" : $"{ms} ms");
        }
    }
}
