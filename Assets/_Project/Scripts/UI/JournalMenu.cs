using System;
using System.Text;
using RiskOfMagic.Core;
using RiskOfMagic.Items;
using RiskOfMagic.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RiskOfMagic.UI
{
    /// <summary>Main menu journal: statistics, challenges, items, bestiary (local profile). Text pages for now.</summary>
    public class JournalMenu : MonoBehaviour
    {
        private enum Tab { Stats, Challenges, Items, Bestiary }

        [SerializeField] private HudTheme theme;
        [SerializeField] private Button statsTab;
        [SerializeField] private Button challengesTab;
        [SerializeField] private Button itemsTab;
        [SerializeField] private Button bestiaryTab;
        [SerializeField] private TMP_Text content;
        [SerializeField] private ScrollRect scroll;
        [SerializeField] private Button backButton;

        private Action onClosed;
        private Tab tab;

        private void Awake()
        {
            Listen(statsTab, () => Show(Tab.Stats));
            Listen(challengesTab, () => Show(Tab.Challenges));
            Listen(itemsTab, () => Show(Tab.Items));
            Listen(bestiaryTab, () => Show(Tab.Bestiary));
            Listen(backButton, Close);
        }

        public void Open(Action closed)
        {
            onClosed = closed;
            gameObject.SetActive(true);
            Show(tab);
        }

        public void Close()
        {
            gameObject.SetActive(false);
            onClosed?.Invoke();
        }

        private void Show(Tab next)
        {
            tab = next;
            Tint(statsTab, tab == Tab.Stats);
            Tint(challengesTab, tab == Tab.Challenges);
            Tint(itemsTab, tab == Tab.Items);
            Tint(bestiaryTab, tab == Tab.Bestiary);
            if (content != null)
                content.text = tab switch
                {
                    Tab.Challenges => ChallengesPage(),
                    Tab.Items => ItemsPage(),
                    Tab.Bestiary => BestiaryPage(),
                    _ => StatsPage()
                };
            if (scroll != null)
                scroll.verticalNormalizedPosition = 1f;
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

        private void Tint(Button button, bool selected)
        {
            if (button == null || theme == null || button.targetGraphic == null)
                return;
            button.targetGraphic.color = selected ? theme.buttonSelectedColor : theme.buttonColor;
        }

        private static void Listen(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
                button.onClick.AddListener(action);
        }
    }
}
