using System.Collections.Generic;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class DemoIslandSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Demo_Island_Traversal.unity";
        private const string GroundLayerName = "IslandGround";
        private const int GroundLayer = 8;
        private static readonly Vector2 PlayerStartXZ = new Vector2(0f, 36f);

        [MenuItem("This Will Float DEMO/Build Demo Island Traversal Scene")]
        public static void BuildScene()
        {
            EnsureLayer(GroundLayerName, GroundLayer);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Material drySand = CreateNoiseMaterial("Dry Sand - beige", new Color(0.70f, 0.55f, 0.34f), new Color(0.88f, 0.75f, 0.52f), 0.18f);
            Material wetSand = CreateNoiseMaterial("Wet Sand - darker", new Color(0.42f, 0.34f, 0.25f), new Color(0.58f, 0.49f, 0.37f), 0.48f);
            Material volcanic = CreateNoiseMaterial("Volcanic Soil", new Color(0.11f, 0.095f, 0.075f), new Color(0.28f, 0.22f, 0.16f), 0.32f);
            Material rock = CreateNoiseMaterial("Dark Rock", new Color(0.13f, 0.13f, 0.12f), new Color(0.34f, 0.33f, 0.31f), 0.38f);
            Material foam = CreateTransparentMaterial("Shore Foam", new Color(0.85f, 0.95f, 0.95f, 0.62f), 0.18f);
            Material water = CreateTransparentMaterial("Natural Blue Water", new Color(0.04f, 0.34f, 0.58f, 0.82f), 0.55f);
            Material wood = CreateNoiseMaterial("Weathered Wood", new Color(0.35f, 0.22f, 0.12f), new Color(0.64f, 0.43f, 0.25f), 0.28f);
            Material rope = CreateNoiseMaterial("Rope", new Color(0.47f, 0.36f, 0.19f), new Color(0.72f, 0.58f, 0.32f), 0.35f);
            Material plastic = CreateMaterial("Muted Blue Plastic", new Color(0.08f, 0.25f, 0.52f), 0.35f);

            ConvertCratesAndBarrelsMaterialsToUrp();
            CreateLighting();
            CreateWater(water);
            CreateIsland(drySand, wetSand, volcanic, rock, foam);
            CreateZones(wood, rope, plastic, rock);
            CreatePlayer();

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("This Will Float DEMO", $"Demo island scene created:\n{ScenePath}", "OK");
        }

        private static void CreateLighting()
        {
            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(0.55f, 0.61f, 0.66f);

            GameObject volume = new GameObject("Scene Readability Notes");
            volume.transform.position = Vector3.zero;
        }

        private static void CreateWater(Material waterMaterial)
        {
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Ocean - no collider";
            water.transform.position = new Vector3(0f, -0.03f, 0f);
            water.transform.localScale = new Vector3(34f, 1f, 34f);
            water.GetComponent<Renderer>().sharedMaterial = waterMaterial;
            Object.DestroyImmediate(water.GetComponent<Collider>());
        }

        private static void CreateIsland(Material drySand, Material wetSand, Material volcanic, Material rock, Material foam)
        {
            GameObject root = new GameObject("Demo Island - readable gameplay layout");

            Terrain terrain = CreateWalkableTerrain("Real walkable island terrain", drySand);
            terrain.transform.SetParent(root.transform);

            GameObject body = CreateIslandBody("Solid island body - visible sides and safety floor", drySand, rock);
            body.transform.SetParent(root.transform);

            CreateIslandMesh("Central volcanic soil patch", 0f, 0.36f, volcanic, false).transform.SetParent(root.transform);
            CreateIslandMesh("Dry sloped sand beach", 0.30f, 0.86f, drySand, false).transform.SetParent(root.transform);
            CreateIslandMesh("Wet sand shoreline band", 0.82f, 1f, wetSand, false).transform.SetParent(root.transform);

            GameObject foamRing = CreateIslandMesh("Animated shoreline foam", 0.965f, 1.015f, foam, false);
            foamRing.transform.SetParent(root.transform);
            foamRing.AddComponent<ShorelinePulse>();

            CreateVolcano(root.transform, rock, volcanic);
        }

        private static Terrain CreateWalkableTerrain(string name, Material material)
        {
            const int resolution = 257;
            const float sizeX = 170f;
            const float sizeZ = 150f;
            const float terrainY = -1f;
            const float heightScale = 28f;
            float[,] heights = new float[resolution, resolution];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float worldX = Mathf.Lerp(-sizeX * 0.5f, sizeX * 0.5f, x / (float)(resolution - 1));
                    float worldZ = Mathf.Lerp(-sizeZ * 0.5f, sizeZ * 0.5f, z / (float)(resolution - 1));
                    float height = IsInsideIsland(worldX, worldZ) ? GetIslandHeight(worldX, worldZ) : -0.65f;
                    heights[z, x] = Mathf.Clamp01((height - terrainY) / heightScale);
                }
            }

            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                size = new Vector3(sizeX, heightScale, sizeZ)
            };
            terrainData.SetHeights(0, 0, heights);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = name;
            terrainObject.transform.position = new Vector3(-sizeX * 0.5f, terrainY, -sizeZ * 0.5f);
            terrainObject.layer = GroundLayer;
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawHeightmap = true;
            terrain.materialTemplate = material;

            TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
            if (collider != null)
            {
                collider.terrainData = terrainData;
            }

            return terrain;
        }

        private static GameObject CreateIslandBody(string name, Material topMaterial, Material sideMaterial)
        {
            const int segments = 144;
            const float bottomY = -5f;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            int topCenter = vertices.Count;
            vertices.Add(new Vector3(0f, GetIslandHeight(0f, 0f) - 0.25f, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment / (float)segments * Mathf.PI * 2f;
                Vector2 point = GetIslandPoint(angle, 1f);
                float y = GetIslandHeight(point.x, point.y) - 0.05f;
                vertices.Add(new Vector3(point.x, y, point.y));
                uvs.Add(new Vector2(point.x * 0.025f, point.y * 0.025f));
            }

            int bottomCenter = vertices.Count;
            vertices.Add(new Vector3(0f, bottomY, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));

            int bottomStart = vertices.Count;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment / (float)segments * Mathf.PI * 2f;
                Vector2 point = GetIslandPoint(angle, 1f);
                vertices.Add(new Vector3(point.x, bottomY, point.y));
                uvs.Add(new Vector2(point.x * 0.025f, point.y * 0.025f));
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                int topA = 1 + segment;
                int topB = 1 + next;
                int bottomA = bottomStart + segment;
                int bottomB = bottomStart + next;

                triangles.Add(topCenter);
                triangles.Add(topA);
                triangles.Add(topB);

                triangles.Add(topA);
                triangles.Add(bottomA);
                triangles.Add(topB);
                triangles.Add(topB);
                triangles.Add(bottomA);
                triangles.Add(bottomB);

                triangles.Add(bottomCenter);
                triangles.Add(bottomB);
                triangles.Add(bottomA);
            }

            Mesh mesh = new Mesh
            {
                name = $"{name} Mesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject obj = new GameObject(name);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sideMaterial != null ? sideMaterial : topMaterial;
            return obj;
        }

        private static GameObject CreateIslandMesh(string name, float tMin, float tMax, Material material, bool collider)
        {
            const int segments = 144;
            const int rings = 14;
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int ring = 0; ring <= rings; ring++)
            {
                float t = Mathf.Lerp(tMin, tMax, ring / (float)rings);
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    Vector2 point = GetIslandPoint(angle, t);
                    float y = GetIslandHeight(point.x, point.y);
                    vertices.Add(new Vector3(point.x, y, point.y));
                    uvs.Add(new Vector2(point.x * 0.035f, point.y * 0.035f));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    int a = ring * segments + segment;
                    int b = ring * segments + next;
                    int c = (ring + 1) * segments + segment;
                    int d = (ring + 1) * segments + next;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh
            {
                name = $"{name} Mesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject obj = new GameObject(name);
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            obj.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (collider)
            {
                MeshCollider meshCollider = obj.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = mesh;
            }

            return obj;
        }

        private static Vector2 GetIslandPoint(float angle, float t)
        {
            float irregular = 1f
                + Mathf.Sin(angle * 3.1f + 0.35f) * 0.075f
                + Mathf.Cos(angle * 5.7f - 1.2f) * 0.055f
                + Mathf.Sin(angle * 9.4f) * 0.025f;
            float radiusX = 74f * irregular;
            float radiusZ = 62f * (1f + (irregular - 1f) * 0.8f);
            return new Vector2(Mathf.Cos(angle) * radiusX * t, Mathf.Sin(angle) * radiusZ * t);
        }

        private static bool IsInsideIsland(float x, float z)
        {
            float nx = x / 74f;
            float nz = z / 62f;
            float angle = Mathf.Atan2(nz, nx);
            float irregular = 1f
                + Mathf.Sin(angle * 3.1f + 0.35f) * 0.075f
                + Mathf.Cos(angle * 5.7f - 1.2f) * 0.055f
                + Mathf.Sin(angle * 9.4f) * 0.025f;
            return Mathf.Sqrt(nx * nx + nz * nz) <= irregular * 1.02f;
        }

        private static float GetIslandHeight(float x, float z)
        {
            float nx = x / 74f;
            float nz = z / 62f;
            float t = Mathf.Clamp01(Mathf.Sqrt(nx * nx + nz * nz));
            float beachSlope = Mathf.Lerp(5.8f, 0.22f, Mathf.Pow(t, 1.35f));
            float launchFlatten = Mathf.InverseLerp(0.35f, 0.8f, z / 62f) * Mathf.InverseLerp(38f, 8f, Mathf.Abs(x));
            float noise = (Mathf.PerlinNoise(x * 0.035f + 9.1f, z * 0.035f + 4.7f) - 0.5f) * 0.55f;
            return Mathf.Lerp(beachSlope + noise, 1.1f + noise * 0.15f, launchFlatten * 0.75f);
        }

        private static void CreateVolcano(Transform parent, Material rock, Material volcanic)
        {
            GameObject volcano = new GameObject("Central volcano landmark");
            volcano.transform.SetParent(parent);
            Mesh mesh = new Mesh { name = "Readable Volcano Mesh" };
            const int segments = 96;
            const int rings = 12;
            Vector3[] vertices = new Vector3[(rings + 1) * segments + 1];
            Vector2[] uvs = new Vector2[vertices.Length];
            List<int> triangles = new List<int>();

            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float radius = Mathf.Lerp(24f, 5.5f, Mathf.Pow(t, 0.82f));
                float y = GetIslandHeight(0f, 0f) + Mathf.Lerp(0f, 17f, Mathf.Pow(t, 1.25f));
                if (ring == rings)
                {
                    y -= 2.2f;
                }

                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    float rough = 1f + Mathf.Sin(angle * 6f + ring) * 0.045f;
                    int index = ring * segments + segment;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * radius * rough, y, Mathf.Sin(angle) * radius * rough);
                    uvs[index] = new Vector2(segment / (float)segments, t);
                }
            }

            int craterCenter = vertices.Length - 1;
            vertices[craterCenter] = new Vector3(0f, vertices[rings * segments].y - 0.8f, 0f);
            uvs[craterCenter] = new Vector2(0.5f, 1f);

            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    int a = ring * segments + segment;
                    int b = ring * segments + next;
                    int c = (ring + 1) * segments + segment;
                    int d = (ring + 1) * segments + next;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            int topStart = rings * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(topStart + segment);
                triangles.Add(craterCenter);
                triangles.Add(topStart + next);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            volcano.AddComponent<MeshFilter>().sharedMesh = mesh;
            volcano.AddComponent<MeshRenderer>().sharedMaterial = rock;

            GameObject crater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crater.name = "Dark crater";
            crater.transform.SetParent(parent);
            crater.transform.position = new Vector3(0f, vertices[craterCenter].y + 0.08f, 0f);
            crater.transform.localScale = new Vector3(4.6f, 0.08f, 4.6f);
            crater.GetComponent<Renderer>().sharedMaterial = volcanic;
            Object.DestroyImmediate(crater.GetComponent<Collider>());
        }

        private static void CreateZones(Material wood, Material rope, Material plastic, Material rock)
        {
            Material sign = CreateMaterial("Sign muted green", new Color(0.18f, 0.35f, 0.22f), 0.35f);
            Material raft = CreateMaterial("Unfinished raft wood", new Color(0.45f, 0.29f, 0.14f), 0.28f);

            GameObject zones = new GameObject("Readable gameplay zones");

            CreateLaunchZone(zones.transform, raft, sign);
            CreateShipwreckZone(zones.transform, wood, rope);
            CreateBuoyancyZone(zones.transform, wood, plastic);
            CreateHeavyResourceZone(zones.transform, rock);
            CreatePalmZone(zones.transform, wood);
            CreateSos(zones.transform, wood);
        }

        private static void CreateLaunchZone(Transform parent, Material raft, Material sign)
        {
            CreatePrimitive("Launch direction arrow", PrimitiveType.Cube, new Vector3(0f, GetIslandHeight(0f, 51f) + 0.08f, 53f), new Vector3(1.2f, 0.08f, 8f), sign, parent, Quaternion.identity);
            CreatePrimitive("Launch button base", PrimitiveType.Cylinder, new Vector3(8f, GetIslandHeight(8f, 45f) + 0.22f, 45f), new Vector3(0.8f, 0.18f, 0.8f), sign, parent, Quaternion.identity);
            CreatePrimitive("Launch sign post", PrimitiveType.Cube, new Vector3(-8f, GetIslandHeight(-8f, 45f) + 1.2f, 45f), new Vector3(0.18f, 2.4f, 0.18f), sign, parent, Quaternion.identity);
            CreatePrimitive("Launch sign board", PrimitiveType.Cube, new Vector3(-8f, GetIslandHeight(-8f, 45f) + 2.35f, 45f), new Vector3(3.1f, 1.1f, 0.18f), sign, parent, Quaternion.identity);

            for (int i = 0; i < 7; i++)
            {
                float x = -3.6f + i * 1.2f;
                CreatePrimitive("Raft log placeholder", PrimitiveType.Cylinder, new Vector3(x, GetIslandHeight(x, 43f) + 0.32f, 43f), new Vector3(0.28f, 2.8f, 0.28f), raft, parent, Quaternion.Euler(90f, 0f, 0f));
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -4.5f + i * 1.8f;
                CreatePrimitive("Material placement slot", PrimitiveType.Cube, new Vector3(x, GetIslandHeight(x, 39f) + 0.04f, 39f), new Vector3(1.2f, 0.05f, 1.2f), sign, parent, Quaternion.identity);
            }
        }

        private static void CreateShipwreckZone(Transform parent, Material wood, Material rope)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(-42f + i * 2.4f, 0f, 30f + Mathf.Sin(i) * 3f);
                p.y = GetIslandHeight(p.x, p.z) + 0.18f;
                CreatePrimitive("Shipwreck plank", PrimitiveType.Cube, p, new Vector3(2.8f, 0.16f, 0.42f), wood, parent, Quaternion.Euler(0f, -25f + i * 11f, 0f));
            }

            CreateTorusPlaced("Rope bundle", new Vector3(-32f, GetIslandHeight(-32f, 24f) + 0.22f, 24f), new Vector3(0.8f, 0.18f, 0.8f), rope, parent, Quaternion.identity);
            CreateCrate(parent, new Vector3(-39f, GetIslandHeight(-39f, 22f) + 0.45f, 22f), 1.1f);
        }

        private static void CreateBuoyancyZone(Transform parent, Material wood, Material plastic)
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = new Vector3(35f + i * 3.4f, 0f, 28f + Mathf.Sin(i * 1.7f) * 3f);
                p.y = GetIslandHeight(p.x, p.z) + 0.45f;
                if (!TryInstantiatePrefab("Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigBarrel_color01.prefab", "Plastic float barrel", p, Quaternion.Euler(0f, 0f, 90f), Vector3.one * 0.65f, parent, plastic))
                {
                    CreatePrimitive("Plastic float barrel", PrimitiveType.Cylinder, p, new Vector3(0.65f, 0.9f, 0.65f), plastic, parent, Quaternion.Euler(0f, 0f, 90f));
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(42f + i * 2.3f, 0f, 19f);
                p.y = GetIslandHeight(p.x, p.z) + 0.38f;
                if (!TryInstantiatePrefab("Assets/AwesomeFreeScans/WoodLogs/Prefabs/LowEndPC/SM_AFS_Log01_LowEndPC.prefab", "Loose log", p, Quaternion.Euler(0f, 80f + i * 12f, 90f), Vector3.one * 0.85f, parent, wood))
                {
                    CreatePrimitive("Loose log", PrimitiveType.Cylinder, p, new Vector3(0.34f, 2.1f, 0.34f), wood, parent, Quaternion.Euler(90f, 0f, 80f + i * 12f));
                }
            }
        }

        private static void CreateHeavyResourceZone(Transform parent, Material rock)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = new Vector3(-34f + i * 2.7f, 0f, -28f + Mathf.Sin(i * 1.9f) * 4f);
                p.y = GetIslandHeight(p.x, p.z) + 0.55f;
                if (!TryInstantiatePrefab("Assets/RockFREE/LODGroups/rock1_LOD0.prefab", "Heavy rock resource", p, Quaternion.Euler(0f, i * 31f, 0f), Vector3.one * (1.2f + i % 3 * 0.18f), parent, rock))
                {
                    CreatePrimitive("Heavy rock resource", PrimitiveType.Sphere, p, new Vector3(1.4f, 0.9f, 1.2f), rock, parent, Quaternion.identity);
                }
            }

            CreateCrate(parent, new Vector3(-25f, GetIslandHeight(-25f, -36f) + 0.52f, -36f), 1.4f);
        }

        private static void CreatePalmZone(Transform parent, Material wood)
        {
            CreatePalm(parent, new Vector3(35f, GetIslandHeight(35f, -30f), -30f), 1.1f);
            CreatePalm(parent, new Vector3(48f, GetIslandHeight(48f, -42f), -42f), 0.95f);
            CreatePalm(parent, new Vector3(24f, GetIslandHeight(24f, -47f), -47f), 1.0f);
            CreatePrimitive("Torn cloth marker", PrimitiveType.Cube, new Vector3(37f, GetIslandHeight(37f, -36f) + 0.55f, -36f), new Vector3(1.4f, 0.06f, 1f), CreateMaterial("Dull cloth", new Color(0.16f, 0.19f, 0.30f), 0.55f), parent, Quaternion.Euler(12f, 35f, 0f));
            CreatePrimitive("Fallen branch", PrimitiveType.Cylinder, new Vector3(31f, GetIslandHeight(31f, -38f) + 0.28f, -38f), new Vector3(0.18f, 1.8f, 0.18f), wood, parent, Quaternion.Euler(90f, 25f, 0f));
        }

        private static void CreateSos(Transform parent, Material wood)
        {
            CreatePrimitive("SOS stroke S1", PrimitiveType.Cube, new Vector3(-5f, GetIslandHeight(-5f, -51f) + 0.08f, -51f), new Vector3(4f, 0.08f, 0.35f), wood, parent, Quaternion.Euler(0f, 12f, 0f));
            CreateTorusPlaced("SOS stroke O", new Vector3(0f, GetIslandHeight(0f, -51f) + 0.08f, -51f), new Vector3(1.2f, 0.08f, 1.2f), wood, parent, Quaternion.identity);
            CreatePrimitive("SOS stroke S2", PrimitiveType.Cube, new Vector3(5f, GetIslandHeight(5f, -51f) + 0.08f, -51f), new Vector3(4f, 0.08f, 0.35f), wood, parent, Quaternion.Euler(0f, -12f, 0f));
        }

        private static void CreatePlayer()
        {
            Vector3 playerStart = GetPlayerStart();
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "First Person Player";
            player.transform.position = playerStart;
            player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            player.GetComponent<Renderer>().enabled = false;
            Object.DestroyImmediate(player.GetComponent<Collider>());

            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);

            GameObject cameraObject = new GameObject("First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.04f;
            cameraObject.AddComponent<AudioListener>();

            DemoFirstPersonPlayer controller = player.AddComponent<DemoFirstPersonPlayer>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("groundMask").intValue = 1 << GroundLayer;
            serialized.FindProperty("startPosition").vector3Value = playerStart;
            serialized.FindProperty("startRotation").quaternionValue = Quaternion.Euler(0f, 180f, 0f);
            serialized.FindProperty("resetIfBelowY").floatValue = -8f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Vector3 GetPlayerStart()
        {
            float x = PlayerStartXZ.x;
            float z = PlayerStartXZ.y;
            return new Vector3(x, GetIslandHeight(x, z) + 0.35f, z);
        }

        private static void CreatePalm(Transform parent, Vector3 basePosition, float scale)
        {
            string prefabPath = scale > 1f ? "Assets/PalmTreePack/Prefabs/1/Palm_1_1.prefab" : "Assets/PalmTreePack/Prefabs/2/Palm_2_1.prefab";
            if (TryInstantiatePrefab(prefabPath, "Palm tree", basePosition, Quaternion.Euler(0f, basePosition.x * 3.7f, 0f), Vector3.one * scale, parent, null))
            {
                return;
            }

            Material bark = CreateMaterial("Palm bark", new Color(0.35f, 0.22f, 0.12f), 0.3f);
            Material leaf = CreateMaterial("Muted palm leaf", new Color(0.06f, 0.34f, 0.15f), 0.45f);
            CreatePrimitive("Palm trunk", PrimitiveType.Cylinder, basePosition + Vector3.up * (2.2f * scale), new Vector3(0.35f * scale, 2.2f * scale, 0.35f * scale), bark, parent, Quaternion.Euler(5f, 0f, 8f));
            for (int i = 0; i < 7; i++)
            {
                CreatePrimitive("Palm frond", PrimitiveType.Cube, basePosition + Vector3.up * (4.45f * scale), new Vector3(0.22f * scale, 0.08f * scale, 2.4f * scale), leaf, parent, Quaternion.Euler(15f, i * 51.4f, 0f));
            }
        }

        private static void CreateCrate(Transform parent, Vector3 position, float scale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigCrate_color01.prefab");
            if (prefab != null)
            {
                GameObject crate = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                crate.name = "Heavy crate resource";
                crate.transform.SetParent(parent);
                crate.transform.position = position;
                crate.transform.localScale = Vector3.one * scale;
                ReplaceBrokenMaterials(crate, CreateMaterial("Fallback crate", new Color(0.42f, 0.27f, 0.13f), 0.35f));
                return;
            }

            CreatePrimitive("Heavy crate resource", PrimitiveType.Cube, position, Vector3.one * scale, CreateMaterial("Fallback crate", new Color(0.42f, 0.27f, 0.13f), 0.35f), parent, Quaternion.identity);
        }

        private static bool TryInstantiatePrefab(string path, string name, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent, Material fallbackMaterial)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return false;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            ReplaceBrokenMaterials(instance, fallbackMaterial);
            return true;
        }

        private static void ReplaceBrokenMaterials(GameObject instance, Material fallbackMaterial)
        {
            if (fallbackMaterial == null)
            {
                return;
            }

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] == null || materials[i].shader == null || materials[i].shader.name == "Hidden/InternalErrorShader")
                    {
                        materials[i] = fallbackMaterial;
                        changed = true;
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material, Transform parent, Quaternion rotation)
        {
            GameObject obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return obj;
        }

        private static GameObject CreateTorusPlaced(string name, Vector3 position, Vector3 scale, Material material, Transform parent, Quaternion rotation)
        {
            GameObject obj = CreateTorus(name);
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            return obj;
        }

        private static GameObject CreateTorus(string name)
        {
            const int majorSegments = 36;
            const int minorSegments = 8;
            const float majorRadius = 0.6f;
            const float minorRadius = 0.12f;
            Mesh mesh = new Mesh { name = $"{name} Mesh" };
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            for (int major = 0; major < majorSegments; major++)
            {
                float majorAngle = major / (float)majorSegments * Mathf.PI * 2f;
                Vector3 center = new Vector3(Mathf.Cos(majorAngle) * majorRadius, 0f, Mathf.Sin(majorAngle) * majorRadius);
                for (int minor = 0; minor < minorSegments; minor++)
                {
                    float minorAngle = minor / (float)minorSegments * Mathf.PI * 2f;
                    Vector3 radial = new Vector3(Mathf.Cos(majorAngle), 0f, Mathf.Sin(majorAngle));
                    Vector3 up = Vector3.up;
                    vertices.Add(center + radial * Mathf.Cos(minorAngle) * minorRadius + up * Mathf.Sin(minorAngle) * minorRadius);
                }
            }

            for (int major = 0; major < majorSegments; major++)
            {
                for (int minor = 0; minor < minorSegments; minor++)
                {
                    int nextMajor = (major + 1) % majorSegments;
                    int nextMinor = (minor + 1) % minorSegments;
                    int a = major * minorSegments + minor;
                    int b = major * minorSegments + nextMinor;
                    int c = nextMajor * minorSegments + minor;
                    int d = nextMajor * minorSegments + nextMinor;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject torus = new GameObject(name);
            torus.AddComponent<MeshFilter>().sharedMesh = mesh;
            torus.AddComponent<MeshRenderer>();
            torus.AddComponent<MeshCollider>().sharedMesh = mesh;
            return torus;
        }

        private static Material CreateNoiseMaterial(string name, Color dark, Color light, float smoothness)
        {
            Material material = CreateMaterial(name, light, smoothness);
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, true)
            {
                name = $"{name} Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f);
                    float grain = Mathf.PerlinNoise(x * 0.31f + 2.3f, y * 0.31f + 4.1f) * 0.2f;
                    texture.SetPixel(x, y, Color.Lerp(dark, light, Mathf.Clamp01(n + grain)));
                }
            }

            texture.Apply();
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }
            else if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            return material;
        }

        private static Material CreateTransparentMaterial(string name, Color color, float smoothness)
        {
            Material material = CreateMaterial(name, color, smoothness);
            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetInt("_ZWrite", 0);
            }

            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return material;
        }

        private static Material CreateMaterial(string name, Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            Material material = new Material(shader)
            {
                name = name,
                color = color
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            return material;
        }

        [MenuItem("This Will Float DEMO/Fix Crates And Barrels Materials")]
        public static void ConvertCratesAndBarrelsMaterialsToUrp()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Crates And Barrels Pack Volume 1 - Free Version" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                Texture mainTexture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                Texture normalTexture = material.HasProperty("_BumpMap") ? material.GetTexture("_BumpMap") : null;
                Texture metallicTexture = material.HasProperty("_MetallicGlossMap") ? material.GetTexture("_MetallicGlossMap") : null;
                Color color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;

                material.shader = shader;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", mainTexture);
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                if (normalTexture != null && material.HasProperty("_BumpMap"))
                {
                    material.SetTexture("_BumpMap", normalTexture);
                    material.EnableKeyword("_NORMALMAP");
                }

                if (metallicTexture != null && material.HasProperty("_MetallicGlossMap"))
                {
                    material.SetTexture("_MetallicGlossMap", metallicTexture);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.38f);
                }

                EditorUtility.SetDirty(material);
            }

            AssetDatabase.SaveAssets();
        }

        private static void EnsureLayer(string layerName, int layerIndex)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            SerializedProperty layer = layers.GetArrayElementAtIndex(layerIndex);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
