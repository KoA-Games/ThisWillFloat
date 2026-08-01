using UnityEngine;

namespace ThisWillFloat.Demo
{
    public class ShorelinePulse : MonoBehaviour
    {
        [SerializeField] private float scaleAmount = 0.025f;
        [SerializeField] private float heightAmount = 0.035f;
        [SerializeField] private float speed = 0.55f;

        private Vector3 baseScale;
        private Vector3 basePosition;

        private void Awake()
        {
            baseScale = transform.localScale;
            basePosition = transform.position;
        }

        private void Update()
        {
            float wave = Mathf.Sin(Time.time * Mathf.PI * 2f * speed) * 0.5f + 0.5f;
            transform.localScale = baseScale * (1f + wave * scaleAmount);
            transform.position = basePosition + Vector3.up * (wave * heightAmount);
        }
    }
}
