using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Items;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// Turns replicated game events into local sounds (no audio RPC): hits, kills, enemy hurt/death,
    /// local player feedback, gold, items, teleporter, game over, scene/boss music, UI controls.
    /// </summary>
    internal sealed class GameAudioEvents : MonoBehaviour
    {
        private const float SelectableScanInterval = 0.5f;

        private static Selectable[] selectables = new Selectable[64];

        private PlayerAvatar boundPlayer;
        private Teleporter boundTeleporter;
        private StageManager boundStage;
        private bool wasDowned;
        private float nextScan;

        private static AudioConfig Config => AudioService.Config;

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
            Health.AnyDied += OnAnyDied;
            PlayerInventory.LocalItemGained += OnItemGained;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDied -= OnAnyDied;
            PlayerInventory.LocalItemGained -= OnItemGained;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            BindPlayer(null);
            BindTeleporter(null);
            BindStage(null);
        }

        private void Update()
        {
            // Instances come and go with spawns/scenes: rebind when they change.
            if (boundPlayer != PlayerAvatar.Local)
                BindPlayer(PlayerAvatar.Local);
            if (boundTeleporter != Teleporter.Instance)
                BindTeleporter(Teleporter.Instance);
            if (boundStage != StageManager.Instance)
                BindStage(StageManager.Instance);

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + SelectableScanInterval;
                AttachUiSounds();
            }
        }

        // ------------------------------------------------------------------ Music

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;
            AudioService.SetOverrideMusic(null);
            var sceneMusic = FindAnyObjectByType<SceneMusic>();
            if (sceneMusic != null)
                AudioService.SetSceneMusic(sceneMusic.Track);
            else if (Config != null)
                AudioService.SetSceneMusic(FindAnyObjectByType<RiskOfMagic.UI.MainMenuController>() != null ? Config.mainMenuMusic : Config.stageMusic);
        }

        // ------------------------------------------------------------------ Combat

        private static void OnAnyDamaged(Health victim, DamageInfo info)
        {
            if (victim == null || info.IgnoreModifiers)
                return;
            var local = PlayerAvatar.Local;
            var config = Config;

            if (victim.TryGetComponent(out EnemyActor enemy) && enemy.Definition != null)
                AudioService.PlayAt(enemy.Definition.hurtSound, info.Point != Vector3.zero ? info.Point : victim.transform.position);

            if (local == null || config == null)
                return;
            if (victim.gameObject == local.gameObject)
                AudioService.Play2D(config.playerHurt);
            else if (info.HasSource && info.SourceId == local.NetworkObjectId && info.ProcCoefficient > 0f)
                AudioService.Play2D(info.IsCritical ? AudioService.Pick(config.criticalHitConfirm, config.hitConfirm) : config.hitConfirm);
        }

        private static void OnAnyDied(Health victim, DamageInfo info)
        {
            if (victim == null)
                return;
            var local = PlayerAvatar.Local;
            var config = Config;

            if (victim.TryGetComponent(out EnemyActor enemy) && enemy.Definition != null)
                AudioService.PlayAt(enemy.Definition.deathSound, victim.transform.position);

            if (local == null || config == null)
                return;
            if (victim.gameObject == local.gameObject)
                AudioService.Play2D(config.playerDeath);
            else if (info.HasSource && info.SourceId == local.NetworkObjectId)
                AudioService.Play2D(config.killConfirm);
        }

        // ------------------------------------------------------------------ Local player

        private void BindPlayer(PlayerAvatar player)
        {
            if (boundPlayer != null)
            {
                if (boundPlayer.Wallet != null)
                    boundPlayer.Wallet.GoldChanged -= OnGoldChanged;
                if (boundPlayer.Downed != null)
                    boundPlayer.Downed.Changed -= OnDownedChanged;
            }
            boundPlayer = player;
            if (player == null)
                return;
            if (player.Wallet != null)
                player.Wallet.GoldChanged += OnGoldChanged;
            if (player.Downed != null)
            {
                wasDowned = player.Downed.IsDowned;
                player.Downed.Changed += OnDownedChanged;
            }
        }

        private static void OnGoldChanged(uint previous, uint current)
        {
            if (current > previous)
                AudioService.Play2D(Config != null ? Config.goldGained : null);
        }

        private void OnDownedChanged()
        {
            if (boundPlayer == null || boundPlayer.Downed == null)
                return;
            bool downed = boundPlayer.Downed.IsDowned;
            if (downed && !wasDowned)
                AudioService.Play2D(Config != null ? Config.playerDowned : null);
            else if (!downed && wasDowned && boundPlayer.IsAlive)
                AudioService.Play2D(Config != null ? Config.playerRevived : null);
            wasDowned = downed;
        }

        private static void OnItemGained(ItemDefinition item, int stack)
        {
            if (item == null)
                return;
            AudioService.Play2D(AudioService.Pick(item.pickupSound, Config != null ? Config.GetItemPickup(item.rarity) : null));
        }

        // ------------------------------------------------------------------ Run

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
            var config = Config;
            if (config == null || boundTeleporter == null)
                return;
            Vector3 position = boundTeleporter.transform.position;
            switch (state)
            {
                case TeleporterState.Charging:
                    AudioService.PlayAt(config.teleporterActivated, position);
                    AudioService.SetOverrideMusic(config.bossMusic);
                    break;
                case TeleporterState.Charged:
                    AudioService.PlayAt(config.teleporterCharged, position);
                    break;
                case TeleporterState.Ready:
                    AudioService.PlayAt(config.teleporterReady, position);
                    AudioService.SetOverrideMusic(null);
                    break;
                default:
                    AudioService.SetOverrideMusic(null);
                    break;
            }
        }

        private void BindStage(StageManager stage)
        {
            if (boundStage != null)
                boundStage.GameOverChanged -= OnGameOverChanged;
            boundStage = stage;
            if (stage != null)
                stage.GameOverChanged += OnGameOverChanged;
        }

        private static void OnGameOverChanged(bool gameOver)
        {
            var config = Config;
            if (config == null)
                return;
            if (gameOver)
            {
                AudioService.Play2D(config.gameOver);
                AudioService.SetOverrideMusic(config.gameOverMusic);
            }
            else
            {
                AudioService.SetOverrideMusic(null);
            }
        }

        // ------------------------------------------------------------------ UI

        private static void AttachUiSounds()
        {
            int count = Selectable.allSelectableCount;
            if (count == 0)
                return;
            if (selectables.Length < count)
                selectables = new Selectable[Mathf.NextPowerOfTwo(count)];
            count = Selectable.AllSelectablesNoAlloc(selectables);
            for (int i = 0; i < count; i++)
            {
                var selectable = selectables[i];
                if (selectable != null && !selectable.TryGetComponent(out UiSoundTrigger _))
                    selectable.gameObject.AddComponent<UiSoundTrigger>();
                selectables[i] = null;
            }
        }
    }
}
