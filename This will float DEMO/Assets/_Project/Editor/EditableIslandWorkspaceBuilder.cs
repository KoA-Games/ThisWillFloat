using System.IO;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class EditableIslandWorkspaceBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Editable_Island_Workspace.unity";
        private const string TerrainFolder = "Assets/_Project/Terrain";
        private const string MaterialFolder = "Assets/_Project/GeneratedMaterials";
        private const string GroundLayerName = "IslandGround";
        private const int GroundLayer = 8;

        [MenuItem("This Will Float DEMO/1. Fix Imported Asset Materials")]
        public static void FixImportedAssetMaterials()
        {
            StableIslandBuilderUtility.FixImportedAssetMaterials();
            EditorUtility.DisplayDialog("This Will Float DEMO", "Imported asset materials were converted for URP.", "OK");
        }

        [MenuItem("This Will Float DEMO/2. Build Editable Island Workspace")]
        public static void BuildEditableIslandWorkspace()
        {
            StableIslandBuilderUtility.BuildFreshWorkspace();
            EditorUtility.DisplayDialog("This Will Float DEMO", "Stable ocean island builder workspace created. Use RESET OCEAN CANVAS or CREATE BIG EDITABLE ISLAND in the Island Builder window.", "OK");
        }

        [MenuItem("This Will Float DEMO/3. Open Island Editing Helper")]
        public static void OpenIslandEditingHelper()
        {
            StableIslandBuilderWindow.ShowWindow();
        }

        [MenuItem("This Will Float DEMO/Legacy Terrain Tools/Make Current Terrain Big Above Water")]
        public static void MakeCurrentTerrainBigAboveWater()
        {
            Terrain terrain = GetEditableTerrain();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("This Will Float DEMO", "Select or create EDIT ME - Island Terrain first.", "OK");
                return;
            }

            ApplyLargeReadableIsland(terrain);
            SetOceanVisible(false);
            EditorUtility.SetDirty(terrain.terrainData);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("This Will Float DEMO/Legacy Terrain Tools/Rebuild Visible Editable Island")]
        public static void RebuildVisibleEditableIslandMenu()
        {
            Terrain terrain = RebuildVisibleEditableIsland();
            if (terrain != null)
            {
                Selection.activeGameObject = terrain.gameObject;
                EditorUtility.DisplayDialog("This Will Float DEMO", "A fresh visible editable island was rebuilt above water.", "OK");
            }
        }

        [MenuItem("This Will Float DEMO/Legacy Terrain Tools/Toggle Old Ocean Visibility")]
        public static void ToggleOceanVisibility()
        {
            GameObject ocean = FindOcean();
            if (ocean == null)
            {
                return;
            }

            ocean.SetActive(!ocean.activeSelf);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void FixImportedAssetMaterialsNoDialog()
        {
            ConvertMaterialsInFolder("Assets/Crates And Barrels Pack Volume 1 - Free Version");
            ConvertMaterialsInFolder("Assets/PalmTreePack");
            ConvertMaterialsInFolder("Assets/RockFREE");
            ConvertMaterialsInFolder("Assets/AwesomeFreeScans/WoodLogs");
            AssetDatabase.SaveAssets();
        }

        internal static Terrain RebuildVisibleEditableIsland()
        {
            EnsureLayer(GroundLayerName, GroundLayer);
            EnsureFolder(TerrainFolder);
            EnsureFolder(MaterialFolder);

            DeleteSceneObjectsNamed("EDIT ME - Island Terrain");
            DeleteSceneObjectsNamed("Builder Land Patches");

            Terrain terrain = CreateEditableTerrain();
            MoveGameplayObjectsToTerrain(terrain);
            SetOceanVisible(false);
            Selection.activeGameObject = terrain.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return terrain;
        }

        internal static Terrain RebuildOceanSculptCanvas()
        {
            EnsureLayer(GroundLayerName, GroundLayer);
            EnsureFolder(TerrainFolder);
            EnsureFolder(MaterialFolder);

            DeleteSceneObjectsNamed("EDIT ME - Island Terrain");
            DeleteSceneObjectsNamed("Ocean - waves, visual only");
            DeleteSceneObjectsNamed("Ocean - visual only, no collider");
            DeleteSceneObjectsNamed("Builder Land Patches");

            Terrain terrain = CreateBlankOceanTerrain();
            Material water = CreateMaterialAsset("Demo Water", new Color(0.04f, 0.28f, 0.48f, 0.64f), 0.58f, true);
            CreateOcean(water);
            SetOceanVisible(true);
            Selection.activeGameObject = terrain.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return terrain;
        }

        private static void DeleteSceneObjectsNamed(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                if (IsSceneObject(objects[i]) && objects[i].name == objectName)
                {
                    Object.DestroyImmediate(objects[i]);
                }
            }
        }

        private static bool IsSceneObject(GameObject obj)
        {
            return obj != null && obj.scene.IsValid() && !EditorUtility.IsPersistent(obj);
        }

        private static void ConvertMaterialsInFolder(string folder)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null || !AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                Texture main = GetTexture(material, "_BaseMap", "_MainTex", "_AlbedoMap");
                Texture normal = GetTexture(material, "_BumpMap", "_NormalMap");
                Texture metallic = GetTexture(material, "_MetallicGlossMap", "_MetallicMap");
                Color color = GetColor(material, "_BaseColor", "_Color");

                material.shader = shader;
                SetTexture(material, main, "_BaseMap", "_MainTex");
                SetTexture(material, normal, "_BumpMap");
                SetTexture(material, metallic, "_MetallicGlossMap");
                SetColor(material, color, "_BaseColor", "_Color");
                if (normal != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                }

                if (metallic != null)
                {
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }

                if (material.HasProperty("_Smoothness"))
                {
                    material.SetFloat("_Smoothness", 0.36f);
                }

                EditorUtility.SetDirty(material);
            }
        }

        private static Terrain CreateEditableTerrain()
        {
            const int resolution = 513;
            const float terrainSize = 520f;
            const float terrainHeight = 92f;
            const float baseY = -10.0f;
            float[,] heights = new float[resolution, resolution];

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)(resolution - 1) * 2f - 1f;
                    float nz = z / (float)(resolution - 1) * 2f - 1f;
                    float angle = Mathf.Atan2(nz, nx);
                    float radius = Mathf.Sqrt(nx * nx + nz * nz);
                    float irregularEdge = 0.76f + Mathf.Sin(angle * 2.2f) * 0.08f + Mathf.Cos(angle * 4.9f) * 0.06f + Mathf.Sin(angle * 8.1f) * 0.025f;
                    float islandMask = SmoothStep01((irregularEdge - radius) / 0.105f);
                    float normalizedInland = Mathf.Clamp01(1f - radius / irregularEdge);
                    float beach = Mathf.Lerp(2.4f, 11.5f, Mathf.Pow(normalizedInland, 0.72f));
                    float volcano = Mathf.Clamp01(1f - radius / 0.37f);
                    float crater = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.105f) / 0.04f);
                    float ridge = Mathf.Sin(angle * 7f + radius * 12f) * Mathf.Clamp01(1f - radius / 0.52f) * 2.1f;
                    float noise = (Mathf.PerlinNoise(x * 0.025f + 12.7f, z * 0.025f + 8.3f) - 0.5f) * 1.4f;
                    float islandHeight = beach + Mathf.Pow(volcano, 1.75f) * 34f - crater * 7f + ridge + noise;
                    float worldHeight = Mathf.Lerp(-4.0f, islandHeight, islandMask);
                    heights[z, x] = Mathf.Clamp01((worldHeight - baseY) / terrainHeight);
                }
            }

            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                alphamapResolution = 256,
                size = new Vector3(terrainSize, terrainHeight, terrainSize)
            };
            terrainData.SetHeights(0, 0, heights);
            terrainData.terrainLayers = CreateTerrainLayers();
            PaintTerrain(terrainData);

            string dataPath = AssetDatabase.GenerateUniqueAssetPath($"{TerrainFolder}/EditableIslandTerrain.asset");
            AssetDatabase.CreateAsset(terrainData, dataPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "EDIT ME - Island Terrain";
            terrainObject.transform.position = new Vector3(-terrainSize * 0.5f, baseY, -terrainSize * 0.5f);
            terrainObject.layer = GroundLayer;
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = false;
            terrain.materialTemplate = GetOrCreateTerrainMaterial();
            terrain.heightmapPixelError = 2f;
            terrainObject.GetComponent<TerrainCollider>().terrainData = terrainData;
            return terrain;
        }

        private static Terrain CreateBlankOceanTerrain()
        {
            const int resolution = 513;
            const float terrainSize = 760f;
            const float terrainHeight = 64f;
            const float baseY = -12f;
            const float seaFloorY = -3.6f;
            float[,] heights = new float[resolution, resolution];
            float normalizedSeaFloor = Mathf.Clamp01((seaFloorY - baseY) / terrainHeight);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    heights[z, x] = normalizedSeaFloor;
                }
            }

            TerrainData terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                alphamapResolution = 256,
                size = new Vector3(terrainSize, terrainHeight, terrainSize)
            };
            terrainData.SetHeights(0, 0, heights);
            terrainData.terrainLayers = CreateTerrainLayers();
            PaintBlankOceanFloor(terrainData);

            string dataPath = AssetDatabase.GenerateUniqueAssetPath($"{TerrainFolder}/EditableIslandTerrain.asset");
            AssetDatabase.CreateAsset(terrainData, dataPath);

            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            terrainObject.name = "EDIT ME - Island Terrain";
            terrainObject.transform.position = new Vector3(-terrainSize * 0.5f, baseY, -terrainSize * 0.5f);
            terrainObject.layer = GroundLayer;
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.drawInstanced = true;
            terrain.allowAutoConnect = false;
            terrain.materialTemplate = GetOrCreateTerrainMaterial();
            terrain.heightmapPixelError = 2f;
            terrainObject.GetComponent<TerrainCollider>().terrainData = terrainData;
            return terrain;
        }

        private static Material GetOrCreateTerrainMaterial()
        {
            string path = $"{MaterialFolder}/Editable Island Terrain.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Lit");
            }

            material = new Material(shader)
            {
                name = "Editable Island Terrain"
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static TerrainLayer[] CreateTerrainLayers()
        {
            return new[]
            {
                CreateTerrainLayer("Dry Sand Layer", new Color(0.76f, 0.58f, 0.34f)),
                CreateTerrainLayer("Wet Sand Layer", new Color(0.42f, 0.34f, 0.25f)),
                CreateTerrainLayer("Volcanic Soil Layer", new Color(0.12f, 0.11f, 0.10f)),
                CreateTerrainLayer("Muted Grass Layer", new Color(0.19f, 0.38f, 0.20f))
            };
        }

        private static TerrainLayer CreateTerrainLayer(string name, Color color)
        {
            Texture2D texture = CreateNoiseTexture($"{name} Texture", color * 0.82f, color * 1.18f);
            string texturePath = AssetDatabase.GenerateUniqueAssetPath($"{TerrainFolder}/{name}_Texture.asset");
            AssetDatabase.CreateAsset(texture, texturePath);

            TerrainLayer layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = texture,
                tileSize = new Vector2(11f, 11f),
                smoothness = 0.18f
            };
            string layerPath = AssetDatabase.GenerateUniqueAssetPath($"{TerrainFolder}/{name}.terrainlayer");
            AssetDatabase.CreateAsset(layer, layerPath);
            return layer;
        }

        private static void PaintTerrain(TerrainData data)
        {
            int width = data.alphamapWidth;
            int height = data.alphamapHeight;
            float[,,] maps = new float[height, width, 4];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float nx = x / (float)(width - 1) * 2f - 1f;
                    float nz = z / (float)(height - 1) * 2f - 1f;
                    float radius = Mathf.Sqrt(nx * nx + nz * nz);
                    float dry = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.62f) / 0.32f);
                    float wet = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.84f) / 0.10f);
                    float volcanic = Mathf.Clamp01(1f - radius / 0.48f);
                    float grass = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.38f) / 0.18f) * 0.55f;
                    float sum = dry + wet + volcanic + grass + 0.001f;
                    maps[z, x, 0] = dry / sum;
                    maps[z, x, 1] = wet / sum;
                    maps[z, x, 2] = volcanic / sum;
                    maps[z, x, 3] = grass / sum;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        private static void PaintBlankOceanFloor(TerrainData data)
        {
            int width = data.alphamapWidth;
            int height = data.alphamapHeight;
            float[,,] maps = new float[height, width, 4];

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    maps[z, x, 0] = 0.18f;
                    maps[z, x, 1] = 0.82f;
                    maps[z, x, 2] = 0f;
                    maps[z, x, 3] = 0f;
                }
            }

            data.SetAlphamaps(0, 0, maps);
        }

        private static void CreateOcean(Material water)
        {
            GameObject ocean = new GameObject("Ocean - waves, visual only");
            ocean.transform.position = new Vector3(0f, 0f, 0f);
            MeshFilter filter = ocean.AddComponent<MeshFilter>();
            MeshRenderer renderer = ocean.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            filter.sharedMesh = CreateOceanGridMesh(2200f, 160);
            ocean.AddComponent<SimpleWaveWater>();
            ocean.SetActive(true);
        }

        private static Mesh CreateOceanGridMesh(float size, int divisions)
        {
            int vertexCount = (divisions + 1) * (divisions + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[divisions * divisions * 6];

            for (int z = 0; z <= divisions; z++)
            {
                for (int x = 0; x <= divisions; x++)
                {
                    int index = z * (divisions + 1) + x;
                    float px = Mathf.Lerp(-size * 0.5f, size * 0.5f, x / (float)divisions);
                    float pz = Mathf.Lerp(-size * 0.5f, size * 0.5f, z / (float)divisions);
                    vertices[index] = new Vector3(px, 0f, pz);
                    uvs[index] = new Vector2(x / (float)divisions, z / (float)divisions);
                }
            }

            int t = 0;
            for (int z = 0; z < divisions; z++)
            {
                for (int x = 0; x < divisions; x++)
                {
                    int a = z * (divisions + 1) + x;
                    int b = a + 1;
                    int c = a + divisions + 1;
                    int d = c + 1;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Editable Workspace Ocean Grid",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void CreateGuideZones(Material marker, Material wood)
        {
            GameObject root = new GameObject("Editable Layout Markers");
            CreateMarker("Launch Beach / Boat Build Area", new Vector3(0f, 1.2f, 58f), marker, root.transform);
            CreateMarker("Shipwreck Debris Area", new Vector3(-48f, 1.2f, 26f), marker, root.transform);
            CreateMarker("Buoyancy Materials Area", new Vector3(48f, 1.2f, 28f), marker, root.transform);
            CreateMarker("Heavy Resource Area", new Vector3(-36f, 1.2f, -36f), marker, root.transform);
            CreateMarker("Palm / Wood Area", new Vector3(38f, 1.2f, -42f), marker, root.transform);

            CreatePrimitive("Starter raft outline", PrimitiveType.Cube, new Vector3(0f, 1.05f, 52f), new Vector3(8f, 0.08f, 5f), wood, root.transform, Quaternion.identity);
        }

        private static void CreateMarker(string label, Vector3 position, Material material, Transform parent)
        {
            GameObject marker = CreatePrimitive(label, PrimitiveType.Cylinder, position, new Vector3(1.1f, 0.08f, 1.1f), material, parent, Quaternion.identity);
            TextMesh text = new GameObject($"{label} Label").AddComponent<TextMesh>();
            text.transform.SetParent(marker.transform);
            text.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            text.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);
            text.text = label;
            text.fontSize = 18;
            text.characterSize = 0.22f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private static void CreateStarterProps()
        {
            GameObject root = new GameObject("Starter Props - move or delete freely");
            TryPrefab("Assets/PalmTreePack/Prefabs/1/Palm_1_1.prefab", new Vector3(36f, 3f, -34f), Quaternion.identity, Vector3.one * 1.4f, root.transform);
            TryPrefab("Assets/PalmTreePack/Prefabs/2/Palm_2_1.prefab", new Vector3(47f, 3f, -20f), Quaternion.Euler(0f, 45f, 0f), Vector3.one * 1.2f, root.transform);
            TryPrefab("Assets/RockFREE/LODGroups/rock1_LOD0.prefab", new Vector3(-30f, 4f, -26f), Quaternion.identity, Vector3.one * 2.0f, root.transform);
            TryPrefab("Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigCrate_color01.prefab", new Vector3(-42f, 2f, 24f), Quaternion.Euler(0f, 20f, 0f), Vector3.one * 1.2f, root.transform);
            TryPrefab("Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigBarrel_color01.prefab", new Vector3(44f, 2f, 26f), Quaternion.Euler(0f, 0f, 90f), Vector3.one * 0.8f, root.transform);
            TryPrefab("Assets/AwesomeFreeScans/WoodLogs/Prefabs/LowEndPC/SM_AFS_Log01_LowEndPC.prefab", new Vector3(41f, 2f, 20f), Quaternion.Euler(0f, 90f, 90f), Vector3.one, root.transform);
        }

        internal static bool TryPrefab(string path, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return false;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            return true;
        }

        internal static GameObject PlacePrefab(string path, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return null;
            }

            GameObject root = GameObject.Find("Builder Placed Assets");
            if (root == null)
            {
                root = new GameObject("Builder Placed Assets");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Island Asset");
            instance.transform.SetParent(root.transform);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return instance;
        }

        private static void CreatePlayer(Terrain terrain)
        {
            Vector3 start = new Vector3(0f, 0f, 42f);
            start.y = terrain.SampleHeight(start) + terrain.transform.position.y + 0.25f;

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "First Person Player";
            player.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 180f, 0f));
            player.GetComponent<Renderer>().enabled = false;
            Object.DestroyImmediate(player.GetComponent<Collider>());

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);

            GameObject cameraObject = new GameObject("First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 68f;
            cameraObject.AddComponent<AudioListener>();

            DemoFirstPersonPlayer controller = player.AddComponent<DemoFirstPersonPlayer>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("groundMask").intValue = 1 << GroundLayer;
            serialized.FindProperty("startPosition").vector3Value = start;
            serialized.FindProperty("startRotation").quaternionValue = Quaternion.Euler(0f, 180f, 0f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType primitive, Vector3 position, Vector3 scale, Material material, Transform parent, Quaternion rotation)
        {
            GameObject obj = GameObject.CreatePrimitive(primitive);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }

        private static void CreateLighting()
        {
            GameObject sun = new GameObject("Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            sun.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.66f);
        }

        internal static Terrain GetEditableTerrain()
        {
            if (Selection.activeGameObject != null)
            {
                Terrain selected = Selection.activeGameObject.GetComponent<Terrain>();
                if (selected != null)
                {
                    return selected;
                }
            }

            GameObject named = GameObject.Find("EDIT ME - Island Terrain");
            if (named != null)
            {
                Terrain terrain = named.GetComponent<Terrain>();
                if (terrain != null)
                {
                    return terrain;
                }
            }

            return Terrain.activeTerrain;
        }

        internal static void SetOceanVisible(bool visible)
        {
            GameObject ocean = FindOcean();
            if (ocean == null && visible)
            {
                EnsureFolder(MaterialFolder);
                Material water = CreateMaterialAsset("Demo Water", new Color(0.05f, 0.33f, 0.55f, 0.48f), 0.55f, true);
                CreateOcean(water);
                ocean = FindOcean();
            }

            if (ocean != null)
            {
                ocean.SetActive(visible);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        private static GameObject FindOcean()
        {
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (IsSceneObject(obj) && (obj.name == "Ocean - waves, visual only" || obj.name == "Ocean - visual only, no collider"))
                {
                    return obj;
                }
            }

            return null;
        }

        internal static void ApplyLargeReadableIsland(Terrain terrain)
        {
            ApplyIslandPreset(terrain, 0.92f, 0.12f, 25f, 0.34f, 7f);
        }

        internal static void ApplyIslandPreset(Terrain terrain, float islandRadius, float shoreSoftness, float volcanoHeight, float volcanoRadius, float beachHeight)
        {
            TerrainData data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            float[,] heights = new float[resolution, resolution];
            float terrainBaseY = terrain.transform.position.y;
            float terrainHeight = data.size.y;
            float radiusValue = Mathf.Clamp(islandRadius, 0.45f, 0.98f);
            float softness = Mathf.Clamp(shoreSoftness, 0.035f, 0.22f);
            float volcanoRadiusValue = Mathf.Clamp(volcanoRadius, 0.16f, 0.58f);

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nx = x / (float)(resolution - 1) * 2f - 1f;
                    float nz = z / (float)(resolution - 1) * 2f - 1f;
                    float angle = Mathf.Atan2(nz, nx);
                    float radius = Mathf.Sqrt(nx * nx + nz * nz);
                    float edge = radiusValue + Mathf.Sin(angle * 2.7f) * 0.075f + Mathf.Cos(angle * 5.4f) * 0.045f;
                    float inside = SmoothStep01((edge - radius) / softness);
                    float center = Mathf.Clamp01(1f - radius / volcanoRadiusValue);
                    float crater = Mathf.Clamp01(1f - Mathf.Abs(radius - 0.15f) / 0.055f);
                    float beachSlope = Mathf.Lerp(2.2f, beachHeight, Mathf.Pow(1f - Mathf.Clamp01(radius / edge), 0.58f));
                    float volcano = Mathf.Pow(center, 1.9f) * volcanoHeight - crater * 4.2f;
                    float noise = (Mathf.PerlinNoise(x * 0.045f + 12.5f, z * 0.045f + 7.8f) - 0.5f) * 0.9f;
                    float worldY = Mathf.Lerp(-4.0f, beachSlope + volcano + noise, inside);
                    heights[z, x] = Mathf.Clamp01((worldY - terrainBaseY) / terrainHeight);
                }
            }

            data.SetHeights(0, 0, heights);
            PaintTerrain(data);
            terrain.Flush();
            MoveGameplayObjectsToTerrain(terrain);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        internal static void MoveGameplayObjectsToTerrain(Terrain terrain)
        {
            MoveObjectToTerrain("First Person Player", terrain, new Vector3(0f, 0f, 58f), 0.25f);
            MoveObjectToTerrain("Palm_1_1", terrain, new Vector3(46f, 0f, -42f), 0f);
            MoveObjectToTerrain("Palm_2_1", terrain, new Vector3(55f, 0f, -22f), 0f);
            MoveObjectToTerrain("rock1_LOD0", terrain, new Vector3(-36f, 0f, -28f), 0.15f);
            MoveObjectToTerrain("Prefab_BigCrate_color01", terrain, new Vector3(-54f, 0f, 32f), 0.1f);
            MoveObjectToTerrain("Prefab_BigBarrel_color01", terrain, new Vector3(54f, 0f, 32f), 0.1f);
            MoveObjectToTerrain("SM_AFS_Log01_LowEndPC", terrain, new Vector3(48f, 0f, 24f), 0.1f);
        }

        private static void MoveObjectToTerrain(string objectNameContains, Terrain terrain, Vector3 desiredPosition, float yOffset)
        {
            GameObject target = FindObjectByNameContains(objectNameContains);
            if (target == null)
            {
                return;
            }

            float y = terrain.SampleHeight(desiredPosition) + terrain.transform.position.y + yOffset;
            target.transform.position = new Vector3(desiredPosition.x, y, desiredPosition.z);

            DemoFirstPersonPlayer player = target.GetComponent<DemoFirstPersonPlayer>();
            if (player != null)
            {
                SerializedObject serialized = new SerializedObject(player);
                serialized.FindProperty("startPosition").vector3Value = target.transform.position;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject FindObjectByNameContains(string text)
        {
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (IsSceneObject(obj) && obj.activeInHierarchy && obj.name.Contains(text))
                {
                    return obj;
                }
            }

            return null;
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Material CreateMaterialAsset(string name, Color color, float smoothness, bool transparent)
        {
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                name = name
            };
            SetColor(material, color, "_BaseColor", "_Color");
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (transparent)
            {
                SetFloat(material, 1f, "_Surface");
                SetFloat(material, 0f, "_ZWrite");
                SetFloat(material, (float)UnityEngine.Rendering.BlendMode.SrcAlpha, "_SrcBlend");
                SetFloat(material, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "_DstBlend");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            string path = AssetDatabase.GenerateUniqueAssetPath($"{MaterialFolder}/{name}.mat");
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Texture2D CreateNoiseTexture(string name, Color dark, Color light)
        {
            Texture2D texture = new Texture2D(96, 96, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat
            };

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
                    texture.SetPixel(x, y, Color.Lerp(dark, light, n));
                }
            }

            texture.Apply();
            return texture;
        }

        private static Texture GetTexture(Material material, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    Texture texture = material.GetTexture(name);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static void SetTexture(Material material, Texture texture, params string[] names)
        {
            if (texture == null)
            {
                return;
            }

            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetTexture(name, texture);
                }
            }
        }

        private static Color GetColor(Material material, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    return material.GetColor(name);
                }
            }

            return Color.white;
        }

        private static void SetColor(Material material, Color color, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetColor(name, color);
                }
            }
        }

        private static void SetFloat(Material material, float value, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetFloat(name, value);
                }
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
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

    public sealed class EditableIslandEditingWindow : EditorWindow
    {
        private enum BuilderTool
        {
            None,
            DrawIslandOutline,
            EraseLand,
            AddLand,
            LowerLand,
            Smooth,
            FlattenBeach,
            PaintDrySand,
            PaintWetSand,
            PaintVolcanic,
            PaintGrass,
            PlacePalm,
            PlaceRock,
            PlaceCrate,
            PlaceBarrel,
            PlaceLog
        }

        private float islandRadius = 0.92f;
        private float shoreSoftness = 0.12f;
        private float volcanoHeight = 25f;
        private float volcanoRadius = 0.34f;
        private float beachHeight = 7f;
        private BuilderTool activeTool = BuilderTool.None;
        private float brushRadius = 12f;
        private float brushStrength = 0.45f;
        private float flattenHeight = 1.3f;
        private Vector2 scroll;
        private bool showAdvancedShape;

        public static void ShowWindow()
        {
            StableIslandBuilderWindow.ShowWindow();
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGui;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
        }

        private void OnGUI()
        {
            StableIslandBuilderWindow.ShowWindow();
            Close();
            return;

#pragma warning disable CS0162
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("This Will Float Island Builder", EditorStyles.boldLabel);
            DrawModeBanner();

            EditorGUILayout.LabelField("1. Ocean Canvas", EditorStyles.boldLabel);
            if (GUILayout.Button("RESET HUGE OCEAN CANVAS", GUILayout.Height(44f)))
            {
                Terrain terrain = EditableIslandWorkspaceBuilder.RebuildOceanSculptCanvas();
                if (terrain != null)
                {
                    Selection.activeGameObject = terrain.gameObject;
                    FrameTerrainTopDown(terrain);
                    ShowNotification(new GUIContent("Ocean canvas ready. Pick Draw Island Outline."));
                }
            }

            if (GUILayout.Button("CREATE BIG EDITABLE ISLAND", GUILayout.Height(44f)))
            {
                Terrain terrain = EditableIslandWorkspaceBuilder.RebuildOceanSculptCanvas();
                if (terrain != null)
                {
                    Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Create Big Editable Island");
                    EditableIslandWorkspaceBuilder.ApplyLargeReadableIsland(terrain);
                    EditableIslandWorkspaceBuilder.SetOceanVisible(true);
                    Selection.activeGameObject = terrain.gameObject;
                    FrameTerrainTopDown(terrain);
                    ShowNotification(new GUIContent("Big island created. Use brushes to edit it."));
                }
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Water", GUILayout.Height(30f)))
            {
                EditableIslandWorkspaceBuilder.SetOceanVisible(true);
            }

            if (GUILayout.Button("Hide Water", GUILayout.Height(30f)))
            {
                EditableIslandWorkspaceBuilder.SetOceanVisible(false);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Frame Island", GUILayout.Height(28f)))
            {
                Terrain terrain = EditableIslandWorkspaceBuilder.GetEditableTerrain();
                if (terrain != null)
                {
                    Selection.activeGameObject = terrain.gameObject;
                    FrameTerrainTopDown(terrain);
                }
            }

            if (GUILayout.Button("Flatter Island", GUILayout.Height(28f)))
            {
                islandRadius = 0.94f;
                shoreSoftness = 0.18f;
                volcanoHeight = 12f;
                volcanoRadius = 0.28f;
                beachHeight = 4.5f;
                ApplyCurrentPreset(false);
            }
            EditorGUILayout.EndHorizontal();

            showAdvancedShape = EditorGUILayout.Foldout(showAdvancedShape, "Advanced island shape sliders");
            if (showAdvancedShape)
            {
                islandRadius = EditorGUILayout.Slider("섬 넓이", islandRadius, 0.55f, 0.98f);
                shoreSoftness = EditorGUILayout.Slider("해변 폭", shoreSoftness, 0.04f, 0.22f);
                volcanoHeight = EditorGUILayout.Slider("화산 높이", volcanoHeight, 8f, 34f);
                volcanoRadius = EditorGUILayout.Slider("화산 너비", volcanoRadius, 0.18f, 0.55f);
                beachHeight = EditorGUILayout.Slider("해변 높이", beachHeight, 2f, 10f);
                if (GUILayout.Button("Apply Slider Shape", GUILayout.Height(28f)))
                {
                    ApplyCurrentPreset(false);
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("2. 브러시", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("아래 툴을 고르고 Scene 뷰에서 왼클릭/드래그. 화면 회전은 Option/Alt + 드래그.", MessageType.None);

            brushRadius = EditorGUILayout.Slider("브러시 크기", brushRadius, 2f, 55f);
            brushStrength = EditorGUILayout.Slider("브러시 세기", brushStrength, 0.05f, 1f);
            flattenHeight = EditorGUILayout.Slider("평평한 해변 높이", flattenHeight, 0.3f, 5f);

            EditorGUILayout.LabelField("3. Island Shape", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Draw Land", BuilderTool.DrawIslandOutline);
            ToolButton("Erase To Water", BuilderTool.EraseLand);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("4. Height Tools", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Raise / Mountain", BuilderTool.AddLand);
            ToolButton("Lower / Dig", BuilderTool.LowerLand);
            ToolButton("Smooth", BuilderTool.Smooth);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            ToolButton("Flatten Beach", BuilderTool.FlattenBeach);
            ToolButton("Stop Tool", BuilderTool.None);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("5. Paint Texture", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Dry Sand", BuilderTool.PaintDrySand);
            ToolButton("Wet Sand", BuilderTool.PaintWetSand);
            ToolButton("Volcanic", BuilderTool.PaintVolcanic);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            ToolButton("Grass", BuilderTool.PaintGrass);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("6. Place Assets", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Palm", BuilderTool.PlacePalm);
            ToolButton("Rock", BuilderTool.PlaceRock);
            ToolButton("Crate", BuilderTool.PlaceCrate);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            ToolButton("Barrel", BuilderTool.PlaceBarrel);
            ToolButton("Log", BuilderTool.PlaceLog);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox($"현재 선택: {GetToolLabel(activeTool)}\nScene 뷰에서 왼클릭/드래그하면 적용됩니다. 물은 시각용이고, 땅 편집은 EDIT ME - Island Terrain에 직접 저장됩니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
#pragma warning restore CS0162
        }

        private void DrawModeBanner()
        {
            Color previous = GUI.backgroundColor;
            GUI.backgroundColor = activeTool == BuilderTool.None ? new Color(0.45f, 0.45f, 0.45f) : new Color(0.15f, 0.55f, 0.95f);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"ACTIVE TOOL: {GetToolLabel(activeTool)}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("왼클릭/드래그로 바로 적용. Stop Tool을 누르면 편집 종료.");
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = previous;
        }

        private static string GetToolLabel(BuilderTool tool)
        {
            return tool switch
            {
                BuilderTool.DrawIslandOutline => "섬 외곽 그리기",
                BuilderTool.EraseLand => "지우개",
                BuilderTool.AddLand => "땅 올리기",
                BuilderTool.LowerLand => "땅 깎기",
                BuilderTool.Smooth => "부드럽게",
                BuilderTool.FlattenBeach => "해변 평탄화",
                BuilderTool.PaintDrySand => "마른 모래 칠하기",
                BuilderTool.PaintWetSand => "젖은 모래 칠하기",
                BuilderTool.PaintVolcanic => "화산 흙 칠하기",
                BuilderTool.PaintGrass => "풀 칠하기",
                BuilderTool.PlacePalm => "야자수 놓기",
                BuilderTool.PlaceRock => "바위 놓기",
                BuilderTool.PlaceCrate => "상자 놓기",
                BuilderTool.PlaceBarrel => "통 놓기",
                BuilderTool.PlaceLog => "통나무 놓기",
                _ => "없음"
            };
        }

        private void ToolButton(string label, BuilderTool tool)
        {
            Color previous = GUI.backgroundColor;
            if (activeTool == tool)
            {
                GUI.backgroundColor = new Color(0.35f, 0.65f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Height(28f)))
            {
                activeTool = tool;
                Tools.current = Tool.None;
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = previous;
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            if (activeTool == BuilderTool.None)
            {
                return;
            }

            Terrain terrain = EditableIslandWorkspaceBuilder.GetEditableTerrain();
            if (terrain == null)
            {
                return;
            }

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
            if (!TryGetBuildPoint(ray, terrain, out Vector3 point))
            {
                return;
            }

            Handles.color = GetHandleColor(activeTool);
            float previewY = activeTool == BuilderTool.DrawIslandOutline || activeTool == BuilderTool.EraseLand ? 0.08f : point.y + 0.05f;
            Handles.DrawWireDisc(new Vector3(point.x, previewY, point.z), Vector3.up, IsPlaceTool(activeTool) ? 1.5f : brushRadius);
            sceneView.Repaint();

            if ((current.type != EventType.MouseDown && current.type != EventType.MouseDrag) || current.button != 0 || current.alt)
            {
                return;
            }

            if (IsPlaceTool(activeTool) && current.type == EventType.MouseDrag)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Island Builder Tool");
            if (IsPlaceTool(activeTool))
            {
                PlaceAssetAt(point, terrain);
            }
            else if (IsPaintTool(activeTool))
            {
                PaintTerrainAt(terrain, point, activeTool);
            }
            else
            {
                SculptTerrainAt(terrain, point, current.shift ? Invert(activeTool) : activeTool);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            current.Use();
        }

        private static BuilderTool Invert(BuilderTool tool)
        {
            if (tool == BuilderTool.AddLand)
            {
                return BuilderTool.LowerLand;
            }

            if (tool == BuilderTool.LowerLand)
            {
                return BuilderTool.AddLand;
            }

            return tool;
        }

        private bool TryGetBuildPoint(Ray ray, Terrain terrain, out Vector3 point)
        {
            int terrainMask = 1 << terrain.gameObject.layer;
            if (Physics.Raycast(ray, out RaycastHit terrainHit, 10000f, terrainMask, QueryTriggerInteraction.Ignore))
            {
                point = terrainHit.point;
                return IsInsideTerrain(terrain, point);
            }

            if (Physics.Raycast(ray, out RaycastHit hit, 10000f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                return IsInsideTerrain(terrain, point);
            }

            Plane plane = new Plane(Vector3.up, Vector3.zero);
            if (plane.Raycast(ray, out float enter))
            {
                point = ray.GetPoint(enter);
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                return IsInsideTerrain(terrain, point);
            }

            point = Vector3.zero;
            return false;
        }

        private static bool IsInsideTerrain(Terrain terrain, Vector3 point)
        {
            Vector3 local = point - terrain.transform.position;
            return local.x >= 0f && local.z >= 0f && local.x <= terrain.terrainData.size.x && local.z <= terrain.terrainData.size.z;
        }

        private bool IsPlaceTool(BuilderTool tool)
        {
            return tool == BuilderTool.PlacePalm || tool == BuilderTool.PlaceRock || tool == BuilderTool.PlaceCrate || tool == BuilderTool.PlaceBarrel || tool == BuilderTool.PlaceLog;
        }

        private static Color GetHandleColor(BuilderTool tool)
        {
            if (tool == BuilderTool.DrawIslandOutline)
            {
                return new Color(0.95f, 0.72f, 0.25f, 0.95f);
            }

            if (tool == BuilderTool.EraseLand)
            {
                return new Color(1f, 0.25f, 0.18f, 0.95f);
            }

            if (tool == BuilderTool.PaintDrySand || tool == BuilderTool.PaintWetSand || tool == BuilderTool.PaintVolcanic || tool == BuilderTool.PaintGrass)
            {
                return new Color(0.35f, 1f, 0.45f, 0.9f);
            }

            if (tool == BuilderTool.PlacePalm || tool == BuilderTool.PlaceRock || tool == BuilderTool.PlaceCrate || tool == BuilderTool.PlaceBarrel || tool == BuilderTool.PlaceLog)
            {
                return new Color(1f, 0.85f, 0.25f, 0.9f);
            }

            return new Color(0.2f, 0.7f, 1f, 0.85f);
        }

        private bool IsPaintTool(BuilderTool tool)
        {
            return tool == BuilderTool.PaintDrySand || tool == BuilderTool.PaintWetSand || tool == BuilderTool.PaintVolcanic || tool == BuilderTool.PaintGrass;
        }

        private void SculptTerrainAt(Terrain terrain, Vector3 worldPoint, BuilderTool tool)
        {
            TerrainData data = terrain.terrainData;
            Vector3 local = worldPoint - terrain.transform.position;
            int centerX = Mathf.RoundToInt(local.x / data.size.x * (data.heightmapResolution - 1));
            int centerZ = Mathf.RoundToInt(local.z / data.size.z * (data.heightmapResolution - 1));
            int radius = Mathf.Max(1, Mathf.RoundToInt(brushRadius / data.size.x * data.heightmapResolution));
            int startX = Mathf.Clamp(centerX - radius, 0, data.heightmapResolution - 1);
            int startZ = Mathf.Clamp(centerZ - radius, 0, data.heightmapResolution - 1);
            int endX = Mathf.Clamp(centerX + radius, 0, data.heightmapResolution - 1);
            int endZ = Mathf.Clamp(centerZ + radius, 0, data.heightmapResolution - 1);
            int width = endX - startX + 1;
            int height = endZ - startZ + 1;
            float[,] heights = data.GetHeights(startX, startZ, width, height);
            float normalizedDelta = brushStrength * 2.8f / data.size.y;
            float target = Mathf.Clamp01((flattenHeight - terrain.transform.position.y) / data.size.y);
            float addLandTarget = Mathf.Clamp01((Mathf.Max(flattenHeight, 1.8f) - terrain.transform.position.y) / data.size.y);
            float outlineTarget = Mathf.Clamp01((2.4f - terrain.transform.position.y) / data.size.y);
            float waterCutTarget = Mathf.Clamp01((-3.6f - terrain.transform.position.y) / data.size.y);

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (startX + x - centerX) / (float)radius;
                    float dz = (startZ + z - centerZ) / (float)radius;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz));
                    falloff = falloff * falloff * (3f - 2f * falloff);

                    if (tool == BuilderTool.DrawIslandOutline)
                    {
                        float raised = Mathf.Max(heights[z, x], outlineTarget);
                        heights[z, x] = Mathf.Lerp(heights[z, x], raised, falloff * Mathf.Clamp01(brushStrength * 1.25f));
                    }
                    else if (tool == BuilderTool.EraseLand)
                    {
                        heights[z, x] = Mathf.Lerp(heights[z, x], waterCutTarget, falloff * Mathf.Clamp01(brushStrength * 1.4f));
                    }
                    else if (tool == BuilderTool.AddLand)
                    {
                        float raised = Mathf.Max(heights[z, x] + normalizedDelta, addLandTarget);
                        heights[z, x] = Mathf.Lerp(heights[z, x], Mathf.Clamp01(raised), falloff);
                    }
                    else if (tool == BuilderTool.LowerLand)
                    {
                        float lowered = Mathf.Min(heights[z, x] - normalizedDelta, waterCutTarget);
                        heights[z, x] = Mathf.Lerp(heights[z, x], Mathf.Clamp01(lowered), falloff);
                    }
                    else if (tool == BuilderTool.FlattenBeach)
                    {
                        heights[z, x] = Mathf.Lerp(heights[z, x], target, falloff * brushStrength);
                    }
                    else if (tool == BuilderTool.Smooth)
                    {
                        heights[z, x] = Mathf.Lerp(heights[z, x], AverageHeight(heights, x, z), falloff * brushStrength);
                    }
                }
            }

            data.SetHeights(startX, startZ, heights);
            terrain.Flush();
            EditorUtility.SetDirty(data);

            if (tool == BuilderTool.DrawIslandOutline)
            {
                PaintTerrainAt(terrain, worldPoint, BuilderTool.PaintDrySand);
                PaintRingAt(terrain, worldPoint, BuilderTool.PaintWetSand, brushRadius * 1.1f, brushRadius * 0.72f);
            }
            else if (tool == BuilderTool.EraseLand)
            {
                PaintTerrainAt(terrain, worldPoint, BuilderTool.PaintWetSand);
            }
            else if (tool == BuilderTool.AddLand)
            {
                PaintTerrainAt(terrain, worldPoint, BuilderTool.PaintDrySand);
            }
            else if (tool == BuilderTool.FlattenBeach)
            {
                PaintTerrainAt(terrain, worldPoint, BuilderTool.PaintDrySand);
            }
            else if (tool == BuilderTool.LowerLand)
            {
                PaintTerrainAt(terrain, worldPoint, BuilderTool.PaintWetSand);
            }
        }

        private static float AverageHeight(float[,] heights, int x, int z)
        {
            float total = 0f;
            int count = 0;
            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int sx = Mathf.Clamp(x + dx, 0, heights.GetLength(1) - 1);
                    int sz = Mathf.Clamp(z + dz, 0, heights.GetLength(0) - 1);
                    total += heights[sz, sx];
                    count++;
                }
            }

            return total / count;
        }

        private void PaintTerrainAt(Terrain terrain, Vector3 worldPoint, BuilderTool tool)
        {
            TerrainData data = terrain.terrainData;
            int layer = GetPaintLayer(tool);
            Vector3 local = worldPoint - terrain.transform.position;
            int centerX = Mathf.RoundToInt(local.x / data.size.x * data.alphamapWidth);
            int centerZ = Mathf.RoundToInt(local.z / data.size.z * data.alphamapHeight);
            int radius = Mathf.Max(1, Mathf.RoundToInt(brushRadius / data.size.x * data.alphamapWidth));
            int startX = Mathf.Clamp(centerX - radius, 0, data.alphamapWidth - 1);
            int startZ = Mathf.Clamp(centerZ - radius, 0, data.alphamapHeight - 1);
            int endX = Mathf.Clamp(centerX + radius, 0, data.alphamapWidth - 1);
            int endZ = Mathf.Clamp(centerZ + radius, 0, data.alphamapHeight - 1);
            int width = endX - startX + 1;
            int height = endZ - startZ + 1;
            float[,,] maps = data.GetAlphamaps(startX, startZ, width, height);
            int layers = data.alphamapLayers;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (startX + x - centerX) / (float)radius;
                    float dz = (startZ + z - centerZ) / (float)radius;
                    float falloff = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz)) * brushStrength;
                    for (int i = 0; i < layers; i++)
                    {
                        maps[z, x, i] *= 1f - falloff;
                    }

                    maps[z, x, layer] += falloff;
                    float sum = 0f;
                    for (int i = 0; i < layers; i++)
                    {
                        sum += maps[z, x, i];
                    }

                    for (int i = 0; i < layers; i++)
                    {
                        maps[z, x, i] /= Mathf.Max(0.001f, sum);
                    }
                }
            }

            data.SetAlphamaps(startX, startZ, maps);
            EditorUtility.SetDirty(data);
        }

        private void PaintRingAt(Terrain terrain, Vector3 worldPoint, BuilderTool tool, float outerRadius, float innerRadius)
        {
            TerrainData data = terrain.terrainData;
            int layer = GetPaintLayer(tool);
            Vector3 local = worldPoint - terrain.transform.position;
            int centerX = Mathf.RoundToInt(local.x / data.size.x * data.alphamapWidth);
            int centerZ = Mathf.RoundToInt(local.z / data.size.z * data.alphamapHeight);
            int outer = Mathf.Max(1, Mathf.RoundToInt(outerRadius / data.size.x * data.alphamapWidth));
            int inner = Mathf.Max(0, Mathf.RoundToInt(innerRadius / data.size.x * data.alphamapWidth));
            int startX = Mathf.Clamp(centerX - outer, 0, data.alphamapWidth - 1);
            int startZ = Mathf.Clamp(centerZ - outer, 0, data.alphamapHeight - 1);
            int endX = Mathf.Clamp(centerX + outer, 0, data.alphamapWidth - 1);
            int endZ = Mathf.Clamp(centerZ + outer, 0, data.alphamapHeight - 1);
            int width = endX - startX + 1;
            int height = endZ - startZ + 1;
            float[,,] maps = data.GetAlphamaps(startX, startZ, width, height);
            int layers = data.alphamapLayers;

            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float distance = Vector2.Distance(new Vector2(startX + x, startZ + z), new Vector2(centerX, centerZ));
                    if (distance < inner || distance > outer)
                    {
                        continue;
                    }

                    float ringFalloff = Mathf.Clamp01(1f - Mathf.Abs(distance - (inner + outer) * 0.5f) / Mathf.Max(1f, (outer - inner) * 0.5f));
                    float strength = ringFalloff * brushStrength;
                    for (int i = 0; i < layers; i++)
                    {
                        maps[z, x, i] *= 1f - strength;
                    }

                    maps[z, x, layer] += strength;
                    float sum = 0f;
                    for (int i = 0; i < layers; i++)
                    {
                        sum += maps[z, x, i];
                    }

                    for (int i = 0; i < layers; i++)
                    {
                        maps[z, x, i] /= Mathf.Max(0.001f, sum);
                    }
                }
            }

            data.SetAlphamaps(startX, startZ, maps);
            EditorUtility.SetDirty(data);
        }

        private static int GetPaintLayer(BuilderTool tool)
        {
            return tool switch
            {
                BuilderTool.PaintDrySand => 0,
                BuilderTool.PaintWetSand => 1,
                BuilderTool.PaintVolcanic => 2,
                BuilderTool.PaintGrass => 3,
                _ => 0
            };
        }

        private void PlaceAssetAt(Vector3 point, Terrain terrain)
        {
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            string path = "";
            Vector3 scale = Vector3.one;
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            if (activeTool == BuilderTool.PlacePalm)
            {
                path = "Assets/PalmTreePack/Prefabs/1/Palm_1_1.prefab";
                scale = Vector3.one * Random.Range(1.0f, 1.5f);
            }
            else if (activeTool == BuilderTool.PlaceRock)
            {
                path = "Assets/RockFREE/LODGroups/rock1_LOD0.prefab";
                scale = Vector3.one * Random.Range(1.2f, 2.2f);
            }
            else if (activeTool == BuilderTool.PlaceCrate)
            {
                path = "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigCrate_color01.prefab";
                scale = Vector3.one * 1.1f;
            }
            else if (activeTool == BuilderTool.PlaceBarrel)
            {
                path = "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigBarrel_color01.prefab";
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);
                scale = Vector3.one * 0.8f;
            }
            else if (activeTool == BuilderTool.PlaceLog)
            {
                path = "Assets/AwesomeFreeScans/WoodLogs/Prefabs/LowEndPC/SM_AFS_Log01_LowEndPC.prefab";
                rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 90f);
                scale = Vector3.one;
            }

            EditableIslandWorkspaceBuilder.PlacePrefab(path, point, rotation, scale);
        }

        private void ApplyCurrentPreset(bool showWaterAfter)
        {
            Terrain terrain = EditableIslandWorkspaceBuilder.GetEditableTerrain();
            if (terrain == null)
            {
                EditorUtility.DisplayDialog("This Will Float DEMO", "No editable terrain found. Run Build Editable Island Workspace first.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(terrain.terrainData, "Apply Island Shape");
            EditableIslandWorkspaceBuilder.ApplyIslandPreset(terrain, islandRadius, shoreSoftness, volcanoHeight, volcanoRadius, beachHeight);
            EditableIslandWorkspaceBuilder.SetOceanVisible(showWaterAfter);
            Selection.activeGameObject = terrain.gameObject;
            FrameTerrainTopDown(terrain);
        }

        private static void FrameTerrainTopDown(Terrain terrain)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
            {
                return;
            }

            Vector3 center = terrain.transform.position + terrain.terrainData.size * 0.5f;
            view.LookAt(center + Vector3.up * 10f, Quaternion.Euler(70f, 0f, 0f), terrain.terrainData.size.x * 0.55f);
        }
    }
}
