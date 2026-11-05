using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Interaction;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using RiskOfMagic.Run.MapEvents;
using UnityEngine;

namespace RiskOfMagic.Meta
{
    /// <summary>
    /// Local listener (every peer, for its own player) turning replicated game events into meta counters.
    /// Created automatically and kept across scenes.
    /// </summary>
    internal sealed class ChallengeTracker : MonoBehaviour
    {
        private const float FastStageSeconds = 300f;
        private const float FreezeAttributionWindow = 1f;

        private readonly Dictionary<Health, float> lastLocalHit = new();
        private readonly HashSet<Health> poisoned = new();
        private PlayerAvatar boundPlayer;
        private StageManager boundStage;
        private Teleporter boundTeleporter;
        private bool runActive;
        private int currentStage;
        private float stageStartTime;
        private int stageDowns;
        private bool teleporterEvent;
        private int teleporterDowns;
        private bool wasDowned;
        private float damageRemainder;
        private float nextEnemyScan;

        private static CharacterDefinition Character => PlayerAvatar.Local != null ? PlayerAvatar.Local.Character : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("ChallengeTracker");
            DontDestroyOnLoad(go);
            go.AddComponent<ChallengeTracker>();
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
            Health.AnyDied += OnAnyDied;
            EnemyActor.AnyFrozen += OnEnemyFrozen;
            PlayerInventory.LocalItemGained += OnItemGained;
            PurchaseInteractable.LocalPurchased += OnPurchased;
            MapEvent.Completed += OnMapEventCompleted;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDied -= OnAnyDied;
            EnemyActor.AnyFrozen -= OnEnemyFrozen;
            PlayerInventory.LocalItemGained -= OnItemGained;
            PurchaseInteractable.LocalPurchased -= OnPurchased;
            MapEvent.Completed -= OnMapEventCompleted;
            BindPlayer(null);
            BindStage(null);
            BindTeleporter(null);
        }

        private void OnApplicationQuit() => MetaProgression.Save();

        private void Update()
        {
            if (boundPlayer != PlayerAvatar.Local)
                BindPlayer(PlayerAvatar.Local);
            if (boundStage != StageManager.Instance)
                BindStage(StageManager.Instance);
            if (boundTeleporter != Teleporter.Instance)
                BindTeleporter(Teleporter.Instance);

            // A run starts once the local player exists in a running game.
            if (!runActive && boundStage != null && boundPlayer != null && boundPlayer.Character != null && !boundStage.IsGameOver)
                StartRun();

            if (Time.unscaledTime >= nextEnemyScan)
            {
                nextEnemyScan = Time.unscaledTime + 1f;
                foreach (var enemy in EnemyActor.Active)
                    if (enemy != null && !enemy.IsIllusion)
                        MetaProgression.SeeEnemy(enemy.Definition);
            }
        }

        // ---------------------------------------------------------------- Run & stages

        private void StartRun()
        {
            runActive = true;
            MetaProgression.ClearRunLog();
            MetaProgression.Add(ChallengeStat.Runs, 1, Character);
            BeginStage(boundStage.StageNumber);
        }

        private void BeginStage(int stage)
        {
            currentStage = stage;
            stageStartTime = Time.time;
            stageDowns = 0;
            poisoned.Clear();
            lastLocalHit.Clear();
            MetaProgression.Max(ChallengeStat.MaxStage, stage, Character);
            MetaProgression.Save();
        }

        private void CompleteStage()
        {
            if (Time.time - stageStartTime <= FastStageSeconds)
                MetaProgression.Add(ChallengeStat.FastStages, 1, Character);
            if (stageDowns == 0)
                MetaProgression.Add(ChallengeStat.StagesWithoutDown, 1, Character);
        }

        private void OnStageStarted(int stage)
        {
            if (!runActive)
                return;
            if (stage == currentStage + 1)
                CompleteStage();
            BeginStage(stage);
        }

        private void OnGameOverChanged(bool over)
        {
            if (!over)
            {
                runActive = false; // restart: a new run begins on the next frame
                return;
            }
            if (!runActive)
                return;
            if (boundStage != null && boundStage.IsVictory)
            {
                CompleteStage();
                MetaProgression.Add(ChallengeStat.Victories, 1, Character);
                MetaProgression.RecordVictoryTime(boundStage.FinalTime);
            }
            MetaProgression.Save();
        }

        // ---------------------------------------------------------------- Combat

        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            var local = PlayerAvatar.Local;
            if (local == null || victim == null || !info.HasSource || info.SourceId != local.NetworkObjectId || victim.gameObject == local.gameObject)
                return;
            lastLocalHit[victim] = Time.time;

            damageRemainder += info.Amount;
            long whole = (long)damageRemainder;
            if (whole > 0)
            {
                damageRemainder -= whole;
                MetaProgression.Add(ChallengeStat.DamageDealt, whole, Character);
            }

