using UnityEngine;
using UnityEngine.InputSystem;

namespace ThisWillFloat.Prototype
{
    [RequireComponent(typeof(CharacterController))]
    public class DemoPlayerController : MonoBehaviour
    {
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Transform holdPoint;
        [SerializeField] private Vector3 cameraLocalPosition = new Vector3(0f, 1.55f, 0f);
        [SerializeField] private float moveSpeed = 4.8f;
        [SerializeField] private float gravity = -18f;
        [SerializeField] private float jumpSpeed = 6f;
        [SerializeField] private float lookSensitivity = 0.12f;
        [SerializeField] private float interactionRadius = 2.0f;
        [SerializeField] private float waterLevel = 0f;
        [SerializeField] private float waterSinkDepth = -2.6f;
        [SerializeField] private float waterSinkSpeed = 0.55f;
        [SerializeField] private bool showThirdPersonModel;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private DemoGameController gameController;

        private CharacterController characterController;
        private DemoCarryableItem heldItem;
        private float verticalVelocity;
        private float cameraPitch;
        private float spawnProtectionTimer;
        private Vector3 protectedSpawnPosition;
        private bool inWater;

        public DemoCarryableItem HeldItem => heldItem;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            SetPlayerModelVisible(showThirdPersonModel);
            ResolveFirstPersonCamera();
            AttachCameraToPlayer();
        }

        private void Update()
        {
            UpdateCursorLock();
            KeepInitialSpawnDry();
            Look();
            Move();

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            bool interactPressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            bool clickPressed = mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState == CursorLockMode.Locked;
            if (interactPressed || clickPressed)
            {
                Interact();
            }
        }

        public void ResetPlayer(Vector3 worldPosition, Quaternion worldRotation)
        {
            if (heldItem != null)
            {
                heldItem.ResetItem();
                heldItem = null;
            }

            characterController.enabled = false;
            transform.SetParent(null);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            verticalVelocity = 0f;
            inWater = false;
            cameraPitch = 0f;
            spawnProtectionTimer = 2f;
            protectedSpawnPosition = worldPosition;
            if (viewCamera != null)
            {
                AttachCameraToPlayer();
            }
            characterController.enabled = true;
        }

        public void SnapToMovingParent(Transform parent, Vector3 worldPosition, Quaternion worldRotation)
        {
            characterController.enabled = false;
            transform.SetParent(parent, true);
            transform.SetPositionAndRotation(worldPosition, worldRotation);
            verticalVelocity = 0f;
            inWater = false;
            AttachCameraToPlayer();
            characterController.enabled = true;
        }

        private void LateUpdate()
        {
            AttachCameraToPlayer();
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

            Vector3 forward = viewCamera != null ? viewCamera.transform.forward : transform.forward;
            Vector3 right = viewCamera != null ? viewCamera.transform.right : transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 move = forward * input.y + right * input.x;
            if (UpdateWaterState(move))
            {
                return;
            }

            if (characterController.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -1f;
            }

            if (characterController.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = jumpSpeed;
            }

            verticalVelocity += gravity * Time.deltaTime;
            Vector3 velocity = move * moveSpeed + Vector3.up * verticalVelocity;
            characterController.Move(velocity * Time.deltaTime);
        }

        private bool UpdateWaterState(Vector3 horizontalMove)
        {
            if (spawnProtectionTimer > 0f)
            {
                return false;
            }

            if (!inWater && transform.position.y > waterLevel + 0.08f)
            {
                return false;
            }

            if (characterController.isGrounded && transform.position.y > waterLevel - 0.15f)
            {
                inWater = false;
                return false;
            }

            inWater = true;
            float targetY = waterSinkDepth;
            float nextY = Mathf.MoveTowards(transform.position.y, targetY, waterSinkSpeed * Time.deltaTime);
            Vector3 velocity = horizontalMove * (moveSpeed * 0.42f);
            velocity.y = (nextY - transform.position.y) / Mathf.Max(Time.deltaTime, 0.0001f);
            characterController.Move(velocity * Time.deltaTime);
            verticalVelocity = 0f;
            return true;
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
            cameraPitch = Mathf.Clamp(cameraPitch - delta.y * lookSensitivity, -72f, 72f);
        }

        private void AttachCameraToPlayer()
        {
            ResolveFirstPersonCamera();

            if (viewCamera == null)
            {
                return;
            }

            OrbitCameraController orbitCamera = viewCamera.GetComponent<OrbitCameraController>();
            if (orbitCamera != null)
            {
                orbitCamera.enabled = false;
            }

            viewCamera.transform.SetParent(transform, false);
            viewCamera.transform.localPosition = cameraLocalPosition;
            viewCamera.transform.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            viewCamera.fieldOfView = 68f;
        }

        private void UpdateCursorLock()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
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

        private void KeepInitialSpawnDry()
        {
            if (spawnProtectionTimer <= 0f)
            {
                return;
            }

            spawnProtectionTimer -= Time.deltaTime;
            if (transform.position.y >= protectedSpawnPosition.y - 0.05f)
            {
                return;
            }

            characterController.enabled = false;
            transform.position = protectedSpawnPosition;
            characterController.enabled = true;
            verticalVelocity = 0f;
            inWater = false;
        }

        private void ResolveFirstPersonCamera()
        {
            Camera childCamera = GetComponentInChildren<Camera>(true);
            if (childCamera != null)
            {
                viewCamera = childCamera;
            }

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }
        }

        private void SetPlayerModelVisible(bool visible)
        {
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<Camera>() == null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private void Interact()
        {
            if (heldItem != null)
            {
                DemoRaftSlot slot = FindTarget<DemoRaftSlot>();
                if (slot != null && slot.Place(heldItem))
                {
                    heldItem = null;
                    return;
                }

                heldItem.Drop(holdPoint.position + transform.forward * 0.55f, transform.rotation);
                heldItem = null;
                return;
            }

            DemoLaunchButton launchButton = FindTarget<DemoLaunchButton>();
            if (launchButton != null)
            {
                launchButton.Interact();
                return;
            }

            DemoCarryableItem item = FindTarget<DemoCarryableItem>();
            if (item != null && !item.IsPlaced && !item.IsHeld)
            {
                heldItem = item;
                item.PickUp(holdPoint);
                gameController?.SetPrompt($"Carrying: {item.DisplayName}");
            }
        }

        private T FindTarget<T>() where T : Component
        {
            T aimed = FindAimed<T>();
            return aimed != null ? aimed : FindNearest<T>();
        }

        private T FindAimed<T>() where T : Component
        {
            if (viewCamera == null)
            {
                return null;
            }

            Ray ray = new Ray(viewCamera.transform.position, viewCamera.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionRadius * 2.4f, interactionMask, QueryTriggerInteraction.Collide))
            {
                return null;
            }

            return hit.collider.GetComponentInParent<T>();
        }

        private T FindNearest<T>() where T : Component
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, interactionRadius, interactionMask, QueryTriggerInteraction.Collide);
            T nearest = null;
            float nearestDistance = float.PositiveInfinity;

            foreach (Collider hit in hits)
            {
                T candidate = hit.GetComponentInParent<T>();
                if (candidate == null)
                {
                    continue;
                }

                float distance = Vector3.SqrMagnitude(candidate.transform.position - transform.position);
                if (distance < nearestDistance)
                {
                    nearest = candidate;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }
    }
}
