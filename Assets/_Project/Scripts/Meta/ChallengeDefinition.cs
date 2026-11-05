using RiskOfMagic.Characters;
using RiskOfMagic.Items;
using RiskOfMagic.Skills;
using UnityEngine;

namespace RiskOfMagic.Meta
{
    /// <summary>Counters tracked locally by ChallengeTracker (global and per character).</summary>
    public enum ChallengeStat : byte
    {
        Runs,
        Victories,
        MaxStage,
        Kills,
        EliteKills,
        BossKills,
        AvatarKills,
        Downs,
        ChestsOpened,
        TombsPurified,
        AltarsCompleted,
        FastStages,
        StagesWithoutDown,
        TeleporterEventsWithoutDown,
        Freezes,
        PoisonedEnemies,
        DamageDealt,
        GoldEarned
    }

    /// <summary>
    /// A challenge: reach `target` on `stat` (optionally while playing `character`) to unlock `reward`
    /// (a CharacterDefinition, ItemDefinition or SkillDefinition) and/or the hard mode. Anything rewarded by a
    /// challenge is locked until it is completed; everything else is available from the start.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Meta/Challenge", fileName = "Challenge")]
    public class ChallengeDefinition : ScriptableObject
    {
        public string displayName = "Défi";
        [TextArea] public string description;
        public ChallengeStat stat;
        [Min(1)] public long target = 1;
        [Tooltip("Only counts while playing this character (empty = any).")] public CharacterDefinition character;

        [Header("Reward")]
        [Tooltip("CharacterDefinition, ItemDefinition or SkillDefinition.")] public Object reward;
        public bool unlocksHardMode;

        public string Id => name;

        public string RewardName
        {
            get
            {
                if (unlocksHardMode)
                    return "Mode Difficile";
                return reward switch
                {
                    CharacterDefinition c => $"Personnage : {c.displayName}",
                    ItemDefinition i => $"Objet : {i.displayName}",
                    SkillDefinition s => $"Compétence : {s.displayName}",
                    _ => "—"
                };
            }
        }
    }
}
