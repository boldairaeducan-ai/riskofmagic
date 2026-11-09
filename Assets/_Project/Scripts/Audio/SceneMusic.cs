using UnityEngine;

namespace RiskOfMagic.Audio
{
    public enum MusicSlot : byte
    {
        MainMenu,
        Stage,
        Custom
    }

    /// <summary>
    /// Optional, one per scene: picks the base music. Scenes without it play the stage music,
    /// except the main menu (MainMenuController present) which plays the menu music.
    /// </summary>
    public class SceneMusic : MonoBehaviour
    {
        [SerializeField] private MusicSlot slot = MusicSlot.Stage;
        [SerializeField] private MusicTrack customTrack = new();
        [SerializeField, Tooltip("Map music asset (generated per map). Used first when it has a clip.")] private MusicAsset music;

        public static SceneMusic Current { get; private set; }

        /// <summary>This map's teleporter event music, null = AudioConfig default.</summary>
        public MusicTrack BossTrack => music != null && music.bossTrack != null && music.bossTrack.clip != null ? music.bossTrack : null;

        private void OnEnable() => Current = this;

        private void OnDisable()
        {
            if (Current == this)
                Current = null;
        }

        public MusicTrack Track
        {
            get
            {
                if (music != null && music.HasClip)
                    return music.track;
                var config = AudioService.Config;
                return slot switch
                {
                    MusicSlot.Custom => customTrack,
                    MusicSlot.MainMenu => config != null ? config.mainMenuMusic : null,
                    _ => config != null ? config.stageMusic : null
                };
            }
        }
    }
}
