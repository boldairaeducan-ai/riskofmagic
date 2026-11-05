using RiskOfMagic.Audio;
using RiskOfMagic.Vfx;
using UnityEngine;

namespace RiskOfMagic.Animation
{
    /// <summary>
    /// Put on the GameObject that holds the Animator to call these from animation events (cosmetic, local):
    /// PlaySound(SoundEvent), PlayVfx(GameObject prefab), Footstep() (character footstep sound; then turn off
    /// PlayerAudio.distanceFootsteps).
    /// </summary>
    public class AnimationEventRelay : MonoBehaviour
    {
        [SerializeField, Tooltip("Optional: where PlayVfx spawns (default: this transform).")] private Transform vfxAnchor;

        public void PlaySound(Object sound)
        {
            if (sound is SoundEvent soundEvent)
                AudioService.PlayAt(soundEvent, transform.position);
        }

        public void PlayVfx(Object prefab)
        {
            if (prefab is GameObject go)
            {
                var anchor = vfxAnchor != null ? vfxAnchor : transform;
                VfxUtility.Spawn(go, anchor.position, anchor.rotation);
            }
        }

        public void Footstep()
        {
            var playerAudio = GetComponentInParent<PlayerAudio>();
            if (playerAudio != null)
                playerAudio.PlayFootstep();
        }
    }
}
