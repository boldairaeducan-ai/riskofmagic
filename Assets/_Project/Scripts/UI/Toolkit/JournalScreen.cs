using System;
using System.Text;
using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace RiskOfMagic.UI
{
    /// <summary>
    /// Main menu journal (Journal.uxml): statistics, challenges, items, bestiary of the local profile, as rich text pages.
    /// To add a page: a tab button in Journal.uxml, an entry in <see cref="Tab"/>, its name in the constructor and a
    /// page builder in Show().
    /// </summary>
    public sealed class JournalScreen
    {
        private enum Tab { Stats, Challenges, Items, Bestiary }

        private const string ActiveTabClass = "tab--active";

        private readonly VisualElement root;
        private readonly Button[] tabButtons;
        private readonly Label content;
        private readonly ScrollView scroll;
        private Action onClosed;
        private Tab tab;

        public bool IsOpen => MenuUi.IsShown(root);

        public JournalScreen(VisualElement root)
        {
            this.root = root;
            tabButtons = new[]
            {
                MenuUi.Require<Button>(root, "statsTab"),
                MenuUi.Require<Button>(root, "challengesTab"),
                MenuUi.Require<Button>(root, "itemsTab"),
                MenuUi.Require<Button>(root, "bestiaryTab")
            };
            for (int i = 0; i < tabButtons.Length; i++)
            {
                var target = (Tab)i;
                if (tabButtons[i] != null)
                    tabButtons[i].clicked += () => Show(target);
            }
            content = MenuUi.Require<Label>(root, "content");
            if (content != null)
                content.enableRichText = true;
            scroll = MenuUi.Require<ScrollView>(root, "scroll");
            var back = MenuUi.Require<Button>(root, "backButton");
            if (back != null)
                back.clicked += Close;
            MenuSounds.Register(root);
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            MenuUi.Show(root);
            Show(tab);
        }

        public void Close()
        {
            if (!IsOpen)
                return;
            MenuUi.Hide(root);
            var closed = onClosed;
            onClosed = null;
            closed?.Invoke();
        }

        private void Show(Tab next)
        {
            tab = next;
            for (int i = 0; i < tabButtons.Length; i++)
                tabButtons[i]?.EnableInClassList(ActiveTabClass, i == (int)tab);
            MenuUi.SetText(content, tab switch
            {
                Tab.Challenges => ChallengesPage(),
                Tab.Items => ItemsPage(),
                Tab.Bestiary => BestiaryPage(),
                _ => StatsPage()
            });
            if (scroll != null)
                scroll.scrollOffset = Vector2.zero;
            MenuUi.Focus(tabButtons[(int)tab]);
        }

        private static string StatsPage()
        {
            var builder = new StringBuilder();
            void Line(string label, long value) => builder.AppendLine($"{label} : <b>{value:N0}</b>");
            builder.AppendLine("<size=120%><b>Général</b></size>");
            Line("Parties jouées", MetaProgression.GetStat(ChallengeStat.Runs));
            Line("Victoires", MetaProgression.GetStat(ChallengeStat.Victories));
            float best = MetaProgression.Profile.bestVictoryTime;
            builder.AppendLine($"Meilleur temps de victoire : <b>{(best > 0f ? $"{(int)best / 60:00}:{(int)best % 60:00}" : "—")}</b>");
            Line("Étape maximale", MetaProgression.GetStat(ChallengeStat.MaxStage));
            Line("Ennemis tués", MetaProgression.GetStat(ChallengeStat.Kills));
            Line("Élites tuées", MetaProgression.GetStat(ChallengeStat.EliteKills));
            Line("Boss vaincus", MetaProgression.GetStat(ChallengeStat.BossKills));
            Line("Dégâts infligés", MetaProgression.GetStat(ChallengeStat.DamageDealt));
            Line("Or ramassé", MetaProgression.GetStat(ChallengeStat.GoldEarned));
            Line("Coffres ouverts", MetaProgression.GetStat(ChallengeStat.ChestsOpened));
            Line("Mises à terre", MetaProgression.GetStat(ChallengeStat.Downs));

            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return builder.ToString();
            builder.AppendLine();
            builder.AppendLine("<size=120%><b>Par personnage</b></size>");
            foreach (var character in catalog.Characters)
            {
                if (character == null)
                    continue;
                string state = MetaProgression.IsUnlocked(character) ? string.Empty : " <alpha=#80>(verrouillé)<alpha=#FF>";
                builder.AppendLine($"<b>{character.displayName}</b>{state} — parties {MetaProgression.GetStat(ChallengeStat.Runs, character)}, " +
                                   $"victoires {MetaProgression.GetStat(ChallengeStat.Victories, character)}, " +
                                   $"étape max {MetaProgression.GetStat(ChallengeStat.MaxStage, character)}, " +
                                   $"kills {MetaProgression.GetStat(ChallengeStat.Kills, character):N0}");
            }
            return builder.ToString();
        }

        private static string ChallengesPage()
        {
            var builder = new StringBuilder();
            int done = 0, total = 0;
            foreach (var challenge in MetaProgression.Challenges)
            {
                if (challenge == null)
                    continue;
                total++;
                bool completed = MetaProgression.IsCompleted(challenge);
                if (completed)
                    done++;
                string mark = completed ? "<color=#70D070>[x]</color>" : "<color=#A0A0A0>[ ]</color>";
                string progress = completed ? "Accompli" : $"{MetaProgression.Progress(challenge):N0} / {challenge.target:N0}";
                builder.AppendLine($"{mark} <b>{challenge.displayName}</b> — {challenge.description}");
                builder.AppendLine($"    <size=85%><alpha=#B0>{progress} · {challenge.RewardName}</size><alpha=#FF>");
            }
            return $"<size=110%>Défis accomplis : <b>{done} / {total}</b></size>\n\n" + builder;
        }

        private static string ItemsPage()
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return string.Empty;
            var builder = new StringBuilder();
            int found = 0;
            foreach (var item in catalog.Items)
            {
                if (item == null)
                    continue;
                string color = ColorUtility.ToHtmlStringRGB(ItemDefinition.GetRarityColor(item.rarity));
                if (!MetaProgression.IsUnlocked(item))
                {
                    var challenge = MetaProgression.UnlockedBy(item);
                    builder.AppendLine($"<alpha=#70>[verrouillé] {item.displayName} — défi : {(challenge != null ? challenge.description : "?")}<alpha=#FF>");
                }
                else if (MetaProgression.IsDiscovered(item))
                {
                    found++;
                    builder.AppendLine($"<color=#{color}><b>{item.displayName}</b></color> ({ItemDefinition.GetRarityName(item.rarity)}) — {item.description}");
                }
                else
                {
                    builder.AppendLine($"<color=#{color}>???</color> <alpha=#80>({ItemDefinition.GetRarityName(item.rarity)}) — pas encore trouvé<alpha=#FF>");
                }
            }
            return $"<size=110%>Objets découverts : <b>{found} / {catalog.Items.Count}</b></size>\n\n" + builder;
        }

        private static string BestiaryPage()
        {
            var catalog = GameCatalog.Instance;
            if (catalog == null)
                return string.Empty;
            var builder = new StringBuilder();
            foreach (var enemy in catalog.Enemies)
            {
                if (enemy == null)
                    continue;
                if (!MetaProgression.HasSeen(enemy))
                {
                    builder.AppendLine("<alpha=#80>??? — jamais rencontré<alpha=#FF>");
                    continue;
                }
                string boss = enemy.isBoss ? " <color=#E8C060>(boss)</color>" : string.Empty;
                builder.AppendLine($"<b>{enemy.displayName}</b>{boss} — {enemy.maxHealth:0} PV de base · tués : {MetaProgression.GetEnemyKills(enemy):N0}");
            }
            return builder.ToString();
        }
    }
}
