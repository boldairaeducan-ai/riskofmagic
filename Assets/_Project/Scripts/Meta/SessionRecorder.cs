using System;
using System.Collections.Generic;
using System.IO;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using RiskOfMagic.Player;
using RiskOfMagic.Run;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RiskOfMagic.Meta
{
    /// <summary>
    /// Playtest session log (local, every peer records its own player): one JSON file per run in
    /// persistentDataPath/Sessions with the character, each stage (map, duration, kills, knock-downs, items, gold, sampled
    /// positions, knock-down/death spots and causes) and the result. Read by Tools → Risk of Magic → Heatmap.
    /// </summary>
    public sealed class SessionRecorder : MonoBehaviour
    {
        public const string FolderName = "Sessions";
        private const float SampleInterval = 1f;

        [Serializable]
        public class DeathEvent
        {
            public float time;
            public float x, y, z;
            public string cause;
            public bool downed;
        }

        [Serializable]
        public class StageRecord
        {
            public int number;
            public string scene;
            public float startTime;
            public float duration;
            public int kills;
            public int knockDowns;
            public int items;
            public int goldEarned;
            public float damageDealt;
            public int playerLevel;
            /// <summary>Sampled positions (x, y, z triplets), one per second while alive.</summary>
            public List<float> positions = new();
            public List<DeathEvent> deaths = new();
        }

        [Serializable]
        public class Session
        {
            public string version;
            public string startedAt;
            public string character;
            public int players;
            public string difficulty;
            public List<StageRecord> stages = new();
            public string result = "abandon";
            public float totalTime;
            public int stageReached;
        }

        public static string Folder => Path.Combine(Application.persistentDataPath, FolderName);

        private Session session;
        private StageRecord stage;
        private string filePath;
        private PlayerAvatar player;
        private float sessionStart;
        private float nextSample;
        private float missingSince = -1f;
        private string lastDamageCause = "Inconnu";
        private bool wasDowned;
        private bool ended;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject("SessionRecorder");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            go.AddComponent<SessionRecorder>();
        }

        private void Update()
        {
            var local = PlayerAvatar.Local;
            var stages = StageManager.Instance;
            bool inRun = local != null && local.IsSpawned && stages != null && stages.IsSpawned && RunDifficulty.Instance != null;

            if (session == null)
            {
                if (inRun)
                    Begin(local);
                return;
            }

            // Left the run (main menu, disconnection): close the session after a short grace period.
            if (!inRun)
            {
                if (missingSince < 0f)
                    missingSince = Time.unscaledTime;
                else if (Time.unscaledTime - missingSince > 3f)
                    End(null);
                return;
            }
            missingSince = -1f;

            // Restart after a game over: the next frame starts a new session.
            if (ended && !stages.IsGameOver)
            {
                End(null);
                return;
            }

            if (local != player)
                Bind(local);

            if (!ended && (stages.StageNumber != stage.number || SceneManager.GetActiveScene().name != stage.scene))
            {
                CloseStage();
                OpenStage(stages.StageNumber);
                Save();
            }

            if (!ended && stages.IsGameOver)
                End(stages.IsVictory ? "victoire" : "défaite");
            if (ended)
                return;

            if (Time.time >= nextSample)
            {
                nextSample = Time.time + SampleInterval;
                if (player.IsAlive && !player.IsDowned)
                {
                    Vector3 p = player.transform.position;
                    stage.positions.Add(Round(p.x));
                    stage.positions.Add(Round(p.y));
                    stage.positions.Add(Round(p.z));
                }
            }

            bool downed = player.IsDowned;
            if (downed && !wasDowned)
                AddDeath(true);
            wasDowned = downed;
        }

        private void Begin(PlayerAvatar local)
        {
            session = new Session
            {
                version = Application.version,
                startedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                character = local.Character != null ? local.Character.displayName : "?",
                players = PlayerAvatar.All.Count,
                difficulty = RunDifficulty.Instance.Difficulty.ToString()
            };
            sessionStart = Time.time;
            ended = false;
            missingSince = -1f;
            Directory.CreateDirectory(Folder);
            filePath = Path.Combine(Folder, $"session_{DateTime.Now:yyyyMMdd_HHmmss}_{local.OwnerClientId}.json");
            Bind(local);
            OpenStage(StageManager.Instance.StageNumber);
            Save();
            Debug.Log($"[Risk of Magic] Session log: {filePath}");
        }

        private void Bind(PlayerAvatar local)
        {
            if (player != null && player.Health != null)
            {
                player.Health.Damaged -= OnDamaged;
                player.Health.Died -= OnDied;
            }
            player = local;
            player.Health.Damaged += OnDamaged;
            player.Health.Died += OnDied;
            wasDowned = player.IsDowned;
        }

        private void OpenStage(int number)
        {
            // The stage number changes a moment before the next map is loaded: that in-between record (old map, new
            // number, a fraction of a second) is noise, the next one replaces it.
            if (stage != null && stage.duration < 1f && session.stages.Count > 0 && session.stages[^1] == stage)
                session.stages.RemoveAt(session.stages.Count - 1);
            stage = new StageRecord { number = number, scene = SceneManager.GetActiveScene().name, startTime = Time.time - sessionStart };
            session.stages.Add(stage);
            session.stageReached = Mathf.Max(session.stageReached, number);
            nextSample = Time.time;
        }

        /// <summary>Stage values are cumulative run stats at the end of the stage.</summary>
        private void CloseStage()
        {
            if (stage == null || player == null)
                return;
            stage.duration = Time.time - sessionStart - stage.startTime;
            var run = player.RunStats;
            if (run != null)
            {
                stage.kills = run.Kills;
                stage.knockDowns = run.Deaths;
                stage.goldEarned = run.GoldEarned;
                stage.damageDealt = Mathf.Round(run.DamageDealt);
            }
            stage.items = CountItems(player);
            stage.playerLevel = player.Experience != null ? player.Experience.Level : 1;
        }

        private void End(string result)
        {
            if (session == null)
                return;
            if (!ended)
            {
                CloseStage();
                if (result != null)
                    session.result = result;
                session.totalTime = Time.time - sessionStart;
                ended = true;
                Save();
            }
            if (result == null) // really left the run
            {
                if (player != null && player.Health != null)
                {
                    player.Health.Damaged -= OnDamaged;
                    player.Health.Died -= OnDied;
                }
                session = null;
                stage = null;
                player = null;
            }
        }

        private void OnApplicationQuit() => End(null);

        private void OnDamaged(DamageInfo info)
        {
            if (info.IgnoreModifiers || info.Amount <= 0f)
                return;
            lastDamageCause = DescribeSource(info);
        }

        private void OnDied(DamageInfo info)
        {
            if (!info.IgnoreModifiers)
                lastDamageCause = DescribeSource(info); // a bleed-out keeps the knock-down cause
            AddDeath(false);
        }

        private void AddDeath(bool downed)
        {
            if (stage == null || player == null)
                return;
            Vector3 p = player.transform.position;
            stage.deaths.Add(new DeathEvent
            {
                time = Time.time - sessionStart, x = Round(p.x), y = Round(p.y), z = Round(p.z),
                cause = lastDamageCause, downed = downed
            });
            Save();
        }

        private static string DescribeSource(in DamageInfo info)
        {
            if (!info.TryGetSource(out var source) || source == null)
                return info.Type.ToString();
            if (source.TryGetComponent(out EnemyActor enemy))
                return (enemy.IsElite ? "Élite " : string.Empty) + enemy.DisplayName;
            return source.name.Replace("(Clone)", string.Empty).Trim();
        }

        private static int CountItems(PlayerAvatar avatar)
        {
            var inventory = avatar.Inventory;
            if (inventory == null)
                return 0;
            int total = 0;
            for (int i = 0; i < inventory.SlotCount; i++)
                total += inventory.GetCountAt(i);
            return total;
        }

        private static float Round(float value) => Mathf.Round(value * 10f) / 10f;

        private void Save()
        {
            if (session == null || string.IsNullOrEmpty(filePath))
                return;
            try
            {
                File.WriteAllText(filePath, JsonUtility.ToJson(session, true));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Risk of Magic] Session log not saved: {exception.Message}");
            }
        }
    }
}