            if (info.Type == DamageType.Poison && poisoned.Add(victim))
                MetaProgression.Add(ChallengeStat.PoisonedEnemies, 1, Character);
        }

        private void OnAnyDied(Health victim, DamageInfo info)
        {
            var local = PlayerAvatar.Local;
            if (victim == null || local == null)
                return;

            if (victim.gameObject == local.gameObject)
            {
                if (!wasDowned) // a bleed-out death was already counted as a down
                    CountDown();
                return;
            }
            if (!victim.TryGetComponent(out EnemyActor enemy) || enemy.IsIllusion)
                return;

            // Shared with the whole team: bosses.
            if (enemy.Definition != null && enemy.Definition.isBoss)
            {
                MetaProgression.Add(ChallengeStat.BossKills, 1, Character);
                if (enemy.TryGetComponent(out ChaosAvatar _))
                    MetaProgression.Add(ChallengeStat.AvatarKills, 1, Character);
            }

            if (!info.HasSource || info.SourceId != local.NetworkObjectId)
                return;
            MetaProgression.Add(ChallengeStat.Kills, 1, Character);
            MetaProgression.AddEnemyKill(enemy.Definition);
            if (enemy.IsElite)
                MetaProgression.Add(ChallengeStat.EliteKills, 1, Character);
        }

        private void OnEnemyFrozen(EnemyActor enemy)
        {
            if (enemy != null && lastLocalHit.TryGetValue(enemy.Health, out float time) && Time.time - time <= FreezeAttributionWindow)
                MetaProgression.Add(ChallengeStat.Freezes, 1, Character);
        }

        private void CountDown()
        {
            stageDowns++;
            teleporterDowns++;
            MetaProgression.Add(ChallengeStat.Downs, 1, Character);
        }

        // ---------------------------------------------------------------- Economy & events

        private static void OnItemGained(ItemDefinition item, int stack) => MetaProgression.Discover(item);

        private static void OnPurchased(PurchaseInteractable purchased)
        {
            if (purchased is ChestInteractable)
                MetaProgression.Add(ChallengeStat.ChestsOpened, 1, Character);
        }

        private static void OnMapEventCompleted(MapEventType type)
        {
            if (PlayerAvatar.Local == null)
                return;
            if (type == MapEventType.PurificationTomb)
                MetaProgression.Add(ChallengeStat.TombsPurified, 1, Character);
            else if (type == MapEventType.ChallengeAltar)
                MetaProgression.Add(ChallengeStat.AltarsCompleted, 1, Character);
        }

        private static void OnGoldChanged(uint previous, uint current)
        {
            if (current > previous)
                MetaProgression.Add(ChallengeStat.GoldEarned, current - previous, Character);
        }

        // ---------------------------------------------------------------- Bindings

        private void BindPlayer(PlayerAvatar player)
        {
            if (boundPlayer != null)
            {
                boundPlayer.Wallet.GoldChanged -= OnGoldChanged;
                if (boundPlayer.Downed != null)
                    boundPlayer.Downed.Changed -= OnDownedChanged;
            }
            boundPlayer = player;
            if (player == null)
                return;
            player.Wallet.GoldChanged += OnGoldChanged;
            if (player.Downed != null)
            {
                wasDowned = player.Downed.IsDowned;
                player.Downed.Changed += OnDownedChanged;
            }
        }

        private void OnDownedChanged()
        {
            bool downed = boundPlayer != null && boundPlayer.Downed != null && boundPlayer.Downed.IsDowned;
            if (downed && !wasDowned)
                CountDown();
            wasDowned = downed;
        }

        private void BindStage(StageManager stage)
        {
            if (boundStage != null)
            {
                boundStage.StageStarted -= OnStageStarted;
                boundStage.GameOverChanged -= OnGameOverChanged;
            }
            boundStage = stage;
            if (stage == null)
            {
                runActive = false;
                return;
            }
            stage.StageStarted += OnStageStarted;
            stage.GameOverChanged += OnGameOverChanged;
        }

        private void BindTeleporter(Teleporter teleporter)
        {
            if (boundTeleporter != null)
                boundTeleporter.StateChanged -= OnTeleporterState;
            boundTeleporter = teleporter;
            if (teleporter != null)
                teleporter.StateChanged += OnTeleporterState;
        }

        private void OnTeleporterState(TeleporterState state)
        {
            if (state == TeleporterState.Charging)
            {
                teleporterEvent = true;
                teleporterDowns = 0;
            }
            else if (state == TeleporterState.Ready && teleporterEvent)
            {
                teleporterEvent = false;
                if (teleporterDowns == 0 && PlayerAvatar.Local != null && PlayerAvatar.Local.IsAlive)
                    MetaProgression.Add(ChallengeStat.TeleporterEventsWithoutDown, 1, Character);
            }
        }
    }
}
