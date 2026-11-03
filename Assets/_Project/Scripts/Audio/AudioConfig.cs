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
        [SoundSlot(false, Cooldown = 0.15f, MaxSimultaneous = 1)] public SoundEvent playerHurt;
        [SoundSlot(false)] public SoundEvent playerDowned;
        [SoundSlot(false)] public SoundEvent playerRevived;
        [SoundSlot(false)] public SoundEvent playerDeath;

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

        [Header("Music")]
        public MusicTrack mainMenuMusic = new();
        public MusicTrack stageMusic = new();
        public MusicTrack bossMusic = new();
        public MusicTrack gameOverMusic = new() { loop = false };

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
