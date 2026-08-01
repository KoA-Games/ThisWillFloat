using UnityEngine;
using UnityEngine.InputSystem;

namespace ThisWillFloat.Prototype
{
    [RequireComponent(typeof(Rigidbody))]
    public class SimpleSail : MonoBehaviour
    {
        [SerializeField] private Vector3 windDirection = new Vector3(0.6f, 0f, 1f);
        [SerializeField] private float windStrength = 3.5f;
        [SerializeField] private Transform sailSurface;
        [SerializeField] private float turnSpeed = 45f;

        private Rigidbody cachedRigidbody;

        private void Awake()
        {
            cachedRigidbody = GetComponent<Rigidbody>();
            if (sailSurface == null)
            {
                sailSurface = transform;
            }
        }

        private void Update()
        {
            float input = 0f;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.qKey.isPressed)
            {
                input -= 1f;
            }

            if (keyboard.eKey.isPressed)
            {
                input += 1f;
            }

            if (Mathf.Abs(input) > 0f)
            {
                sailSurface.Rotate(Vector3.up, input * turnSpeed * Time.deltaTime, Space.World);
            }
        }

        private void FixedUpdate()
        {
            Vector3 normalizedWind = windDirection.normalized;
            float sailFacing = Mathf.Abs(Vector3.Dot(sailSurface.forward, normalizedWind));
            Vector3 force = normalizedWind * (windStrength * sailFacing);
            cachedRigidbody.AddForceAtPosition(force, sailSurface.position, ForceMode.Acceleration);
        }
    }
}
