using RiskOfMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Reads the shared action map (GameInput) for the local player: enabled on the owner only.
    /// Bindings can be changed at runtime from the options (Raccourcis).
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        private InputActionMap map;

        public InputActionMap Actions => map;

        public Vector2 Move => map.enabled ? GameInput.Move.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Pixels this frame.</summary>
        public Vector2 LookPointerDelta => map.enabled ? GameInput.LookPointer.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Normalized stick, to be scaled by deltaTime.</summary>
        public Vector2 LookStick => map.enabled ? GameInput.LookStick.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressed => enabled && GameInput.Jump.WasPressedThisFrame();
        public bool SprintPressed => enabled && GameInput.Sprint.WasPressedThisFrame();
        public bool ToggleCursorPressed => enabled && !GameInput.MenuKeysBlocked && GameInput.ToggleCursor.WasPressedThisFrame();
        public bool InteractPressed => enabled && GameInput.Interact.WasPressedThisFrame();
        public bool InteractHeld => enabled && GameInput.Interact.IsPressed();
        public bool EquipmentPressed => enabled && GameInput.Equipment.WasPressedThisFrame();

        public bool IsSkillHeld(SkillSlot slot) => enabled && GameInput.Skills[(int)slot].IsPressed();
        public bool WasSkillPressed(SkillSlot slot) => enabled && GameInput.Skills[(int)slot].WasPressedThisFrame();
        public bool WasSkillReleased(SkillSlot slot) => enabled && GameInput.Skills[(int)slot].WasReleasedThisFrame();

        private void Awake()
        {
#if !ENABLE_INPUT_SYSTEM
            Debug.LogError("[PlayerInputHandler] The Input System backend is disabled: set Project Settings > Player > Active Input Handling to 'Input System Package (New)' or 'Both'.", this);
#endif
            map = GameInput.Map;
            enabled = false;
        }

        /// <summary>Keyboard / mouse key of a skill slot, in the player's keyboard layout ("A" on AZERTY for Q).</summary>
        public string GetSkillKeyLabel(int slot) => GameInput.SkillKey(slot);

        // Only the local player's handler is enabled: it owns the shared map's enabled state.
        private void OnEnable() => map?.Enable();
        private void OnDisable() => map?.Disable();
    }
}
