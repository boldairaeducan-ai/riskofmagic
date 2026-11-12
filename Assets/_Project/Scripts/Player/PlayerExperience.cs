using System;
using RiskOfMagic.Combat;
using RiskOfMagic.Enemies;
using Unity.Netcode;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Risk of Rain 2-style levels: every kill grants experience to the whole team (= the enemy's gold reward, which
    /// already scales with the run difficulty). Each level raises base damage, max health and regen
    /// (CharacterDefinition per-level values). Server writes, every peer reads. Kept across stages, reset on a new run.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerExperience : NetworkBehaviour
    {
        private float experiencePerGold => RiskOfMagic.Core.GameBalance.Instance.experience.experiencePerGold;
        private float firstLevelExperience => RiskOfMagic.Core.GameBalance.Instance.experience.firstLevelExperience;
        private float levelGrowth => RiskOfMagic.Core.GameBalance.Instance.experience.levelGrowth;
        private int maxLevel => RiskOfMagic.Core.GameBalance.Instance.experience.maxLevel;

        private readonly NetworkVariable<float> experience = new();
        private readonly NetworkVariable<int> level = new(1);

        /// <summary>Every peer: (new level).</summary>
        public event Action<int> LevelChanged;

        public int Level => level.Value;
        public float Experience => experience.Value;

        /// <summary>0..1 progress toward the next level.</summary>
        public float Progress
        {
            get
            {
                float from = TotalForLevel(level.Value), to = TotalForLevel(level.Value + 1);
                return to > from ? Mathf.Clamp01((experience.Value - from) / (to - from)) : 1f;
            }
        }

        /// <summary>Cumulative experience needed to reach a level (level 1 = 0).</summary>
        public float TotalForLevel(int targetLevel)
            => targetLevel <= 1 ? 0f : firstLevelExperience * (Mathf.Pow(levelGrowth, targetLevel - 1) - 1f) / (levelGrowth - 1f);

        public override void OnNetworkSpawn()
        {
            level.OnValueChanged += OnLevelChanged;
            if (IsServer)
                EnemyActor.Killed += OnEnemyKilled;
        }

        public override void OnNetworkDespawn()
        {
            level.OnValueChanged -= OnLevelChanged;
            if (IsServer)
                EnemyActor.Killed -= OnEnemyKilled;
        }

        /// <summary>Server only (new run).</summary>
        public void ResetExperience()
        {
            if (!IsServer)
                return;
            experience.Value = 0f;
            level.Value = 1;
        }

        /// <summary>Server only.</summary>
        public void AddExperience(float amount)
        {
            if (!IsServer || amount <= 0f)
                return;
            experience.Value += amount;
            int newLevel = level.Value;
            while (newLevel < maxLevel && experience.Value >= TotalForLevel(newLevel + 1))
                newLevel++;
            if (newLevel != level.Value)
                level.Value = newLevel;
        }

        private void OnEnemyKilled(EnemyActor enemy, DamageInfo killingBlow) => AddExperience(enemy.GoldReward / Mathf.Max(0.01f, RiskOfMagic.Core.GameBalance.Instance.economy.enemyGoldMultiplier)
                                        * experiencePerGold); // experience ignores the gold multiplier

        private void OnLevelChanged(int previous, int current) => LevelChanged?.Invoke(current);
    }
}
