using UnityEngine;
using UnityEngine.InputSystem;

namespace ThisWillFloat.Demo
{
    [RequireComponent(typeof(CharacterController))]
    public class DemoFirstPersonPlayer : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private Vector3 startPosition = new Vector3(0f, 8f, 44f);
        [SerializeField] private Quaternion startRotation = Quaternion.Euler(0f, 180f, 0f);
        [SerializeField] private float moveSpeed = 5.2f;
        [SerializeField] private float sprintSpeed = 7.5f;
        [SerializeField] private float jumpSpeed = 5.8f;
        [SerializeField] private float gravity = -21f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float resetIfBelowY = -8f;

        private CharacterController controller;
        private float pitch;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            if (viewCamera == null)
            {
                viewCamera = GetComponentInChildren<Camera>(true);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Start()
        {
            ResetToStart();
        }

        private void Update()
        {
            UpdateCursorLock();
            Look();
            Move();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                ResetToStart();
            }

            if (transform.position.y < resetIfBelowY)
            {
                ResetToStart();
            }
        }

        public void ResetToStart()
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(startPosition, startRotation);
            SnapToIslandGround();
            pitch = 0f;
            verticalVelocity = 0f;
            controller.enabled = true;
        }

        private void SnapToIslandGround()
        {
            Vector3 rayStart = startPosition + Vector3.up * 80f;
            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 180f, groundMask, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * 0.18f;
            }
        }

        private void UpdateCursorLock()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void Look()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || Cursor.lockState != CursorLockMode.Locked)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(Vector3.up, delta.x * lookSensitivity);
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -72f, 72f);
            if (viewCamera != null)
            {
                viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
        }

        private void Move()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            Vector2 input = Vector2.zero;
            input.y += keyboard.wKey.isPressed ? 1f : 0f;
            input.y -= keyboard.sKey.isPressed ? 1f : 0f;
            input.x += keyboard.dKey.isPressed ? 1f : 0f;
            input.x -= keyboard.aKey.isPressed ? 1f : 0f;
            input = Vector2.ClampMagnitude(input, 1f);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1.5f;
            }

            if (controller.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = jumpSpeed;
            }

            verticalVelocity += gravity * Time.deltaTime;
            float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : moveSpeed;
            Vector3 horizontal = (transform.forward * input.y + transform.right * input.x) * speed;
            controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        private void OnGUI()
        {
            float x = Screen.width * 0.5f;
            float y = Screen.height * 0.5f;
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(x - 9f, y - 1f, 18f, 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x - 1f, y - 9f, 2f, 18f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x - 7f, y, 14f, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y - 7f, 1f, 14f), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
