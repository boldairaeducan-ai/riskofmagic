using RiskOfMagic.Feedback;
using RiskOfMagic.Settings;
using UnityEngine;

namespace RiskOfMagic.Player
{
    /// <summary>
    /// First person look: yaw on the root, pitch on the camera pivot. Runs before the motor.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PlayerLook : MonoBehaviour
    {
        [SerializeField] private PlayerInputHandler input;
        [SerializeField] private Transform cameraPivot;
        [SerializeField] private Camera playerCamera;

        [Header("Sensitivity")]
        [SerializeField, Tooltip("Degrees per pixel")] private float mouseSensitivity = 0.1f;
        [SerializeField, Tooltip("Degrees per second")] private float stickSensitivity = 200f;
        [SerializeField] private bool invertY;

        [Header("Limits")]
        [SerializeField] private float minPitch = -85f;
        [SerializeField] private float maxPitch = 85f;

        private float pitch;
        private bool isLocal;

        public Transform CameraPivot => cameraPivot;
        /// <summary>Owner only (not replicated): vertical aim angle, negative = up.</summary>
        public float Pitch => pitch;
        public Camera Camera => playerCamera;
        /// <summary>Shake and FOV kicks (local player only; null if the camera has no CameraEffects).</summary>
        public CameraEffects Effects { get; private set; }
        public float Pitch => pitch;
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;
        /// <summary>Set by the pause menu: Esc opens it instead of just freeing the cursor.</summary>
        public static bool EscapeHandledExternally { get; set; }
        public Ray AimRay => new(cameraPivot.position, cameraPivot.forward);

        public float MouseSensitivity
        {
            get => mouseSensitivity;
            set => mouseSensitivity = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            if (input == null)
                input = GetComponent<PlayerInputHandler>();
            if (playerCamera == null)
                playerCamera = GetComponentInChildren<Camera>(true);
            if (cameraPivot == null && playerCamera != null)
                cameraPivot = playerCamera.transform.parent;
            if (playerCamera != null)
                Effects = playerCamera.GetComponent<CameraEffects>();
        }

        public void SetLocal(bool local)
        {
            isLocal = local;
            GameSettings.Changed -= ApplySettings;
            if (local)
            {
                GameSettings.Changed += ApplySettings;
                ApplySettings();
            }
            if (playerCamera != null)
            {
                playerCamera.enabled = local;
                if (playerCamera.TryGetComponent(out AudioListener listener))
                    listener.enabled = local;
            }
            if (local)
                SetCursorLocked(true);
        }

        public void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void Update()
        {
            if (!isLocal)
                return;
            if (input.ToggleCursorPressed && !EscapeHandledExternally)
                SetCursorLocked(!IsCursorLocked);
            if (!IsCursorLocked)
                return;

            Vector2 delta = input.LookPointerDelta * (mouseSensitivity * GameSettings.MouseSensitivity)
                            + input.LookStick * (stickSensitivity * GameSettings.StickSensitivity * Time.deltaTime);
            transform.Rotate(0f, delta.x, 0f, Space.Self);
            bool invert = invertY != GameSettings.InvertY;
            pitch = Mathf.Clamp(pitch + (invert ? delta.y : -delta.y), minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void ApplySettings()
        {
            if (Effects != null)
                Effects.BaseFieldOfView = GameSettings.FieldOfView;
            else if (playerCamera != null)
                playerCamera.fieldOfView = GameSettings.FieldOfView;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplySettings;
            if (isLocal)
                SetCursorLocked(false);
        }
    }
}
