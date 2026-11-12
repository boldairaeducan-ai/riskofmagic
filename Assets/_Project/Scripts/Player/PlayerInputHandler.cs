using RiskOfMagic.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// Builds the player action map in code (no asset setup required). Bindings use physical key
    /// positions, so WASD maps to ZQSD on AZERTY keyboards automatically.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        private InputActionMap map;
        private InputAction move;
        private InputAction lookPointer;
        private InputAction lookStick;
        private InputAction jump;
        private InputAction sprint;
        private InputAction toggleCursor;
        private InputAction interact;
        private InputAction equipment;
        private InputAction[] skills;

        /// <summary>Exposed for a future rebinding UI.</summary>
        public InputActionMap Actions => map;

        public Vector2 Move => map.enabled ? move.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Pixels this frame.</summary>
        public Vector2 LookPointerDelta => map.enabled ? lookPointer.ReadValue<Vector2>() : Vector2.zero;
        /// <summary>Normalized stick, to be scaled by deltaTime.</summary>
        public Vector2 LookStick => map.enabled ? lookStick.ReadValue<Vector2>() : Vector2.zero;
        public bool JumpPressed => jump.WasPressedThisFrame();
        public bool SprintPressed => sprint.WasPressedThisFrame();
        public bool ToggleCursorPressed => toggleCursor.WasPressedThisFrame();
        public bool InteractPressed => interact.WasPressedThisFrame();
        public bool InteractHeld => interact.IsPressed();
        public bool EquipmentPressed => equipment.WasPressedThisFrame();

        public bool IsSkillHeld(SkillSlot slot) => skills[(int)slot].IsPressed();
        public bool WasSkillPressed(SkillSlot slot) => skills[(int)slot].WasPressedThisFrame();
        public bool WasSkillReleased(SkillSlot slot) => skills[(int)slot].WasReleasedThisFrame();

        private void Awake()
        {
#if !ENABLE_INPUT_SYSTEM
            Debug.LogError("[PlayerInputHandler] The Input System backend is disabled: set Project Settings > Player > Active Input Handling to 'Input System Package (New)' or 'Both'.", this);
#endif
            map = new InputActionMap("Player");

            move = map.AddAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            move.AddBinding("<Gamepad>/leftStick");

            lookPointer = map.AddAction("LookPointer", InputActionType.Value, "<Mouse>/delta");
            lookStick = map.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick");

            jump = AddButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            sprint = AddButton("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");
            toggleCursor = AddButton("ToggleCursor", "<Keyboard>/escape", "<Gamepad>/start");
            interact = AddButton("Interact", "<Keyboard>/e", "<Gamepad>/buttonWest");
            equipment = AddButton("Equipment", "<Keyboard>/f", "<Gamepad>/dpad/up");

            skills = new[]
            {
                AddButton("Primary", "<Mouse>/leftButton", "<Gamepad>/rightTrigger"),
                AddButton("Secondary", "<Mouse>/rightButton", "<Gamepad>/leftTrigger"),
                AddButton("Utility", "<Keyboard>/q", "<Gamepad>/leftShoulder"), // physical key: A on AZERTY
                AddButton("Special", "<Keyboard>/r", "<Gamepad>/rightShoulder")
            };

            enabled = false;
        }

        /// <summary>Keyboard / mouse key of a skill slot, in the player's keyboard layout ("A" on AZERTY for Q).</summary>
        public string GetSkillKeyLabel(int slot)
        {
            if (skills == null || slot < 0 || slot >= skills.Length)
                return string.Empty;
            if (slot == 0)
                return "Clic G";
            if (slot == 1)
                return "Clic D";
            string label = skills[slot].GetBindingDisplayString(0);
            return string.IsNullOrEmpty(label) ? string.Empty : label.ToUpperInvariant();
        }

        private InputAction AddButton(string actionName, params string[] bindings)
        {
            var action = map.AddAction(actionName, InputActionType.Button);
            foreach (var binding in bindings)
                action.AddBinding(binding);
            return action;
        }

        private void OnEnable() => map?.Enable();
        private void OnDisable() => map?.Disable();
        private void OnDestroy() => map?.Dispose();
    }
}
