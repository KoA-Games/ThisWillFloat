using System.Collections.Generic;
using ThisWillFloat.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThisWillFloat.EditorTools
{
    public static class PrototypeSceneBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/Prototype_Buoyancy.unity";
        private const string DemoScenePath = "Assets/_Project/Scenes/V02_Demo_Loop.unity";
        private static readonly Vector3 DemoSpawnPosition = new Vector3(82f, 1.2f, -58f);
        private static readonly Quaternion DemoSpawnRotation = Quaternion.Euler(0f, -90f, 0f);
        private const string CargoMaterialPath = "Assets/Crates And Barrels Pack Volume 1 - Free Version/Materials/Mat_CratesAndBarrelsVol1_01.mat";
        private const string CargoAlbedoPath = "Assets/Crates And Barrels Pack Volume 1 - Free Version/Textures/Tex_CratesAndBarrelsVol1_A_color01.tga";
        private const string CargoNormalPath = "Assets/Crates And Barrels Pack Volume 1 - Free Version/Textures/Tex_CratesAndBarrelsVol1_N.tga";
        private const string CargoOcclusionPath = "Assets/Crates And Barrels Pack Volume 1 - Free Version/Textures/Tex_CratesAndBarrelsVol1_O.tga";
        private const string CargoMaskPath = "Assets/Crates And Barrels Pack Volume 1 - Free Version/Textures/Tex_CratesAndBarrelsVol1_M.tga";

        [MenuItem("This Will Float/Build Prototype Buoyancy Scene")]
        public static void BuildScene()
        {
            BuildPrototypeScene(ScenePath, false);
        }

        [MenuItem("This Will Float/Build V0.2 Demo Scene")]
        public static void BuildV02DemoScene()
        {
            BuildPrototypeScene(DemoScenePath, true);
        }

        private static void BuildPrototypeScene(string scenePath, bool includeDemoLoop)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            EnsureProjectFolders();
            FixCargoAssetMaterials(false);

            WaveSurface waveSurface = CreateWater();
            List<SectionConnector> connectors = new List<SectionConnector>();

            PhysicsMaterial raftFriction = CreatePhysicsMaterial("Raft High Friction", 1.4f, 1.2f);
            PhysicsMaterial cargoFriction = CreatePhysicsMaterial("Cargo High Friction", 1.6f, 1.4f);

            RaftSection front = CreateSection("Front Raft Section", new Vector3(0f, 1.25f, 1.55f), new Vector3(3f, 0.6f, 2.95f), 20f, waveSurface, new Color(0.58f, 0.43f, 0.23f), raftFriction);
            RaftSection rear = CreateSection("Rear Raft Section", new Vector3(0f, 1.25f, -1.55f), new Vector3(3f, 0.6f, 2.95f), 20f, waveSurface, new Color(0.52f, 0.37f, 0.19f), raftFriction);

            SectionConnector weakConnector = CreateConnector("Weak Center Connector", front, rear, new Vector3(0f, 1.2f, 0f), ConnectorType.LooseStack, true);
            connectors.Add(weakConnector);

            CreateSail(front);
            Transform leftCargoPoint = CreateCargoPoint("Left Cargo Drop", front.transform, new Vector3(-0.85f, 0.5f, -0.25f));
            Transform rightCargoPoint = CreateCargoPoint("Right Cargo Drop", front.transform, new Vector3(0.85f, 0.5f, -0.25f));

            CreateIsland();
            Transform cameraTarget = CreateCameraAndLight();
            cameraTarget.position = new Vector3(0f, 1f, 0f);

            Material cargoMaterial = CreateMaterial("Heavy Cargo Material", new Color(0.12f, 0.45f, 0.28f));
            FailurePrototypeController controller = new GameObject("Failure Prototype Controller").AddComponent<FailurePrototypeController>();
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("waveSurface").objectReferenceValue = waveSurface;
            serializedController.FindProperty("frontSection").objectReferenceValue = front;
            serializedController.FindProperty("rearSection").objectReferenceValue = rear;
            serializedController.FindProperty("weakConnector").objectReferenceValue = weakConnector;
            serializedController.FindProperty("leftCargoPoint").objectReferenceValue = leftCargoPoint;
            serializedController.FindProperty("rightCargoPoint").objectReferenceValue = rightCargoPoint;
            serializedController.FindProperty("cargoMass").floatValue = 8.5f;
            serializedController.FindProperty("connectorArmDelay").floatValue = 10f;
            serializedController.FindProperty("presetCargoInterval").floatValue = 0.75f;
            serializedController.FindProperty("heavySinkSpeed").floatValue = 0.055f;
            serializedController.FindProperty("scriptedBreakDelay").floatValue = 2f;
            serializedController.FindProperty("scriptedBreakDuration").floatValue = 7f;
            serializedController.FindProperty("cargoPiecesPerPress").intValue = 3;
            serializedController.FindProperty("cargoAccidentThreshold").intValue = 4;
            serializedController.FindProperty("cargoWarningSeconds").floatValue = 2.25f;
            serializedController.FindProperty("cargoAccidentSinkDuration").floatValue = 7.5f;
            serializedController.FindProperty("cargoAccidentTorque").floatValue = 20f;
            serializedController.FindProperty("cargoDeckSurfaceY").floatValue = 0.17f;
            serializedController.FindProperty("cargoStackLayerHeight").floatValue = 0.34f;
            SetObjectArray(serializedController.FindProperty("cargoPrefabs"), LoadCargoPrefabs());
            serializedController.FindProperty("cargoMaterial").objectReferenceValue = cargoMaterial;
            serializedController.FindProperty("cargoPhysicsMaterial").objectReferenceValue = cargoFriction;
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            PrototypeStatusDisplay display = new GameObject("Prototype Status Display").AddComponent<PrototypeStatusDisplay>();
            display.Watch(front.BuoyancyBody, connectors.ToArray());
            display.WatchController(controller);

            if (includeDemoLoop)
            {
                display.gameObject.SetActive(false);
                CreateV02DemoLoop(front, rear, weakConnector, controller, cameraTarget);
            }

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            if (includeDemoLoop)
            {
                SetSceneViewForDemoPreview();
            }
            EditorUtility.DisplayDialog("This Will Float", $"Prototype scene created:\n{scenePath}", "Open");
        }

        [MenuItem("This Will Float/Fix Cargo Asset Materials")]
        public static void FixCargoAssetMaterials()
        {
            FixCargoAssetMaterials(true);
        }

        private static void EnsureProjectFolders()
        {
            CreateFolderIfMissing("Assets/_Project");
            CreateFolderIfMissing("Assets/_Project/Scenes");
            CreateFolderIfMissing("Assets/_Project/Scripts");
            CreateFolderIfMissing("Assets/_Project/Editor");
        }

        private static GameObject[] LoadCargoPrefabs()
        {
            string[] prefabPaths =
            {
                "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigCrate_color01.prefab",
                "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_SmallCrate01_color01.prefab",
                "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_SmallCrate02_color01.prefab",
                "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_SmallBarrel_color01.prefab",
                "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigBarrel_color01.prefab"
            };

            List<GameObject> prefabs = new List<GameObject>();
            foreach (string path in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                }
            }

            return prefabs.ToArray();
        }

        private static void FixCargoAssetMaterials(bool showDialog)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(CargoMaterialPath);
            if (material == null)
            {
                if (showDialog)
                {
                    EditorUtility.DisplayDialog("This Will Float", "Cargo material was not found. Check that the Crates And Barrels asset is imported.", "OK");
                }

                return;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader != null)
            {
                material.shader = shader;
            }

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(CargoAlbedoPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(CargoNormalPath);
            Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(CargoOcclusionPath);
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(CargoMaskPath);

            SetMaterialTexture(material, "_BaseMap", albedo);
            SetMaterialTexture(material, "_MainTex", albedo);
            SetMaterialTexture(material, "_BumpMap", normal);
            SetMaterialTexture(material, "_OcclusionMap", occlusion);
            SetMaterialTexture(material, "_MetallicGlossMap", mask);

            SetMaterialColor(material, "_BaseColor", Color.white);
            SetMaterialColor(material, "_Color", Color.white);
            SetMaterialFloat(material, "_Metallic", 0f);
            SetMaterialFloat(material, "_Smoothness", 0.32f);
            SetMaterialFloat(material, "_Glossiness", 0.32f);
            SetMaterialFloat(material, "_BumpScale", 1f);
            SetMaterialFloat(material, "_OcclusionStrength", 0.85f);

            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            if (mask != null)
            {
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                material.EnableKeyword("_METALLICGLOSSMAP");
            }

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();

            if (showDialog)
            {
                EditorUtility.DisplayDialog("This Will Float", "Cargo materials were converted for URP.", "OK");
            }
        }

        private static void SetMaterialTexture(Material material, string propertyName, Texture texture)
        {
            if (texture != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, texture);
            }
        }

        private static void SetMaterialFloat(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetMaterialColor(Material material, string propertyName, Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetObjectArray(SerializedProperty property, Object[] values)
        {
            if (property == null)
            {
                return;
            }

            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }
        }

        private static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = System.IO.Path.GetFileName(path);

            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderIfMissing(parent);
            }

            AssetDatabase.CreateFolder(parent ?? "Assets", folder);
        }

        private static void CreateV02DemoLoop(RaftSection front, RaftSection rear, SectionConnector connector, FailurePrototypeController physicsController, Transform cameraTarget)
        {
            Material sandMaterial = CreateSandMaterial();
            Material markerMaterial = CreateMaterial("Demo Slot Marker Material", new Color(0.1f, 0.9f, 0.35f, 0.5f));
            Material buttonMaterial = CreateMaterial("Launch Button Material", new Color(0.1f, 0.7f, 0.25f));
            Material playerMaterial = CreateMaterial("Prototype Player Material", new Color(0.92f, 0.76f, 0.32f));

            CreatePlayableStartIsland(sandMaterial);

            DemoGameController gameController = new GameObject("V0.2 Demo Game Controller").AddComponent<DemoGameController>();

            DemoPlayerController player = CreateDemoPlayer(playerMaterial, gameController, cameraTarget);
            DemoLaunchButton buildButton = CreateDemoButton("Build Grid Button Station", new Vector3(74f, 1.05f, -51f), buttonMaterial, gameController, DemoButtonAction.OpenBuildGrid);
            DemoLaunchButton launchButton = CreateDemoButton("Launch Button Station", new Vector3(90f, 1.05f, -51f), buttonMaterial, gameController, DemoButtonAction.Launch);
            GameObject buildGridRoot = new GameObject("Beach Boat Blueprint Grid");
            buildGridRoot.transform.position = Vector3.zero;
            DemoRaftSlot[] slots = CreateDemoRaftSlots(front, rear, physicsController, markerMaterial, buildGridRoot.transform);
            buildGridRoot.SetActive(false);
            DemoCarryableItem[] items = CreateDemoMaterials();

            buildButton.Initialize(gameController, DemoButtonAction.OpenBuildGrid);
            launchButton.Initialize(gameController, DemoButtonAction.Launch);

            SerializedObject serializedGame = new SerializedObject(gameController);
            serializedGame.FindProperty("physicsController").objectReferenceValue = physicsController;
            serializedGame.FindProperty("frontSection").objectReferenceValue = front;
            serializedGame.FindProperty("rearSection").objectReferenceValue = rear;
            serializedGame.FindProperty("connector").objectReferenceValue = connector;
            serializedGame.FindProperty("player").objectReferenceValue = player;
            serializedGame.FindProperty("buildButton").objectReferenceValue = buildButton;
            serializedGame.FindProperty("launchButton").objectReferenceValue = launchButton;
            serializedGame.FindProperty("buildGridRoot").objectReferenceValue = buildGridRoot;
            SetObjectArray(serializedGame.FindProperty("slots"), slots);
            SetObjectArray(serializedGame.FindProperty("items"), items);
            serializedGame.FindProperty("raftForward").vector3Value = Vector3.forward;
            serializedGame.FindProperty("playerStartPosition").vector3Value = DemoSpawnPosition;
            serializedGame.FindProperty("playerStartRotation").quaternionValue = DemoSpawnRotation;
            serializedGame.ApplyModifiedPropertiesWithoutUndo();

            HideRaftForDemoStart(front, rear);
        }

        private static DemoPlayerController CreateDemoPlayer(Material playerMaterial, DemoGameController gameController, Transform cameraTarget)
        {
            GameObject playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            playerObject.name = "Demo Player";
            playerObject.transform.position = DemoSpawnPosition;
            playerObject.transform.rotation = DemoSpawnRotation;
            playerObject.transform.localScale = new Vector3(0.65f, 0.9f, 0.65f);
            playerObject.GetComponent<Renderer>().sharedMaterial = playerMaterial;
            playerObject.GetComponent<Renderer>().enabled = false;
            Object.DestroyImmediate(playerObject.GetComponent<Collider>());

            CharacterController characterController = playerObject.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.36f;
            characterController.center = new Vector3(0f, 0.9f, 0f);

            GameObject holdPoint = new GameObject("Carry Hold Point");
            holdPoint.transform.SetParent(playerObject.transform);
            holdPoint.transform.localPosition = new Vector3(0f, 1.15f, 0.85f);

            Camera oldCamera = Camera.main;
            if (oldCamera != null)
            {
                OrbitCameraController orbitCamera = oldCamera.GetComponent<OrbitCameraController>();
                if (orbitCamera != null)
                {
                    orbitCamera.enabled = false;
                }

                oldCamera.tag = "Untagged";
                oldCamera.enabled = false;
                AudioListener oldListener = oldCamera.GetComponent<AudioListener>();
                if (oldListener != null)
                {
                    oldListener.enabled = false;
                }
            }

            if (cameraTarget != null)
            {
                cameraTarget.gameObject.SetActive(false);
            }

            GameObject cameraObject = new GameObject("First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            cameraObject.transform.localRotation = Quaternion.identity;
            Camera mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.fieldOfView = 68f;
            mainCamera.nearClipPlane = 0.04f;
            cameraObject.AddComponent<AudioListener>();

            DemoPlayerController player = playerObject.AddComponent<DemoPlayerController>();
            SerializedObject serializedPlayer = new SerializedObject(player);
            serializedPlayer.FindProperty("viewCamera").objectReferenceValue = mainCamera;
            serializedPlayer.FindProperty("holdPoint").objectReferenceValue = holdPoint.transform;
            serializedPlayer.FindProperty("cameraLocalPosition").vector3Value = new Vector3(0f, 1.55f, 0f);
            serializedPlayer.FindProperty("showThirdPersonModel").boolValue = false;
            serializedPlayer.FindProperty("waterLevel").floatValue = 0.18f;
            serializedPlayer.FindProperty("waterSinkDepth").floatValue = -3.2f;
            serializedPlayer.FindProperty("gameController").objectReferenceValue = gameController;
            serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

            return player;
        }

        private static void CreatePlayableStartIsland(Material sandMaterial)
        {
            GameObject islandRoot = new GameObject("Playable Start Island");

            GameObject beach = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beach.name = "Large Playable Sand Island";
            beach.transform.SetParent(islandRoot.transform);
            beach.transform.position = new Vector3(0f, 0.2f, -50f);
            beach.transform.localScale = new Vector3(230f, 0.22f, 180f);
            beach.GetComponent<Renderer>().sharedMaterial = sandMaterial;
            CreateIslandSafetyCollider(islandRoot.transform);

            Material jungleMaterial = CreateMaterial("Start Island Jungle Material", new Color(0.12f, 0.38f, 0.18f));
            Material volcanicRockMaterial = CreateMaterial("Start Island Volcanic Rock Material", new Color(0.18f, 0.17f, 0.15f));
            Material craterMaterial = CreateMaterial("Start Island Dark Crater Material", new Color(0.05f, 0.045f, 0.04f));
            CreateVolcanoTerrain(islandRoot.transform, jungleMaterial, volcanicRockMaterial, craterMaterial);

            Material foamMaterial = CreateMaterial("Start Shore Wash Material", new Color(0.82f, 0.96f, 1f, 0.46f));
            for (int i = 0; i < 16; i++)
            {
                GameObject wash = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wash.name = $"Small Shore Wave Wash {i + 1}";
                wash.transform.SetParent(islandRoot.transform);
                wash.transform.position = new Vector3(-42f + i * 5.6f, 0.47f, 18f - Mathf.Sin(i * 1.7f) * 0.42f);
                wash.transform.localScale = new Vector3(4.2f, 0.012f, 0.18f + (i % 2) * 0.12f);
                wash.transform.localRotation = Quaternion.Euler(0f, Mathf.Sin(i) * 7f, 0f);
                wash.GetComponent<Renderer>().sharedMaterial = foamMaterial;
                Object.DestroyImmediate(wash.GetComponent<Collider>());
            }

            Material darkerSandMaterial = CreateMaterial("Start Beach Damp Sand Material", new Color(0.64f, 0.52f, 0.34f));
            for (int i = 0; i < 64; i++)
            {
                GameObject ripple = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ripple.name = "Sand Ripple";
                ripple.transform.SetParent(islandRoot.transform);
                float x = -70f + (i % 8) * 20f + Mathf.Sin(i * 2.1f) * 1.2f;
                float z = 4f - (i / 8) * 13f + Mathf.Cos(i * 1.3f) * 0.8f;
                ripple.transform.position = new Vector3(x, 0.47f, z);
                ripple.transform.localRotation = Quaternion.Euler(0f, -8f + Mathf.Sin(i) * 14f, 0f);
                ripple.transform.localScale = new Vector3(5.4f + (i % 3) * 1.2f, 0.01f, 0.05f);
                ripple.GetComponent<Renderer>().sharedMaterial = darkerSandMaterial;
                Object.DestroyImmediate(ripple.GetComponent<Collider>());
            }

            CreatePalmTree(islandRoot.transform, new Vector3(-38f, 0.58f, -30f), -18f, 6.2f);
            CreatePalmTree(islandRoot.transform, new Vector3(42f, 0.58f, -28f), 24f, 5.8f);
            CreatePalmTree(islandRoot.transform, new Vector3(-24f, 0.58f, -82f), 42f, 5.5f);
            CreatePalmTree(islandRoot.transform, new Vector3(28f, 0.58f, -78f), -36f, 5.2f);
            CreatePalmTree(islandRoot.transform, new Vector3(-62f, 0.55f, -10f), 64f, 4.7f);
            CreatePalmTree(islandRoot.transform, new Vector3(65f, 0.55f, -8f), -58f, 4.8f);
            CreateRock(islandRoot.transform, new Vector3(46f, 0.62f, 2f), new Vector3(4.8f, 1.8f, 3.6f));
            CreateRock(islandRoot.transform, new Vector3(-52f, 0.61f, -6f), new Vector3(4.2f, 1.5f, 3.3f));

            Material grassMaterial = CreateMaterial("Start Beach Grass Blade Material", new Color(0.08f, 0.42f, 0.16f));
            for (int i = 0; i < 120; i++)
            {
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float radius = 14f + (i % 23) * 3.1f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius * 1.15f - 2f, 0.53f, -45f + Mathf.Sin(angle) * radius * 0.82f);
                CreateGrassBlade(islandRoot.transform, position, 0.55f + (i % 4) * 0.1f, grassMaterial);
            }

            GameObject spawnGround = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spawnGround.name = "Reliable Player Spawn Beach Pad";
            spawnGround.transform.SetParent(islandRoot.transform);
            spawnGround.transform.position = new Vector3(82f, 0.8f, -58f);
            spawnGround.transform.localScale = new Vector3(62f, 0.24f, 42f);
            spawnGround.GetComponent<Renderer>().sharedMaterial = sandMaterial;
        }

        private static void CreateIslandSafetyCollider(Transform parent)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Playable Island Ground Collider";
            ground.transform.SetParent(parent);
            ground.transform.position = new Vector3(0f, 0.42f, -50f);
            ground.transform.localScale = new Vector3(210f, 0.22f, 165f);
            ground.GetComponent<Renderer>().enabled = false;
        }

        private static void CreateVolcanoTerrain(Transform parent, Material jungleMaterial, Material rockMaterial, Material craterMaterial)
        {
            Vector3 center = new Vector3(0f, 0.52f, -47f);
            CreateVolcanoMesh(parent, "Tall Jungle Volcano", center, 58f, 48f, 58f, 14, 128, jungleMaterial);
            CreateVolcanoMesh(parent, "Dark Volcanic Summit", center + new Vector3(0f, 45f, 0f), 18f, 14f, 14f, 6, 96, rockMaterial);

            GameObject crater = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            crater.name = "Volcano Crater";
            crater.transform.SetParent(parent);
            crater.transform.position = center + new Vector3(0f, 60.8f, 0f);
            crater.transform.localScale = new Vector3(8.5f, 0.1f, 6.8f);
            crater.GetComponent<Renderer>().sharedMaterial = craterMaterial;
            Object.DestroyImmediate(crater.GetComponent<Collider>());
        }

        private static void CreateVolcanoMesh(Transform parent, string name, Vector3 center, float radiusX, float radiusZ, float height, int rings, int segments, Material material)
        {
            GameObject volcano = new GameObject(name);
            volcano.transform.SetParent(parent);
            volcano.transform.position = center;

            Mesh mesh = new Mesh { name = $"{name} Mesh" };
            Vector3[] vertices = new Vector3[(rings + 1) * segments + 1];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[rings * segments * 6 + segments * 3];

            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float radiusScale = 1f - t * 0.86f;
                float y = Mathf.Pow(t, 1.8f) * height;
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    float uneven = 1f + Mathf.Sin(angle * 4.1f + ring * 0.7f) * 0.045f + Mathf.Cos(angle * 7.3f) * 0.025f;
                    int index = ring * segments + segment;
                    vertices[index] = new Vector3(Mathf.Cos(angle) * radiusX * radiusScale * uneven, y, Mathf.Sin(angle) * radiusZ * radiusScale * uneven);
                    uv[index] = new Vector2(segment / (float)segments, t);
                }
            }

            int topCenter = vertices.Length - 1;
            vertices[topCenter] = new Vector3(0f, height + 0.75f, 0f);
            uv[topCenter] = new Vector2(0.5f, 1f);

            int tri = 0;
            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    int current = ring * segments + segment;
                    int currentNext = ring * segments + next;
                    int upper = (ring + 1) * segments + segment;
                    int upperNext = (ring + 1) * segments + next;
                    triangles[tri++] = current;
                    triangles[tri++] = upper;
                    triangles[tri++] = currentNext;
                    triangles[tri++] = currentNext;
                    triangles[tri++] = upper;
                    triangles[tri++] = upperNext;
                }
            }

            int topRingStart = rings * segments;
            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles[tri++] = topRingStart + segment;
                triangles[tri++] = topCenter;
                triangles[tri++] = topRingStart + next;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            volcano.AddComponent<MeshFilter>().sharedMesh = mesh;
            volcano.AddComponent<MeshRenderer>().sharedMaterial = material;
            MeshCollider collider = volcano.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private static void CreateRock(Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Beach Rock";
            rock.transform.SetParent(parent);
            rock.transform.localPosition = localPosition;
            rock.transform.localRotation = Quaternion.Euler(0f, localPosition.x * 11f, localPosition.z * 7f);
            rock.transform.localScale = localScale;
            rock.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Start Island Rock Material", new Color(0.38f, 0.36f, 0.32f));
            Object.DestroyImmediate(rock.GetComponent<Collider>());
        }

        private static void CreateGrassBlade(Transform parent, Vector3 localPosition, float height, Material material)
        {
            GameObject grass = GameObject.CreatePrimitive(PrimitiveType.Cube);
            grass.name = "Beach Grass";
            grass.transform.SetParent(parent);
            grass.transform.localPosition = localPosition + Vector3.up * (height * 0.5f);
            grass.transform.localRotation = Quaternion.Euler(0f, localPosition.x * 37f + localPosition.z * 19f, 8f);
            grass.transform.localScale = new Vector3(0.035f, height, 0.035f);
            grass.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(grass.GetComponent<Collider>());
        }

        private static DemoLaunchButton CreateDemoButton(string name, Vector3 position, Material buttonMaterial, DemoGameController gameController, DemoButtonAction action)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;

            GameObject baseObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            baseObject.name = $"{name} Base";
            baseObject.transform.SetParent(root.transform);
            baseObject.transform.localPosition = Vector3.zero;
            baseObject.transform.localScale = new Vector3(0.5f, 0.16f, 0.5f);
            baseObject.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Launch Button Base Material", new Color(0.2f, 0.16f, 0.1f));

            GameObject button = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            button.name = name.Contains("Build") ? "Build Grid Button" : "Launch Button";
            button.transform.SetParent(root.transform);
            button.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            button.transform.localScale = new Vector3(0.34f, 0.1f, 0.34f);
            button.GetComponent<Renderer>().sharedMaterial = buttonMaterial;

            DemoLaunchButton launchButton = root.AddComponent<DemoLaunchButton>();
            SerializedObject serializedButton = new SerializedObject(launchButton);
            serializedButton.FindProperty("gameController").objectReferenceValue = gameController;
            serializedButton.FindProperty("action").enumValueIndex = (int)action;
            serializedButton.FindProperty("buttonRenderer").objectReferenceValue = button.GetComponent<Renderer>();
            serializedButton.ApplyModifiedPropertiesWithoutUndo();

            return launchButton;
        }

        private static DemoRaftSlot[] CreateDemoRaftSlots(RaftSection front, RaftSection rear, FailurePrototypeController physicsController, Material markerMaterial, Transform gridRoot)
        {
            List<DemoRaftSlot> slots = new List<DemoRaftSlot>();
            float[] xs = { -0.85f, 0f, 0.85f };
            CreateSlotRow(front, physicsController, markerMaterial, slots, xs, 0.82f, "Front", gridRoot, -4.55f);
            CreateSlotRow(front, physicsController, markerMaterial, slots, xs, -0.68f, "Center", gridRoot, -5.4f);
            CreateSlotRow(rear, physicsController, markerMaterial, slots, xs, -0.75f, "Back", gridRoot, -6.25f);
            return slots.ToArray();
        }

        private static void CreateSlotRow(RaftSection section, FailurePrototypeController physicsController, Material markerMaterial, List<DemoRaftSlot> slots, float[] xs, float localZ, string rowName, Transform gridRoot, float worldZ)
        {
            if (section == null)
            {
                return;
            }

            for (int i = 0; i < xs.Length; i++)
            {
                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Slot {rowName} {i + 1}";
                marker.transform.SetParent(gridRoot);
                marker.transform.position = new Vector3(82f + xs[i] * 1.25f, 1.08f, worldZ - 45.5f);
                marker.transform.localRotation = Quaternion.identity;
                marker.transform.localScale = new Vector3(0.62f, 0.025f, 0.62f);
                marker.GetComponent<Renderer>().sharedMaterial = markerMaterial;
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                BoxCollider trigger = marker.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(2.2f, 5f, 2.2f);

                DemoRaftSlot slot = marker.AddComponent<DemoRaftSlot>();
                slot.Initialize(section, physicsController, 0.2f, new Vector3(xs[i], 0.34f, localZ));
                slots.Add(slot);
            }
        }

        private static DemoCarryableItem[] CreateDemoMaterials()
        {
            List<DemoCarryableItem> items = new List<DemoCarryableItem>();
            Material plankMaterial = CreateMaterial("Demo Plank Material", new Color(0.48f, 0.28f, 0.13f));
            Material logMaterial = CreateMaterial("Demo Log Cargo Material", new Color(0.5f, 0.34f, 0.16f));
            Material plasticMaterial = CreateMaterial("Demo Plastic Float Material", new Color(0.08f, 0.34f, 0.95f));
            Material rockMaterial = CreateMaterial("Demo Rock Material", new Color(0.35f, 0.34f, 0.32f));
            Material ropeMaterial = CreateMaterial("Demo Rope Cargo Material", new Color(0.66f, 0.51f, 0.28f));

            items.Add(CreatePrimitiveItem("Plank", DemoMaterialType.Plank, PrimitiveType.Cube, new Vector3(72f, 1.08f, -63f), new Vector3(1.35f, 0.18f, 0.45f), plankMaterial, 3.5f, 0.08f, 0f, 0f, Quaternion.Euler(0f, -18f, 0f)));
            items.Add(CreatePrimitiveItem("Log", DemoMaterialType.Log, PrimitiveType.Cylinder, new Vector3(76f, 1.18f, -67f), new Vector3(0.32f, 0.9f, 0.32f), logMaterial, 5.5f, 0.18f, 0f, 0f, Quaternion.Euler(90f, 0f, 18f)));
            items.Add(CreatePrimitiveItem("Float Barrel", DemoMaterialType.FloatBarrel, PrimitiveType.Cylinder, new Vector3(82f, 1.22f, -69f), new Vector3(0.42f, 0.58f, 0.42f), plasticMaterial, 2.2f, 0.28f, 0f, 0f, Quaternion.Euler(0f, 12f, 0f)));
            items.Add(CreatePrefabOrCubeItem("Heavy Crate", DemoMaterialType.HeavyCrate, new Vector3(88f, 1.1f, -67f), new Vector3(0.58f, 0.58f, 0.58f), 13.5f, 0f, 0.2f, 0f));
            items.Add(CreatePrimitiveItem("Rock", DemoMaterialType.Rock, PrimitiveType.Sphere, new Vector3(94f, 1.1f, -62f), new Vector3(0.72f, 0.52f, 0.62f), rockMaterial, 20f, -0.05f, 0.35f, 0f));
            items.Add(CreatePrimitiveItem("Rope Bundle", DemoMaterialType.Rope, PrimitiveType.Cylinder, new Vector3(96f, 1.08f, -55f), new Vector3(0.38f, 0.22f, 0.38f), ropeMaterial, 1.5f, 0f, -0.2f, 18f, Quaternion.Euler(90f, 0f, 0f)));
            return items.ToArray();
        }

        private static DemoCarryableItem CreatePrefabOrCubeItem(string itemName, DemoMaterialType type, Vector3 position, Vector3 fallbackScale, float mass, float buoyancyBonus, float connectorStress, float connectorStrengthBonus)
        {
            GameObject[] prefabs = LoadCargoPrefabs();
            GameObject itemObject = prefabs.Length > 0 ? Object.Instantiate(prefabs[0]) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            itemObject.name = itemName;
            itemObject.transform.position = position;
            itemObject.transform.rotation = Quaternion.identity;
            itemObject.transform.localScale = prefabs.Length > 0 ? Vector3.one * 0.62f : fallbackScale;
            if (prefabs.Length == 0)
            {
                itemObject.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Demo Heavy Crate Fallback Material", new Color(0.35f, 0.2f, 0.1f));
            }

            return FinishDemoItem(itemObject, type, itemName, mass, buoyancyBonus, connectorStress, connectorStrengthBonus);
        }

        private static DemoCarryableItem CreatePrimitiveItem(string itemName, DemoMaterialType type, PrimitiveType primitiveType, Vector3 position, Vector3 scale, Material material, float mass, float buoyancyBonus, float connectorStress, float connectorStrengthBonus, Quaternion? rotation = null)
        {
            GameObject itemObject = GameObject.CreatePrimitive(primitiveType);
            itemObject.name = itemName;
            itemObject.transform.position = position;
            itemObject.transform.rotation = rotation ?? Quaternion.identity;
            itemObject.transform.localScale = scale;
            itemObject.GetComponent<Renderer>().sharedMaterial = material;
            return FinishDemoItem(itemObject, type, itemName, mass, buoyancyBonus, connectorStress, connectorStrengthBonus);
        }

        private static DemoCarryableItem FinishDemoItem(GameObject itemObject, DemoMaterialType type, string itemName, float mass, float buoyancyBonus, float connectorStress, float connectorStrengthBonus)
        {
            EnsureDemoItemCollider(itemObject);
            Rigidbody body = itemObject.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = itemObject.AddComponent<Rigidbody>();
            }

            body.mass = Mathf.Max(0.1f, mass);
            body.linearDamping = 0.6f;
            body.angularDamping = 0.8f;

            DemoCarryableItem item = itemObject.AddComponent<DemoCarryableItem>();
            item.Configure(type, itemName, mass, buoyancyBonus, connectorStress, connectorStrengthBonus);
            return item;
        }

        private static void EnsureDemoItemCollider(GameObject itemObject)
        {
            if (itemObject.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            BoxCollider collider = itemObject.AddComponent<BoxCollider>();
            Renderer[] renderers = itemObject.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            collider.center = itemObject.transform.InverseTransformPoint(bounds.center);
            Vector3 localSize = itemObject.transform.InverseTransformVector(bounds.size);
            collider.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
        }

        private static void HideRaftForDemoStart(RaftSection front, RaftSection rear)
        {
            SetSectionEditorVisibleAndPhysical(front, false);
            SetSectionEditorVisibleAndPhysical(rear, false);
        }

        private static void SetSectionEditorVisibleAndPhysical(RaftSection section, bool value)
        {
            if (section == null)
            {
                return;
            }

            foreach (Renderer renderer in section.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject != section.gameObject)
                {
                    renderer.enabled = value;
                }
            }

            foreach (Collider collider in section.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = value;
            }

            section.Rigidbody.isKinematic = !value;
            section.Rigidbody.useGravity = value;
        }

        private static WaveSurface CreateWater()
        {
            GameObject water = new GameObject("Prototype Water");
            water.name = "Prototype Water";
            water.transform.position = new Vector3(0f, 0.18f, 0f);

            water.AddComponent<MeshFilter>();
            MeshRenderer renderer = water.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateMaterial("Prototype Water Material", new Color(0.05f, 0.43f, 0.78f, 0.88f));

            WaveSurface waveSurface = water.AddComponent<WaveSurface>();
            WaterMeshVisual waterMeshVisual = water.AddComponent<WaterMeshVisual>();
            CreateOceanDepthVolume();

            SerializedObject serializedWater = new SerializedObject(waterMeshVisual);
            serializedWater.FindProperty("waveSurface").objectReferenceValue = waveSurface;
            serializedWater.FindProperty("size").floatValue = 520f;
            serializedWater.FindProperty("resolution").intValue = 220;
            serializedWater.ApplyModifiedPropertiesWithoutUndo();

            return waveSurface;
        }

        private static void CreateOceanDepthVolume()
        {
            Material underwaterMaterial = CreateMaterial("Underwater Volume Material", new Color(0.04f, 0.28f, 0.58f, 0.34f));
            if (underwaterMaterial.HasProperty("_Cull"))
            {
                underwaterMaterial.SetFloat("_Cull", 0f);
            }

            GameObject volume = GameObject.CreatePrimitive(PrimitiveType.Cube);
            volume.name = "Ocean Water Depth Volume";
            volume.transform.position = new Vector3(0f, -10f, 0f);
            volume.transform.localScale = new Vector3(520f, 20f, 520f);
            volume.GetComponent<Renderer>().sharedMaterial = underwaterMaterial;
            Object.DestroyImmediate(volume.GetComponent<Collider>());

            GameObject oceanFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            oceanFloor.name = "Ocean Catch Floor";
            oceanFloor.transform.position = new Vector3(0f, -8.5f, 0f);
            oceanFloor.transform.localScale = new Vector3(520f, 0.35f, 520f);
            oceanFloor.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Deep Ocean Floor Material", new Color(0.06f, 0.13f, 0.18f));
        }

        private static void SetSceneViewForDemoPreview()
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
            {
                return;
            }

            sceneView.in2DMode = false;
            sceneView.orthographic = false;
            sceneView.LookAt(new Vector3(0f, 22f, -58f), Quaternion.Euler(28f, -28f, 0f), 165f);
            sceneView.Repaint();
        }

        private static RaftSection CreateSection(string name, Vector3 position, Vector3 scale, float mass, WaveSurface waveSurface, Color color, PhysicsMaterial frictionMaterial)
        {
            GameObject sectionObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sectionObject.name = name;
            sectionObject.transform.position = position;
            sectionObject.transform.localScale = Vector3.one;
            sectionObject.GetComponent<Renderer>().enabled = false;

            BoxCollider rootCollider = sectionObject.GetComponent<BoxCollider>();
            rootCollider.size = scale;
            rootCollider.material = frictionMaterial;

            Rigidbody body = sectionObject.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.8f;
            body.angularDamping = 1.2f;
            body.centerOfMass = new Vector3(0f, -0.35f, 0f);
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BuoyancyBody buoyancy = sectionObject.AddComponent<BuoyancyBody>();
            RaftSection section = sectionObject.AddComponent<RaftSection>();

            List<Transform> points = new List<Transform>();
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    GameObject point = new GameObject($"Buoyancy Point {x},{z}");
                    point.transform.SetParent(sectionObject.transform);
                    point.transform.localPosition = new Vector3(x * 1.15f, -0.42f, z * 1.12f);
                    points.Add(point.transform);
                }
            }

            buoyancy.SetWaveSurface(waveSurface);
            buoyancy.SetBuoyancyPoints(points.ToArray());
            buoyancy.SetSplashPoints(CreateSplashPoints(sectionObject.transform));
            buoyancy.SetSplashParticles(CreateSplashParticles(sectionObject.transform));

            CreateLogVisuals(sectionObject.transform, color);
            CreateCargoCatchCollider(sectionObject.transform, frictionMaterial);

            SerializedObject serializedSection = new SerializedObject(section);
            serializedSection.FindProperty("sectionName").stringValue = name;
            serializedSection.FindProperty("structuralMass").floatValue = mass;
            serializedSection.ApplyModifiedPropertiesWithoutUndo();

            return section;
        }

        private static SectionConnector CreateConnector(string name, RaftSection sectionA, RaftSection sectionB, Vector3 position, ConnectorType type, bool breakable = false)
        {
            GameObject connectorObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            connectorObject.name = name;
            connectorObject.transform.position = position;
            connectorObject.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            connectorObject.transform.localScale = new Vector3(0.12f, 0.75f, 0.12f);
            connectorObject.GetComponent<Collider>().enabled = false;

            Renderer renderer = connectorObject.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateMaterial($"{name} Material", Color.green);
            renderer.enabled = false;

            ConfigurableJoint joint = sectionA.gameObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = sectionB.Rigidbody;
            joint.enableCollision = false;

            SectionConnector connector = connectorObject.AddComponent<SectionConnector>();
            connector.Initialize(sectionA, sectionB, type);

            SerializedObject serializedConnector = new SerializedObject(connector);
            serializedConnector.FindProperty("joint").objectReferenceValue = joint;
            serializedConnector.FindProperty("canBreak").boolValue = breakable;
            serializedConnector.FindProperty("hitPoints").floatValue = breakable ? 6f : 160f;
            serializedConnector.FindProperty("stressMultiplier").floatValue = breakable ? 0.58f : 0.35f;
            serializedConnector.FindProperty("warningThreshold").floatValue = breakable ? 0.48f : 0.78f;
            serializedConnector.FindProperty("breakThreshold").floatValue = breakable ? 1.05f : 1.8f;
            serializedConnector.FindProperty("damagePerSecond").floatValue = breakable ? 2f : 1.5f;
            serializedConnector.FindProperty("warningSecondsBeforeBreak").floatValue = breakable ? 5.5f : 5f;
            serializedConnector.FindProperty("crackMotionForce").floatValue = breakable ? 2.2f : 0f;
            serializedConnector.FindProperty("separationImpulse").floatValue = breakable ? 0.08f : 0f;
            serializedConnector.FindProperty("scriptedMaxSlack").floatValue = breakable ? 1.15f : 0.03f;
            serializedConnector.FindProperty("scriptedMaxAngularSlack").floatValue = breakable ? 32f : 4f;
            serializedConnector.FindProperty("scriptedSinkDepth").floatValue = breakable ? 0.6f : 0f;
            serializedConnector.FindProperty("warningRenderer").objectReferenceValue = renderer;
            serializedConnector.ApplyModifiedPropertiesWithoutUndo();

            connectorObject.transform.SetParent(sectionA.transform);
            return connector;
        }

        private static void CreateSail(RaftSection parent)
        {
            GameObject mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mast.name = "Mast";
            mast.transform.SetParent(parent.transform);
            mast.transform.localPosition = new Vector3(0f, 2.35f, -0.2f);
            mast.transform.localScale = new Vector3(0.1f, 2.85f, 0.1f);
            mast.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Mast Material", new Color(0.38f, 0.22f, 0.11f));
            Object.DestroyImmediate(mast.GetComponent<Collider>());

            CreateSailYard(parent.transform, "Top Sail Yard", new Vector3(0f, 4.95f, -0.15f));
            CreateSailYard(parent.transform, "Bottom Sail Yard", new Vector3(0f, 1.65f, -0.05f));
            CreateCurvedSail(parent.transform);
            CreateSailRope(parent.transform, new Vector3(-1.45f, 1.65f, -0.05f), new Vector3(-1.35f, 0.32f, 1.35f));
            CreateSailRope(parent.transform, new Vector3(1.45f, 1.65f, -0.05f), new Vector3(1.35f, 0.32f, 1.35f));
        }

        private static void CreateSailYard(Transform parent, string name, Vector3 localPosition)
        {
            GameObject yard = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            yard.name = name;
            yard.transform.SetParent(parent);
            yard.transform.localPosition = localPosition;
            yard.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            yard.transform.localScale = new Vector3(0.06f, 1.42f, 0.06f);
            yard.GetComponent<Renderer>().sharedMaterial = CreateMaterial($"{name} Material", new Color(0.42f, 0.25f, 0.12f));
            Object.DestroyImmediate(yard.GetComponent<Collider>());
        }

        private static void CreateCurvedSail(Transform parent)
        {
            GameObject sail = new GameObject("Blue Curved Sail");
            sail.transform.SetParent(parent);
            sail.transform.localPosition = Vector3.zero;

            Mesh mesh = new Mesh { name = "Prototype Curved Sail Mesh" };
            int columns = 8;
            int rows = 8;
            Vector3[] vertices = new Vector3[(columns + 1) * (rows + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[columns * rows * 6];

            for (int y = 0, index = 0; y <= rows; y++)
            {
                float v = y / (float)rows;
                for (int x = 0; x <= columns; x++, index++)
                {
                    float u = x / (float)columns;
                    float localX = Mathf.Lerp(-1.45f, 1.45f, u);
                    float localY = Mathf.Lerp(1.68f, 4.9f, v);
                    float curve = Mathf.Sin(u * Mathf.PI) * 0.58f;
                    vertices[index] = new Vector3(localX, localY, -0.05f + curve);
                    uv[index] = new Vector2(u, v);
                }
            }

            for (int y = 0, triangle = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int root = y * (columns + 1) + x;
                    triangles[triangle++] = root;
                    triangles[triangle++] = root + columns + 1;
                    triangles[triangle++] = root + 1;
                    triangles[triangle++] = root + 1;
                    triangles[triangle++] = root + columns + 1;
                    triangles[triangle++] = root + columns + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = sail.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = sail.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateSailMaterial();
        }

        private static void CreateSailRope(Transform parent, Vector3 from, Vector3 to)
        {
            Vector3 midpoint = (from + to) * 0.5f;
            Vector3 direction = to - from;
            GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rope.name = "Sail Support Rope";
            rope.transform.SetParent(parent);
            rope.transform.localPosition = midpoint;
            rope.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            rope.transform.localScale = new Vector3(0.025f, direction.magnitude * 0.5f, 0.025f);
            rope.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Sail Rope Material", new Color(0.67f, 0.54f, 0.34f));
            Object.DestroyImmediate(rope.GetComponent<Collider>());
        }

        private static void CreateCargoCatchCollider(Transform parent, PhysicsMaterial frictionMaterial)
        {
            GameObject catcher = new GameObject("Cargo Catch Collider");
            catcher.transform.SetParent(parent);
            catcher.transform.localPosition = new Vector3(0f, 0.34f, 0f);
            BoxCollider collider = catcher.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.85f, 0.08f, 2.75f);
            collider.material = frictionMaterial;
        }

        private static void CreateLogVisuals(Transform parent, Color baseColor)
        {
            Material logMaterial = CreateMaterial($"{parent.name} Log Material", baseColor);
            Material ropeMaterial = CreateMaterial($"{parent.name} Rope Material", new Color(0.67f, 0.54f, 0.34f));

            int logCount = 8;
            float spacing = 0.31f;
            float startX = -(logCount - 1) * spacing * 0.5f;
            for (int i = 0; i < logCount; i++)
            {
                float x = startX + i * spacing;
                GameObject log = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                log.name = $"Log {i + 1}";
                log.transform.SetParent(parent);
                log.transform.localPosition = new Vector3(x, 0.02f, 0f);
                log.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                log.transform.localScale = new Vector3(0.2f, 1.46f, 0.2f);
                log.GetComponent<Renderer>().sharedMaterial = logMaterial;
            }

            for (int i = 0; i < 3; i++)
            {
                float z = -1.05f + i * 1.05f;
                GameObject rope = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rope.name = $"Rope Lash {i + 1}";
                rope.transform.SetParent(parent);
                rope.transform.localPosition = new Vector3(0f, 0.27f, z);
                rope.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                rope.transform.localScale = new Vector3(0.05f, 1.46f, 0.05f);
                rope.GetComponent<Renderer>().sharedMaterial = ropeMaterial;
                Object.DestroyImmediate(rope.GetComponent<Collider>());
            }
        }

        private static ParticleSystem CreateSplashParticles(Transform parent)
        {
            GameObject splash = new GameObject("Splash Particles");
            splash.transform.SetParent(parent);
            splash.transform.localPosition = Vector3.zero;
            ParticleSystem particles = splash.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.startLifetime = 0.34f;
            main.startSpeed = 1.25f;
            main.startSize = 0.06f;
            main.maxParticles = 360;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            main.startColor = new Color(0.82f, 0.94f, 1f, 0.66f);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.06f;

            Renderer renderer = particles.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateParticleMaterial("Splash Particle Material", new Color(0.82f, 0.94f, 1f, 0.66f));

            return particles;
        }

        private static Transform[] CreateSplashPoints(Transform parent)
        {
            List<Transform> points = new List<Transform>();
            float[] xs = { -1.35f, 0f, 1.35f };
            float[] zs = { -1.35f, 0f, 1.35f };
            float[] ys = { -0.58f, -0.2f, 0.18f };

            foreach (float y in ys)
            {
                foreach (float x in xs)
                {
                    CreateSplashPoint(parent, points, new Vector3(x, y, -1.35f));
                    CreateSplashPoint(parent, points, new Vector3(x, y, 1.35f));
                }

                foreach (float z in zs)
                {
                    CreateSplashPoint(parent, points, new Vector3(-1.35f, y, z));
                    CreateSplashPoint(parent, points, new Vector3(1.35f, y, z));
                }
            }

            return points.ToArray();
        }

        private static void CreateSplashPoint(Transform parent, List<Transform> points, Vector3 localPosition)
        {
            GameObject point = new GameObject("Splash Contact Point");
            point.transform.SetParent(parent);
            point.transform.localPosition = localPosition;
            points.Add(point.transform);
        }

        private static Transform CreateCargoPoint(string name, Transform parent, Vector3 localPosition)
        {
            GameObject marker = new GameObject(name);
            marker.name = name;
            marker.transform.SetParent(parent);
            marker.transform.localPosition = localPosition;
            return marker.transform;
        }

        private static void CreateCargo(RaftSection parent, Vector3 worldPosition, float mass)
        {
            GameObject cargo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cargo.name = "Loose Cargo";
            cargo.transform.position = worldPosition;
            cargo.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);
            cargo.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Cargo Material", new Color(0.2f, 0.52f, 0.32f));

            Rigidbody body = cargo.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.05f;
            body.angularDamping = 0.05f;
        }

        private static void CreateIsland()
        {
            GameObject islandRoot = new GameObject("Beach Test Island");
            islandRoot.transform.position = new Vector3(5f, 0.08f, 72f);

            Material sandMaterial = CreateSandMaterial();
            Material grassMaterial = CreateMaterial("Island Grass Material", new Color(0.18f, 0.48f, 0.22f));
            Material rockMaterial = CreateMaterial("Island Rock Material", new Color(0.42f, 0.39f, 0.34f));
            Material foamMaterial = CreateMaterial("Shore Foam Material", new Color(0.86f, 0.96f, 1f, 0.62f));

            CreateIslandDisc(islandRoot.transform, "Soft Sand Beach", 36f, 23f, 0.03f, sandMaterial);
            CreateIslandTerrain(islandRoot.transform, grassMaterial, rockMaterial);
            CreateShoreFoam(islandRoot.transform, foamMaterial);
            CreatePalmTree(islandRoot.transform, new Vector3(-8.5f, 0.35f, 1.8f), 16f, 2.6f);
            CreatePalmTree(islandRoot.transform, new Vector3(6.8f, 0.34f, -4.2f), -22f, 2.35f);
            CreatePalmTree(islandRoot.transform, new Vector3(1.8f, 0.34f, 7.4f), 38f, 2.25f);
            CreatePalmTree(islandRoot.transform, new Vector3(-15.5f, 0.28f, -7.5f), -42f, 2.05f);
            CreatePalmTree(islandRoot.transform, new Vector3(15.3f, 0.28f, 5.6f), 54f, 2.1f);
            CreatePalmTree(islandRoot.transform, new Vector3(-3.5f, 0.32f, -9.2f), 78f, 1.9f);
            CreateBeachRock(islandRoot.transform, new Vector3(-28f, 0.18f, -9.5f), new Vector3(2.2f, 0.72f, 1.45f), rockMaterial);
            CreateBeachRock(islandRoot.transform, new Vector3(25f, 0.17f, 7.8f), new Vector3(1.9f, 0.62f, 1.4f), rockMaterial);
            CreateBeachRock(islandRoot.transform, new Vector3(34f, 0.15f, -3.4f), new Vector3(1.35f, 0.46f, 1.05f), rockMaterial);
            CreateBeachGrassTufts(islandRoot.transform, grassMaterial);
        }

        private static void CreateIslandDisc(Transform parent, string name, float radiusX, float radiusZ, float height, Material material)
        {
            GameObject disc = new GameObject(name);
            disc.transform.SetParent(parent);
            disc.transform.localPosition = new Vector3(0f, height, 0f);

            Mesh mesh = new Mesh { name = $"{name} Mesh" };
            int segments = 64;
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float uneven = 1f + Mathf.Sin(angle * 3.1f) * 0.045f + Mathf.Cos(angle * 5.4f) * 0.035f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radiusX * uneven, 0f, Mathf.Sin(angle) * radiusZ * uneven);
                uv[i + 1] = new Vector2(Mathf.Cos(angle) * 0.5f + 0.5f, Mathf.Sin(angle) * 0.5f + 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                int root = i * 3;
                triangles[root] = 0;
                triangles[root + 1] = i + 1;
                triangles[root + 2] = i == segments - 1 ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            disc.AddComponent<MeshFilter>().sharedMesh = mesh;
            disc.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreateIslandTerrain(Transform parent, Material grassMaterial, Material rockMaterial)
        {
            GameObject terrain = new GameObject("Raised Island Terrain");
            terrain.transform.SetParent(parent);
            terrain.transform.localPosition = Vector3.zero;

            Mesh mesh = new Mesh { name = "Raised Island Terrain Mesh" };
            int grid = 38;
            float radiusX = 23f;
            float radiusZ = 14.5f;
            Vector3[] vertices = new Vector3[(grid + 1) * (grid + 1)];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[grid * grid * 6];

            for (int z = 0, index = 0; z <= grid; z++)
            {
                float vz = z / (float)grid;
                for (int x = 0; x <= grid; x++, index++)
                {
                    float vx = x / (float)grid;
                    float localX = Mathf.Lerp(-radiusX, radiusX, vx);
                    float localZ = Mathf.Lerp(-radiusZ, radiusZ, vz);
                    float nx = localX / radiusX;
                    float nz = localZ / radiusZ;
                    float radial = Mathf.Clamp01(Mathf.Sqrt(nx * nx + nz * nz));
                    float mountain = Mathf.Pow(1f - radial, 2.2f) * 7.8f;
                    float ridge = Mathf.PerlinNoise(vx * 4.2f + 1.7f, vz * 4.2f + 5.3f) * 1.4f * (1f - radial);
                    float height = radial > 1f ? -0.12f : 0.22f + mountain + ridge;
                    vertices[index] = new Vector3(localX, height, localZ);
                    uv[index] = new Vector2(vx, vz);
                }
            }

            for (int z = 0, tri = 0; z < grid; z++)
            {
                for (int x = 0; x < grid; x++)
                {
                    int root = z * (grid + 1) + x;
                    triangles[tri++] = root;
                    triangles[tri++] = root + grid + 1;
                    triangles[tri++] = root + 1;
                    triangles[tri++] = root + 1;
                    triangles[tri++] = root + grid + 1;
                    triangles[tri++] = root + grid + 2;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial = grassMaterial;

            CreateMountainCap(parent, rockMaterial);
        }

        private static void CreateMountainCap(Transform parent, Material rockMaterial)
        {
            GameObject cap = new GameObject("Distant Rocky Peak");
            cap.name = "Distant Rocky Peak";
            cap.transform.SetParent(parent);
            cap.transform.localPosition = new Vector3(0.5f, 7.2f, 0.2f);
            cap.transform.localScale = new Vector3(3.4f, 1.4f, 3.4f);

            Mesh mesh = new Mesh { name = "Distant Rocky Peak Mesh" };
            int segments = 18;
            Vector3[] vertices = new Vector3[segments + 2];
            int[] triangles = new int[segments * 6];

            vertices[0] = new Vector3(0f, 1.2f, 0f);
            vertices[1] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                vertices[i + 2] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }

            for (int i = 0, tri = 0; i < segments; i++)
            {
                int next = i == segments - 1 ? 2 : i + 3;
                int current = i + 2;
                triangles[tri++] = 0;
                triangles[tri++] = current;
                triangles[tri++] = next;
                triangles[tri++] = 1;
                triangles[tri++] = next;
                triangles[tri++] = current;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            cap.AddComponent<MeshFilter>().sharedMesh = mesh;
            cap.AddComponent<MeshRenderer>().sharedMaterial = rockMaterial;
        }

        private static void CreateShoreFoam(Transform parent, Material material)
        {
            CreateFoamRing(parent, "Inner Shore Wash", 36.8f, 23.6f, 39.8f, 26.2f, 0.015f, material);
            CreateFoamRing(parent, "Outer Shore Break", 39.6f, 26f, 42.2f, 28.4f, 0.005f, material);
        }

        private static void CreateFoamRing(Transform parent, string name, float innerX, float innerZ, float outerX, float outerZ, float height, Material material)
        {
            GameObject ring = new GameObject(name);
            ring.transform.SetParent(parent);
            ring.transform.localPosition = new Vector3(0f, height, 0f);

            Mesh mesh = new Mesh { name = $"{name} Mesh" };
            int segments = 96;
            Vector3[] vertices = new Vector3[segments * 2];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float uneven = 1f + Mathf.Sin(angle * 3.1f) * 0.045f + Mathf.Cos(angle * 5.4f) * 0.035f;
                vertices[i * 2] = new Vector3(Mathf.Cos(angle) * innerX * uneven, 0f, Mathf.Sin(angle) * innerZ * uneven);
                vertices[i * 2 + 1] = new Vector3(Mathf.Cos(angle) * outerX * uneven, 0f, Mathf.Sin(angle) * outerZ * uneven);
                uv[i * 2] = new Vector2(0f, i / (float)segments);
                uv[i * 2 + 1] = new Vector2(1f, i / (float)segments);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int tri = i * 6;
                int inner = i * 2;
                int outer = inner + 1;
                int nextInner = next * 2;
                int nextOuter = nextInner + 1;
                triangles[tri] = inner;
                triangles[tri + 1] = nextInner;
                triangles[tri + 2] = outer;
                triangles[tri + 3] = outer;
                triangles[tri + 4] = nextInner;
                triangles[tri + 5] = nextOuter;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            ring.AddComponent<MeshFilter>().sharedMesh = mesh;
            ring.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void CreatePalmTree(Transform parent, Vector3 localPosition, float yaw, float scale)
        {
            Material barkMaterial = CreateMaterial("Palm Bark Material", new Color(0.43f, 0.28f, 0.13f));
            Material leafMaterial = CreateMaterial("Palm Leaf Material", new Color(0.07f, 0.42f, 0.19f));

            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Palm Trunk";
            trunk.transform.SetParent(parent);
            trunk.transform.localPosition = localPosition + new Vector3(0f, 0.7f * scale, 0f);
            trunk.transform.localRotation = Quaternion.Euler(0f, yaw, -7f);
            trunk.transform.localScale = new Vector3(0.14f * scale, 0.72f * scale, 0.14f * scale);
            trunk.GetComponent<Renderer>().sharedMaterial = barkMaterial;
            Object.DestroyImmediate(trunk.GetComponent<Collider>());

            Vector3 crown = localPosition + new Vector3(0f, 1.48f * scale, 0f);
            for (int i = 0; i < 7; i++)
            {
                GameObject leaf = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaf.name = "Palm Frond";
                leaf.transform.SetParent(parent);
                float angle = yaw + i * (360f / 7f);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                leaf.transform.localPosition = crown + direction * (0.38f * scale) + Vector3.down * (0.05f * scale);
                leaf.transform.localRotation = Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(18f, 0f, 0f);
                leaf.transform.localScale = new Vector3(0.12f * scale, 0.035f * scale, 0.92f * scale);
                leaf.GetComponent<Renderer>().sharedMaterial = leafMaterial;
                Object.DestroyImmediate(leaf.GetComponent<Collider>());
            }

            GameObject coconuts = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coconuts.name = "Palm Coconuts";
            coconuts.transform.SetParent(parent);
            coconuts.transform.localPosition = crown + new Vector3(0.04f, -0.08f, 0.02f) * scale;
            coconuts.transform.localScale = Vector3.one * (0.16f * scale);
            coconuts.GetComponent<Renderer>().sharedMaterial = CreateMaterial("Coconut Material", new Color(0.24f, 0.15f, 0.08f));
            Object.DestroyImmediate(coconuts.GetComponent<Collider>());
        }

        private static void CreateBeachRock(Transform parent, Vector3 localPosition, Vector3 scale, Material material)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Beach Rock";
            rock.transform.SetParent(parent);
            rock.transform.localPosition = localPosition;
            rock.transform.localScale = scale;
            rock.GetComponent<Renderer>().sharedMaterial = material;
            Object.DestroyImmediate(rock.GetComponent<Collider>());
        }

        private static void CreateBeachGrassTufts(Transform parent, Material material)
        {
            for (int i = 0; i < 80; i++)
            {
                float angle = i * 137.5f * Mathf.Deg2Rad;
                float radius = 5.5f + (i % 13) * 1.15f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.2f, Mathf.Sin(angle) * radius * 0.68f);
                GameObject tuft = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tuft.name = "Beach Grass Tuft";
                tuft.transform.SetParent(parent);
                tuft.transform.localPosition = position;
                tuft.transform.localRotation = Quaternion.Euler(0f, i * 33f, 0f);
                tuft.transform.localScale = new Vector3(0.11f, 0.46f + (i % 3) * 0.1f, 0.11f);
                tuft.GetComponent<Renderer>().sharedMaterial = material;
                Object.DestroyImmediate(tuft.GetComponent<Collider>());
            }
        }

        private static Transform CreateCameraAndLight()
        {
            GameObject lightObject = new GameObject("Sun");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(5.5f, 5.2f, -8.5f);
            camera.transform.rotation = Quaternion.Euler(32f, -35f, 0f);
            camera.fieldOfView = 55f;
            cameraObject.AddComponent<AudioListener>();

            GameObject target = new GameObject("Camera Orbit Target");
            OrbitCameraController orbitCamera = cameraObject.AddComponent<OrbitCameraController>();
            SerializedObject serializedCamera = new SerializedObject(orbitCamera);
            serializedCamera.FindProperty("target").objectReferenceValue = target.transform;
            serializedCamera.FindProperty("distance").floatValue = 8.5f;
            serializedCamera.FindProperty("yaw").floatValue = -35f;
            serializedCamera.FindProperty("pitch").floatValue = 28f;
            serializedCamera.ApplyModifiedPropertiesWithoutUndo();

            return target.transform;
        }

        private static PhysicsMaterial CreatePhysicsMaterial(string name, float staticFriction, float dynamicFriction)
        {
            PhysicsMaterial material = new PhysicsMaterial(name)
            {
                staticFriction = staticFriction,
                dynamicFriction = dynamicFriction,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Maximum,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };

            return material;
        }

        private static Material CreateSailMaterial()
        {
            Material material = CreateMaterial("Blue Cloth Sail Material", new Color(0.04f, 0.18f, 0.48f));
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, true)
            {
                name = "Prototype Blue Cloth Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float verticalThread = Mathf.Abs(Mathf.Sin(x * 0.42f)) * 0.16f;
                    float horizontalThread = Mathf.Abs(Mathf.Sin(y * 0.31f)) * 0.1f;
                    float fadedCenter = Mathf.Sin((x / 127f) * Mathf.PI) * 0.08f;
                    float weave = verticalThread + horizontalThread + fadedCenter;
                    Color color = new Color(0.035f + weave, 0.12f + weave, 0.34f + weave * 0.75f, 1f);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", 0f);
            }

            return material;
        }

        private static Material CreateSandMaterial()
        {
            Material material = CreateMaterial("Warm Sand Material", new Color(0.86f, 0.74f, 0.48f));
            Texture2D texture = new Texture2D(128, 128, TextureFormat.RGBA32, true)
            {
                name = "Prototype Sand Texture",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    float grain = Mathf.PerlinNoise(x * 0.19f, y * 0.19f) * 0.12f;
                    float shell = Mathf.PerlinNoise(x * 0.045f + 7.1f, y * 0.045f + 3.4f) * 0.08f;
                    Color color = new Color(0.72f + grain + shell, 0.62f + grain * 0.65f, 0.38f + grain * 0.38f, 1f);
                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();

            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.18f);
            }

            return material;
        }

        private static Material CreateParticleMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Particles/Standard Unlit");
            }

            if (shader == null)
            {
                return CreateMaterial(name, color);
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

            return material;
        }

        private static Material CreateMaterial(string name, Color color)
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

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.82f);
            }

            if (color.a < 0.99f)
            {
                if (material.HasProperty("_Surface"))
                {
                    material.SetFloat("_Surface", 1f);
                }

                if (material.HasProperty("_Blend"))
                {
                    material.SetFloat("_Blend", 0f);
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

                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            return material;
        }
    }
}
