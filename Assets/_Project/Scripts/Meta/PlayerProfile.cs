using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RiskOfMagic.Meta
{
    /// <summary>
    /// Local save of the meta progression (JSON in Application.persistentDataPath/profile.json): counters,
    /// completed challenges, discovered items, bestiary, skill loadouts and records.
    /// </summary>
    [Serializable]
    public class PlayerProfile
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public long value;
        }

        public int version = 1;
        public List<Entry> counters = new();
        public List<string> completedChallenges = new();
        public List<string> discoveredItems = new();
        public List<string> seenEnemies = new();
        public List<Entry> enemyKills = new();
        /// <summary>key = character asset name, value = loadout mask (bit per skill slot, 1 = alternative).</summary>
        public List<Entry> loadouts = new();
        public float bestVictoryTime;

        private const string FileName = "profile.json";
        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        public static PlayerProfile Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return JsonUtility.FromJson<PlayerProfile>(File.ReadAllText(FilePath)) ?? new PlayerProfile();
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Profile] Could not read {FilePath}: {exception.Message}");
            }
            return new PlayerProfile();
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(this, true));
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Profile] Could not write {FilePath}: {exception.Message}");
            }
        }

        public long Get(List<Entry> list, string key)
        {
            foreach (var entry in list)
                if (entry.key == key)
                    return entry.value;
            return 0;
        }

        public void Set(List<Entry> list, string key, long value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].key != key)
                    continue;
                list[i] = new Entry { key = key, value = value };
                return;
            }
            list.Add(new Entry { key = key, value = value });
        }
    }
}
