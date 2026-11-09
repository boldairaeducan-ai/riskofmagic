using System;
using System.Collections.Generic;
using RiskOfMagic.Characters;
using RiskOfMagic.Core;
using RiskOfMagic.Enemies;
using RiskOfMagic.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RiskOfMagic.Meta
{
    /// <summary>
    /// Local meta progression: counters, challenges, unlocks, loadouts. Each player has their own profile; in coop the
    /// server rolls items with the host's unlocks and each player picks among their own characters and skills.
    /// </summary>
    public static class MetaProgression
    {
        private static PlayerProfile profile;
        private static readonly List<ChallengeDefinition> completedThisRun = new();

        /// <summary>Raised when a challenge is completed (local).</summary>
        public static event Action<ChallengeDefinition> ChallengeCompleted;

        public static PlayerProfile Profile => profile ??= PlayerProfile.Load();
        public static IReadOnlyList<ChallengeDefinition> Challenges =>
            GameCatalog.Instance != null ? GameCatalog.Instance.Challenges : Array.Empty<ChallengeDefinition>();
        /// <summary>Completed since the last ClearRunLog (end screen).</summary>
        public static IReadOnlyList<ChallengeDefinition> CompletedThisRun => completedThisRun;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            profile = null;
            ChallengeCompleted = null;
            completedThisRun.Clear();
        }

        public static void Save() => Profile.Save();
        public static void ClearRunLog() => completedThisRun.Clear();

        // ---------------------------------------------------------------- Counters

        private static string Key(ChallengeStat stat, CharacterDefinition character)
            => character != null ? $"{stat}@{character.name}" : stat.ToString();

        public static long GetStat(ChallengeStat stat, CharacterDefinition character = null) => Profile.Get(Profile.counters, Key(stat, character));

        /// <summary>Adds to the global counter and to the character's.</summary>
        public static void Add(ChallengeStat stat, long amount, CharacterDefinition character)
        {
            if (amount <= 0)
                return;
            var p = Profile;
            p.Set(p.counters, Key(stat, null), p.Get(p.counters, Key(stat, null)) + amount);
            if (character != null)
                p.Set(p.counters, Key(stat, character), p.Get(p.counters, Key(stat, character)) + amount);
            CheckChallenges(stat);
        }

        /// <summary>Keeps the highest value (records such as the max stage).</summary>
        public static void Max(ChallengeStat stat, long value, CharacterDefinition character)
        {
            var p = Profile;
            if (value > p.Get(p.counters, Key(stat, null)))
                p.Set(p.counters, Key(stat, null), value);
            if (character != null && value > p.Get(p.counters, Key(stat, character)))
                p.Set(p.counters, Key(stat, character), value);
            CheckChallenges(stat);
        }

        // ---------------------------------------------------------------- Challenges & unlocks

        public static bool IsCompleted(ChallengeDefinition challenge) => challenge != null && Profile.completedChallenges.Contains(challenge.Id);

        public static long Progress(ChallengeDefinition challenge) =>
            Math.Min(challenge.target, GetStat(challenge.stat, challenge.character));

        private static void CheckChallenges(ChallengeStat stat)
        {
            foreach (var challenge in Challenges)
            {
                if (challenge == null || challenge.stat != stat || IsCompleted(challenge))
                    continue;
                if (GetStat(stat, challenge.character) < challenge.target)
                    continue;
                Profile.completedChallenges.Add(challenge.Id);
                completedThisRun.Add(challenge);
                Save();
                Debug.Log($"[Meta] Challenge completed: {challenge.displayName} → {challenge.RewardName}");
                ChallengeCompleted?.Invoke(challenge);
            }
        }

        private const string UnlockAllKey = "RiskOfMagic.Debug.UnlockAll";

        /// <summary>
        /// Testing: every character, skill, item and the hard mode are available (Tools → Risk of Magic → Debug).
        /// Editor and development builds only; challenges still progress and complete normally.
        /// </summary>
        public static bool DebugUnlockAll
        {
            get => (Application.isEditor || Debug.isDebugBuild) && PlayerPrefs.GetInt(UnlockAllKey, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(UnlockAllKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>The challenge that unlocks this reward (null = available from the start).</summary>
        public static ChallengeDefinition UnlockedBy(Object reward)
        {
            if (reward == null || DebugUnlockAll)
                return null;
            foreach (var challenge in Challenges)
                if (challenge != null && challenge.reward == reward)
                    return challenge;
            return null;
        }

        public static bool IsUnlocked(Object reward)
        {
            var challenge = UnlockedBy(reward);
            return challenge == null || IsCompleted(challenge);
        }

        public static ChallengeDefinition HardModeChallenge
        {
            get
            {
                if (DebugUnlockAll)
                    return null;
                foreach (var challenge in Challenges)
                    if (challenge != null && challenge.unlocksHardMode)
                        return challenge;
                return null;
            }
        }

        public static bool IsHardModeUnlocked => HardModeChallenge == null || IsCompleted(HardModeChallenge);

        // ---------------------------------------------------------------- Loadouts, discoveries

        /// <summary>Bit i set = alternative skill in slot i (only kept if still unlocked).</summary>
        public static byte GetLoadout(CharacterDefinition character)
        {
            if (character == null)
                return 0;
            byte mask = (byte)Profile.Get(Profile.loadouts, character.name);
            for (int i = 0; i < 4; i++)
            {
                var alt = character.GetAlternative((SkillSlot)i);
                if ((mask & (1 << i)) != 0 && (alt == null || !IsUnlocked(alt)))
                    mask &= (byte)~(1 << i);
            }
            return mask;
        }

        public static void SetLoadout(CharacterDefinition character, byte mask)
        {
            if (character == null)
                return;
            Profile.Set(Profile.loadouts, character.name, mask);
            Save();
        }

        public static void Discover(ItemDefinition item)
        {
            if (item != null && !Profile.discoveredItems.Contains(item.name))
                Profile.discoveredItems.Add(item.name);
        }

        public static bool IsDiscovered(ItemDefinition item) => item != null && Profile.discoveredItems.Contains(item.name);

        public static void SeeEnemy(EnemyDefinition enemy)
        {
            if (enemy != null && !Profile.seenEnemies.Contains(enemy.name))
                Profile.seenEnemies.Add(enemy.name);
        }

        public static void AddEnemyKill(EnemyDefinition enemy)
        {
            if (enemy == null)
                return;
            SeeEnemy(enemy);
            Profile.Set(Profile.enemyKills, enemy.name, Profile.Get(Profile.enemyKills, enemy.name) + 1);
        }

        public static long GetEnemyKills(EnemyDefinition enemy) => enemy != null ? Profile.Get(Profile.enemyKills, enemy.name) : 0;
        public static bool HasSeen(EnemyDefinition enemy) => enemy != null && Profile.seenEnemies.Contains(enemy.name);

        public static void RecordVictoryTime(float seconds)
        {
            if (seconds > 0f && (Profile.bestVictoryTime <= 0f || seconds < Profile.bestVictoryTime))
                Profile.bestVictoryTime = seconds;
        }
    }
}
