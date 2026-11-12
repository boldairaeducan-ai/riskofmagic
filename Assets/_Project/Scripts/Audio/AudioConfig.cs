using System;
using RiskOfMagic.Items;
using UnityEngine;
using UnityEngine.Audio;

namespace RiskOfMagic.Audio
{
    [Serializable]
    public class MusicTrack
    {
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.7f;
        public bool loop = true;
    }

    /// <summary>
    /// Global audio slots (Resources/AudioConfig): optional mixer, game-wide sounds and music.
    /// Per-object sounds live on their definitions (skills, projectiles, enemies, items, equipment, interactables).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Audio/Audio Config", fileName = "AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        public const string ResourcePath = "AudioConfig";

        [Header("Mixer (optional)")]
        [Tooltip("When set, volumes go through exposed parameters instead of AudioSource volumes.")]
        public AudioMixer mixer;
        public AudioMixerGroup effectsGroup;
        public AudioMixerGroup uiGroup;
        public AudioMixerGroup musicGroup;
        public string musicVolumeParameter = "MusicVolume";
        public string effectsVolumeParameter = "EffectsVolume";

        [Header("UI")]
        [SoundSlot(false, SoundCategory.UI)] public SoundEvent uiClick;
        [SoundSlot(false, SoundCategory.UI)] public SoundEvent uiHover;

        [Header("Combat (local player)")]
        [SoundSlot(false, Cooldown = 0.04f, MaxSimultaneous = 3)] public SoundEvent hitConfirm;
        [SoundSlot(false, Cooldown = 0.04f, MaxSimultaneous = 3)] public SoundEvent criticalHitConfirm;
        [SoundSlot(false, Cooldown = 0.04f, MaxSimultaneous = 3)] public SoundEvent killConfirm;
        [Tooltip("Footsteps of every character on an unknown surface (and fallback of the surfaces below).")]
        [SoundSlot(MaxSimultaneous = 8)] public SoundEvent playerFootstep;
        [SoundSlot(MaxSimultaneous = 8)] public SoundEvent footstepDirt;
        [SoundSlot(MaxSimultaneous = 8)] public SoundEvent footstepSand;
        [SoundSlot(MaxSimultaneous = 8)] public SoundEvent footstepRock;
        [SoundSlot(MaxSimultaneous = 8)] public SoundEvent footstepWater;
        [Tooltip("Jump of every character.")] [SoundSlot(MaxSimultaneous = 4)] public SoundEvent playerJump;
        [Tooltip("Landing of every character (after a real fall).")] [SoundSlot(MaxSimultaneous = 4)] public SoundEvent playerLand;
        [Tooltip("At the hit point of any enemy hit (layered with the enemy's own hurt sound).")]
        [SoundSlot(Cooldown = 0.03f, MaxSimultaneous = 4)] public SoundEvent enemyImpact;
        [SoundSlot(Cooldown = 0.03f, MaxSimultaneous = 3)] public SoundEvent enemyImpactCritical;
        [SoundSlot(Cooldown = 0.05f, MaxSimultaneous = 3)] public SoundEvent enemyKillImpact;
        [SoundSlot(false, Cooldown = 0.15f, MaxSimultaneous = 1)] public SoundEvent playerHurt;
        [SoundSlot(false)] public SoundEvent playerDowned;
        [SoundSlot(false)] public SoundEvent playerRevived;
        [SoundSlot(false)] public SoundEvent playerDeath;
        [SoundSlot(false), Tooltip("Keep it subtle: level ups are meant to be almost transparent.")] public SoundEvent levelUp;

        [Header("Economy & items")]
        [SoundSlot(false, Cooldown = 0.06f, MaxSimultaneous = 2)] public SoundEvent goldGained;
        public SoundEvent purchase;
        [SoundSlot(false)] public SoundEvent itemPickupCommon;
        [SoundSlot(false)] public SoundEvent itemPickupUncommon;
        [SoundSlot(false)] public SoundEvent itemPickupRare;
        [SoundSlot(false)] public SoundEvent itemPickupLegendary;
        [SoundSlot(false)] public SoundEvent itemPickupBoss;

        [Header("Run")]
        [SoundSlot(MaxDistance = 150f)] public SoundEvent teleporterActivated;
        [SoundSlot(MaxDistance = 150f)] public SoundEvent teleporterCharged;
        [SoundSlot(MaxDistance = 150f)] public SoundEvent teleporterReady;
        [SoundSlot(false)] public SoundEvent gameOver;
        [SoundSlot(false)] public SoundEvent victory;

        [Header("Music")]
        public MusicTrack mainMenuMusic = new();
        public MusicTrack stageMusic = new();
        public MusicTrack bossMusic = new();
        [Tooltip("Final stage event (avatar). Empty clip = bossMusic.")] public MusicTrack finalBossMusic = new();
        public MusicTrack victoryMusic = new() { loop = false };
        public MusicTrack gameOverMusic = new() { loop = false };

        /// <summary>Footstep of a surface; empty slot = the generic footstep.</summary>
        public SoundEvent GetFootstep(SurfaceType surface)
        {
            var specific = surface switch
            {
                SurfaceType.Dirt => footstepDirt,
                SurfaceType.Sand => footstepSand,
                SurfaceType.Rock => footstepRock,
                SurfaceType.Water => footstepWater,
                _ => null
            };
            return AudioService.Pick(specific, playerFootstep);
        }

        public SoundEvent GetItemPickup(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Uncommon => itemPickupUncommon,
            ItemRarity.Rare => itemPickupRare,
            ItemRarity.Legendary => itemPickupLegendary,
            ItemRarity.Boss => itemPickupBoss,
            _ => itemPickupCommon
        };
    }
}
