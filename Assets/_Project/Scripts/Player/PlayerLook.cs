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
        public Camera Camera => playerCamera;
        public float Pitch => pitch;
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;
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
        }

        public void SetLocal(bool local)
        {
            isLocal = local;
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
            if (input.ToggleCursorPressed)
                SetCursorLocked(!IsCursorLocked);
            if (!IsCursorLocked)
                return;

            Vector2 delta = input.LookPointerDelta * mouseSensitivity + input.LookStick * (stickSensitivity * Time.deltaTime);
            transform.Rotate(0f, delta.x, 0f, Space.Self);
            pitch = Mathf.Clamp(pitch + (invertY ? delta.y : -delta.y), minPitch, maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void OnDestroy()
        {
            if (isLocal)
                SetCursorLocked(false);
        }
    }
}
