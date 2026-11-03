using UnityEngine;

namespace RiskOfMagic.Audio
{
    /// <summary>Plays a sound each time the (pooled) effect is enabled: explosions, impacts, equipment pulses...</summary>
    public class EffectSound : MonoBehaviour
    {
        [SerializeField] private SoundEvent sound;

        private void OnEnable() => AudioService.PlayAt(sound, transform.position);
    }
}
