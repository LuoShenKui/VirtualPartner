using UnityEngine;

namespace VRDemo.Core
{
    /// <summary>
    /// 简单第一人称控制器，让玩家以男主视角在房间中移动和观察。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CompanionFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float moveSpeed = 2.6f;
        [SerializeField] private float lookSensitivity = 2.4f;
        [SerializeField] private float eyeHeight = 1.48f;
        [SerializeField] private float groundHeight = 0.08f;

        private CharacterController characterController;
        private float pitch;
        private bool movementEnabled = true;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            LockCursor(true);
        }

        private void OnDisable()
        {
            LockCursor(false);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            HandleCursorToggle();
            if (movementEnabled)
            {
                HandleLook();
            }
            if (movementEnabled)
            {
                HandleMovement();
            }
            else
            {
                SnapToGround();
            }
        }

        public void BindCamera(Camera targetCamera)
        {
            playerCamera = targetCamera;
            if (playerCamera != null)
            {
                playerCamera.transform.SetParent(transform, false);
                playerCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
                playerCamera.transform.localRotation = Quaternion.identity;
                pitch = playerCamera.transform.localEulerAngles.x;
            }
        }

        public void SetEyeHeight(float value)
        {
            eyeHeight = value;
            if (playerCamera != null)
            {
                playerCamera.transform.localPosition = new Vector3(0f, eyeHeight, 0f);
            }
        }

        public void SetGroundHeight(float value)
        {
            groundHeight = value;
            SnapToGround();
        }

        public void SetMovementEnabled(bool value)
        {
            movementEnabled = value;
        }

        private void HandleCursorToggle()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                LockCursor(Cursor.lockState != CursorLockMode.Locked);
            }
        }

        private void HandleLook()
        {
            if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            var yaw = Input.GetAxis("Mouse X") * lookSensitivity;
            var lookY = Input.GetAxis("Mouse Y") * lookSensitivity;

            transform.Rotate(Vector3.up * yaw);
            pitch = Mathf.Clamp(pitch - lookY, -35f, 45f);
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        private void HandleMovement()
        {
            SnapToGround();

            var moveInput = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            moveInput = Vector3.ClampMagnitude(moveInput, 1f);

            var move = (transform.right * moveInput.x + transform.forward * moveInput.z) * moveSpeed;
            move.y = 0f;
            characterController.Move(move * Time.deltaTime);
            SnapToGround();
        }

        private void SnapToGround()
        {
            if (characterController == null)
            {
                return;
            }

            if (Mathf.Abs(transform.position.y - groundHeight) < 0.001f)
            {
                return;
            }

            characterController.enabled = false;
            transform.position = new Vector3(transform.position.x, groundHeight, transform.position.z);
            characterController.enabled = true;
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
