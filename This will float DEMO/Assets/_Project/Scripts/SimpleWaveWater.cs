using UnityEngine;

namespace ThisWillFloat.Demo
{
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public class SimpleWaveWater : MonoBehaviour
    {
        [SerializeField] private float amplitude = 0.55f;
        [SerializeField] private float frequency = 0.045f;
        [SerializeField] private float speed = 1.1f;
        [SerializeField] private float crossAmplitude = 0.24f;
        [SerializeField] private float textureScrollSpeed = 0.018f;

        private Mesh mesh;
        private Vector3[] baseVertices;
        private Vector3[] animatedVertices;
        private MeshRenderer meshRenderer;
        private bool ownsRuntimeMesh;

        public void Configure(float newAmplitude, float newFrequency, float newSpeed, float newCrossAmplitude)
        {
            amplitude = newAmplitude;
            frequency = newFrequency;
            speed = newSpeed;
            crossAmplitude = newCrossAmplitude;
            CacheMesh();
        }

        private void OnEnable()
        {
            CacheMesh();
        }

        private void Update()
        {
            if (mesh == null || baseVertices == null)
            {
                CacheMesh();
            }

            if (mesh == null || baseVertices == null)
            {
                return;
            }

            float time = Time.time;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                time = (float)UnityEditor.EditorApplication.timeSinceStartup;
            }
#endif
            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 v = baseVertices[i];
                float waveA = Mathf.Sin((v.x * frequency) + time * speed) * amplitude;
                float waveB = Mathf.Cos((v.z * frequency * 1.35f) + time * speed * 0.7f) * crossAmplitude;
                animatedVertices[i] = new Vector3(v.x, waveA + waveB, v.z);
            }

            mesh.vertices = animatedVertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                Vector2 offset = new Vector2(time * textureScrollSpeed, time * textureScrollSpeed * 0.63f);
                if (meshRenderer.sharedMaterial.HasProperty("_BaseMap"))
                {
                    meshRenderer.sharedMaterial.SetTextureOffset("_BaseMap", offset);
                }

                if (meshRenderer.sharedMaterial.HasProperty("_MainTex"))
                {
                    meshRenderer.sharedMaterial.SetTextureOffset("_MainTex", offset);
                }
            }
        }

        private void CacheMesh()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            mesh = filter.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            if (!ownsRuntimeMesh)
            {
                if (mesh.name.EndsWith(" Runtime"))
                {
                    ownsRuntimeMesh = true;
                }
                else
                {
                    mesh = Instantiate(mesh);
                    mesh.name = "Builder Ocean Mesh Runtime";
                    filter.sharedMesh = mesh;
                    ownsRuntimeMesh = true;
                }
            }

            mesh.MarkDynamic();
            baseVertices = mesh.vertices;
            animatedVertices = new Vector3[baseVertices.Length];
        }
    }
}
