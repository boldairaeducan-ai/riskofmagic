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
        [SerializeField, Min(0f), Tooltip("Experience per point of gold reward.")] private float experiencePerGold = 1f;
        [SerializeField, Min(1f), Tooltip("Experience to go from level 1 to 2.")] private float firstLevelExperience = 20f;
        [SerializeField, Min(1.01f), Tooltip("Each level needs this much more experience than the previous one.")] private float levelGrowth = 1.55f;
        [SerializeField, Min(1)] private int maxLevel = 99;

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

        private void OnEnemyKilled(EnemyActor enemy, DamageInfo killingBlow) => AddExperience(enemy.GoldReward * experiencePerGold);

        private void OnLevelChanged(int previous, int current) => LevelChanged?.Invoke(current);
    }
}
