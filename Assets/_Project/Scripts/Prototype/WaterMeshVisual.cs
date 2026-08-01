using UnityEngine;

namespace ThisWillFloat.Prototype
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class WaterMeshVisual : MonoBehaviour
    {
        [SerializeField] private WaveSurface waveSurface;
        [SerializeField] private float size = 80f;
        [SerializeField] private int resolution = 96;
        [SerializeField] private float normalUpdateInterval = 0.08f;

        private Mesh mesh;
        private Vector3[] vertices;
        private float nextNormalUpdateTime;

        private void Awake()
        {
            if (waveSurface == null)
            {
                waveSurface = GetComponent<WaveSurface>();
            }

            BuildMesh();
        }

        private void Update()
        {
            if (mesh == null || waveSurface == null)
            {
                return;
            }

            float halfSize = size * 0.5f;
            float step = size / resolution;

            for (int z = 0, index = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++, index++)
                {
                    float localX = -halfSize + x * step;
                    float localZ = -halfSize + z * step;
                    Vector3 worldPosition = transform.TransformPoint(new Vector3(localX, 0f, localZ));
                    float worldHeight = waveSurface.GetHeight(worldPosition);
                    vertices[index].y = transform.InverseTransformPoint(new Vector3(worldPosition.x, worldHeight, worldPosition.z)).y;
                }
            }

            mesh.vertices = vertices;

            if (Time.time >= nextNormalUpdateTime)
            {
                mesh.RecalculateNormals();
                nextNormalUpdateTime = Time.time + normalUpdateInterval;
            }

            mesh.RecalculateBounds();
        }

        private void BuildMesh()
        {
            resolution = Mathf.Clamp(resolution, 8, 180);
            mesh = new Mesh
            {
                name = "Prototype Animated Water Mesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };

            int vertexCount = (resolution + 1) * (resolution + 1);
            vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[resolution * resolution * 6];

            float halfSize = size * 0.5f;
            float step = size / resolution;

            for (int z = 0, index = 0; z <= resolution; z++)
            {
                for (int x = 0; x <= resolution; x++, index++)
                {
                    vertices[index] = new Vector3(-halfSize + x * step, 0f, -halfSize + z * step);
                    uvs[index] = new Vector2(x / (float)resolution, z / (float)resolution);
                }
            }

            for (int z = 0, triangleIndex = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int root = z * (resolution + 1) + x;
                    triangles[triangleIndex++] = root;
                    triangles[triangleIndex++] = root + resolution + 1;
                    triangles[triangleIndex++] = root + 1;
                    triangles[triangleIndex++] = root + 1;
                    triangles[triangleIndex++] = root + resolution + 1;
                    triangles[triangleIndex++] = root + resolution + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GetComponent<MeshFilter>().sharedMesh = mesh;
        }
    }
}
