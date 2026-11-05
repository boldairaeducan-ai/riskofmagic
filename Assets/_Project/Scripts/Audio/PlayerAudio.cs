using RiskOfMagic.Player;
using UnityEngine;

namespace RiskOfMagic.Audio
{
    /// <summary>
    /// Character body sounds on every peer (footsteps, jump, landing) from the CharacterDefinition slots.
    /// Remote players are detected from their replicated movement.
    /// </summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerAudio : MonoBehaviour
    {
        [SerializeField, Min(0.3f), Tooltip("Metres walked between two footsteps.")] private float strideLength = 1.9f;
        [SerializeField, Tooltip("Sprint stride multiplier.")] private float sprintStrideMultiplier = 1.25f;
        [SerializeField] private float groundProbe = 0.25f;
        [SerializeField] private LayerMask groundMask = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0f), Tooltip("Minimum vertical speed to play the landing sound.")] private float landingSpeed = 4f;
        [SerializeField, Tooltip("Off when footsteps come from animation events (AnimationEventRelay.Footstep).")]
        private bool distanceFootsteps = true;

        private PlayerAvatar avatar;
        private Vector3 lastPosition;
        private float strideProgress;
        private bool wasGrounded = true;
        private float verticalSpeed;

        private void Awake() => avatar = GetComponent<PlayerAvatar>();

        private void OnEnable()
        {
            lastPosition = transform.position;
            strideProgress = 0f;
        }

        private void Update()
        {
            var character = avatar.Character;
            Vector3 position = transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            if (delta.sqrMagnitude > 25f || character == null || !avatar.IsStanding)
            {
                strideProgress = 0f; // teleport / downed
                return;
            }

            bool grounded = avatar.IsOwner && avatar.Motor != null
                ? avatar.Motor.IsGrounded
                : Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down, 0.1f + groundProbe, groundMask, QueryTriggerInteraction.Ignore);

            if (grounded && !wasGrounded && -verticalSpeed >= landingSpeed)
                AudioService.PlayAt(character.landSound, position);
            else if (!grounded && wasGrounded && delta.y / dt > 1.5f)
                AudioService.PlayAt(character.jumpSound, position);

            verticalSpeed = delta.y / dt;
            wasGrounded = grounded;
            if (!grounded)
                return;

            delta.y = 0f;
            bool sprinting = avatar.Motor != null && avatar.Motor.IsSprinting;
            strideProgress += delta.magnitude;
            float stride = strideLength * (sprinting ? sprintStrideMultiplier : 1f);
            if (distanceFootsteps && strideProgress >= stride)
            {
                strideProgress -= stride;
                PlayFootstep();
            }
        }

        /// <summary>Also called by animation events (AnimationEventRelay).</summary>
        public void PlayFootstep()
        {
            if (avatar.Character != null)
                AudioService.PlayAt(avatar.Character.footstepSound, transform.position);
        }
    }
}
