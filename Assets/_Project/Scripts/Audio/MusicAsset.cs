using UnityEngine;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// A music track stored as an asset (one per map: Audio/Music/&lt;Scene&gt;_Music.asset, created empty by the map
    /// tools and never overwritten). Referenced by the map's SceneMusic; empty clip = the AudioConfig stage music.
    /// </summary>
    [CreateAssetMenu(menuName = "Risk of Magic/Audio/Music", fileName = "Music")]
    public class MusicAsset : ScriptableObject
    {
        public MusicTrack track = new();
        [Tooltip("Optional: played during this map's teleporter event instead of AudioConfig.bossMusic.")]
        public MusicTrack bossTrack = new();

        public bool HasClip => track != null && track.clip != null;
    }
}
