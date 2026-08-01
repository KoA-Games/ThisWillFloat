using UnityEngine;

namespace ThisWillFloat.Prototype
{
    public class WaveSurface : MonoBehaviour
    {
        [SerializeField] private float baseWaterHeight = 0f;
        [SerializeField] private float amplitude = 0.16f;
        [SerializeField] private float wavelength = 8.5f;
        [SerializeField] private float speed = 0.75f;
        [SerializeField] private float crossAmplitude = 0.06f;
        [SerializeField] private float crossWavelength = 5.5f;
        [SerializeField] private float crossSpeed = 1.1f;
        [SerializeField] private float chopAmplitude = 0.018f;
        [SerializeField] private float chopWavelength = 2.4f;
        [SerializeField] private float chopSpeed = 1.6f;

        public float BaseWaterHeight => transform.position.y + baseWaterHeight;

        public float GetHeight(Vector3 worldPosition)
        {
            float time = Time.time;
            float primary = Mathf.Sin((worldPosition.x / wavelength + time * speed) * Mathf.PI * 2f) * amplitude;
            float cross = Mathf.Sin((worldPosition.z / crossWavelength + time * crossSpeed) * Mathf.PI * 2f) * crossAmplitude;
            float chop = Mathf.Sin(((worldPosition.x + worldPosition.z) / chopWavelength + time * chopSpeed) * Mathf.PI * 2f) * chopAmplitude;
            return BaseWaterHeight + primary + cross + chop;
        }

        public Vector3 GetNormal(Vector3 worldPosition)
        {
            const float sampleDistance = 0.25f;
            float left = GetHeight(worldPosition + Vector3.left * sampleDistance);
            float right = GetHeight(worldPosition + Vector3.right * sampleDistance);
            float back = GetHeight(worldPosition + Vector3.back * sampleDistance);
            float forward = GetHeight(worldPosition + Vector3.forward * sampleDistance);

            Vector3 tangentX = new Vector3(sampleDistance * 2f, right - left, 0f);
            Vector3 tangentZ = new Vector3(0f, forward - back, sampleDistance * 2f);
            return Vector3.Cross(tangentZ, tangentX).normalized;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.65f, 1f, 0.35f);
            Vector3 center = transform.position + Vector3.up * baseWaterHeight;
            Gizmos.DrawCube(center, new Vector3(20f, 0.02f, 20f));
        }
    }
}
