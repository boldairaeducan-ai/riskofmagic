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

        public MusicTrack Track
        {
            get
            {
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
