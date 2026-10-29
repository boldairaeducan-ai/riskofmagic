using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Owner-simulated CharacterController movement (pair with OwnerNetworkTransform).
    /// Sprint is a toggle cancelled when not moving forward, Risk of Rain style.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : NetworkBehaviour
    {
        [SerializeField] private MovementConfig config;
        [SerializeField] private PlayerInputHandler input;

        private CharacterController controller;
        private NetworkTransform networkTransform;
        private Vector3 velocity;
        private Vector3 externalVelocity;
        private float lastGroundedTime = float.NegativeInfinity;
        private float lastJumpPressedTime = float.NegativeInfinity;
        private int jumpsUsed;
        private Vector3 dashVelocity;
        private float dashEndTime = float.NegativeInfinity;
        private bool dashIgnoresGravity;
        private bool warnedMissingConfig;

        public event Action Jumped;
        public event Action Landed;

        public MovementConfig Config => config;
        public bool ControlEnabled { get; set; } = true;
        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsDashing => Time.time < dashEndTime;
        public Vector3 Velocity => velocity + externalVelocity;
        /// <summary>Multiplicative modifier for buffs, items, skills.</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        public void SetConfig(MovementConfig newConfig) => config = newConfig;

        public void AddImpulse(Vector3 impulse) => externalVelocity += impulse;

        public void SetVerticalVelocity(float value) => velocity.y = value;

        public void CancelSprint() => IsSprinting = false;

        /// <summary>Overrides movement with a fixed velocity for a duration (rolls, charges).</summary>
        public void Dash(Vector3 dashVelocityValue, float duration, bool ignoreGravity)
        {
            dashVelocity = dashVelocityValue;
            dashEndTime = Time.time + duration;
            dashIgnoresGravity = ignoreGravity;
            velocity.y = dashVelocityValue.y;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            if (networkTransform != null && IsSpawned && networkTransform.CanCommitToTransform)
                networkTransform.Teleport(position, rotation, transform.localScale);
            else
                transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            velocity = Vector3.zero;
            externalVelocity = Vector3.zero;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            networkTransform = GetComponent<NetworkTransform>();
            if (input == null)
                input = GetComponent<PlayerInputHandler>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner)
                return;
            if (config == null)
            {
                if (!warnedMissingConfig)
                    Debug.LogError($"[PlayerMotor] No MovementConfig on {name} (character not applied? check Resources/GameCatalog).", this);
                warnedMissingConfig = true;
                return;
            }
            Tick(Time.deltaTime);
        }

        private void Tick(float dt)
        {
            Vector2 moveInput = ControlEnabled ? input.Move : Vector2.zero;
            bool jumpPressed = ControlEnabled && input.JumpPressed;

            if (ControlEnabled && input.SprintPressed)
                IsSprinting = !IsSprinting;
            if (moveInput.y <= config.sprintForwardThreshold)
                IsSprinting = false;

            bool wasGrounded = IsGrounded;
            IsGrounded = controller.isGrounded;
            if (IsGrounded && !wasGrounded)
                Landed?.Invoke();

            // Horizontal
            float speed = config.walkSpeed * SpeedMultiplier * (IsSprinting ? config.sprintMultiplier : 1f);
            Vector3 wish = transform.right * moveInput.x + transform.forward * moveInput.y;
            wish = Vector3.ClampMagnitude(wish, 1f) * speed;
            Vector3 horizontal = new(velocity.x, 0f, velocity.z);
            float acceleration = IsGrounded ? config.groundAcceleration : config.airAcceleration;
            horizontal = Vector3.MoveTowards(horizontal, wish, acceleration * dt);

            // Vertical
            if (IsGrounded && velocity.y <= 0f)
            {
                velocity.y = -config.groundStickForce;
                lastGroundedTime = Time.time;
                jumpsUsed = 0;
            }
            else if (jumpsUsed == 0 && Time.time - lastGroundedTime > config.coyoteTime)
            {
                jumpsUsed = 1; // walked off a ledge: the ground jump is lost
            }

            if (jumpPressed)
                lastJumpPressedTime = Time.time;

            if (Time.time - lastJumpPressedTime <= config.jumpBufferTime && jumpsUsed < config.maxJumps)
            {
                velocity.y = Mathf.Sqrt(2f * config.gravity * config.jumpHeight);
                jumpsUsed++;
                lastJumpPressedTime = float.NegativeInfinity;
                lastGroundedTime = float.NegativeInfinity;
                Jumped?.Invoke();
            }

            velocity.y = Mathf.Max(velocity.y - config.gravity * dt, -config.maxFallSpeed);
            velocity.x = horizontal.x;
            velocity.z = horizontal.z;

            externalVelocity = Vector3.MoveTowards(externalVelocity, Vector3.zero, config.externalDrag * dt * Mathf.Max(1f, externalVelocity.magnitude));

            if (IsDashing)
            {
                velocity.x = dashVelocity.x;
                velocity.z = dashVelocity.z;
                if (dashIgnoresGravity)
                    velocity.y = dashVelocity.y;
            }

            CollisionFlags flags = controller.Move((velocity + externalVelocity) * dt);
            if ((flags & CollisionFlags.Above) != 0 && velocity.y > 0f)
                velocity.y = 0f;
        }
    }
}
