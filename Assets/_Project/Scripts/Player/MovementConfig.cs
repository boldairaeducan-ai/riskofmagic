using UnityEngine;

namespace RiskOfMagic.Player
{
    [CreateAssetMenu(menuName = "Risk of Magic/Movement Config", fileName = "MovementConfig")]
    public class MovementConfig : ScriptableObject
    {
        [Header("Ground")]
        [Min(0f)] public float walkSpeed = 7f;
        [Min(1f)] public float sprintMultiplier = 1.45f;
        [Min(0f)] public float groundAcceleration = 80f;

        [Header("Air")]
        [Min(0f)] public float airAcceleration = 25f;
        [Min(0f)] public float gravity = 30f;
        [Min(0f)] public float maxFallSpeed = 50f;

        [Header("Jump")]
        [Min(0f)] public float jumpHeight = 1.6f;
        [Min(1)] public int maxJumps = 1;
        [Min(0f)] public float coyoteTime = 0.12f;
        [Min(0f)] public float jumpBufferTime = 0.12f;

        [Header("External forces (dash, knockback)")]
        [Min(0f)] public float externalDrag = 8f;

        [Header("Misc")]
        [Min(0f)] public float groundStickForce = 2f;
        [Range(0f, 1f)] public float sprintForwardThreshold = 0.1f;
    }
}
