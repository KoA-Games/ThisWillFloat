using UnityEngine;
using UnityEngine.InputSystem;

namespace ThisWillFloat.Prototype
{
    public class OrbitCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float distance = 9f;
        [SerializeField] private float minDistance = 4f;
        [SerializeField] private float maxDistance = 18f;
        [SerializeField] private float yaw = -35f;
        [SerializeField] private float pitch = 28f;
        [SerializeField] private float minPitch = 8f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float rotateSensitivity = 0.18f;
        [SerializeField] private float zoomSensitivity = 0.025f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.8f, 0f);

        private void LateUpdate()
        {
            ReadInput();
            UpdateCameraPose();
        }

        private void ReadInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.isPressed || mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                yaw += delta.x * rotateSensitivity;
                pitch -= delta.y * rotateSensitivity;
            }

            float scroll = mouse.scroll.ReadValue().y;
            distance -= scroll * zoomSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
        }

        private void UpdateCameraPose()
        {
            Vector3 focus = target != null ? target.position + targetOffset : targetOffset;
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focus + rotation * new Vector3(0f, 0f, -distance);
            transform.rotation = rotation;
        }
    }
}
