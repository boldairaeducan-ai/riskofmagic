using UnityEngine;

namespace RiskOfMagic.Audio
{
    public enum SoundCategory : byte
    {
        Effects,
        UI,
        Music
    }

    /// <summary>
    /// A playable sound: one random clip among several, randomized volume/pitch, 2D or 3D, with anti-spam limits.
    /// This is the asset slot used everywhere (skills, projectiles, enemies, items, UI...).
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Audio/Sound Event", fileName = "Sound")]
    public class SoundEvent : ScriptableObject
    {
        public AudioClip[] clips = new AudioClip[0];
        public SoundCategory category = SoundCategory.Effects;
        public Vector2 volume = new(0.9f, 1f);
        public Vector2 pitch = new(0.95f, 1.05f);

        [Header("3D")]
        [Tooltip("Off = 2D (UI, local feedback).")] public bool spatial = true;
        [Min(0.1f)] public float minDistance = 2f;
        [Min(1f)] public float maxDistance = 40f;

        [Header("Limits")]
        [Min(0f), Tooltip("Minimum time between two plays.")] public float cooldown;
        [Min(0), Tooltip("0 = unlimited simultaneous voices.")] public int maxSimultaneous = 6;

        public bool HasClips => clips != null && clips.Length > 0;

        public AudioClip PickClip() => HasClips ? clips[Random.Range(0, clips.Length)] : null;
    }
}
