using System;
using System.Collections.Generic;
using System.IO;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class BrightLargeSurvivalIslandTools
    {
        private const string ScenePath = "Assets/_Project/Scenes/Bright_Large_Survival_Island_Volcano_HQ.unity";
        private const string CurrentApprovedScenePath =
            "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string MaterialFolder = "Assets/_Project/GeneratedMaterials";
        private const string AutoOpenSessionKey = "ThisWillFloat.OpenCurrentApprovedEnvironment.v2";
        private static readonly Vector2 VolcanoCenter = new Vector2(35f, 75f);
        private const float VolcanoRadius = 345f;

        private static readonly string[] GrayCliffs =
        {
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_1_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_2_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_3_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_4_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_5_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_6_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_7_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_8_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_9_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_10_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Gray Rock/Cliff_11_LODs.prefab"
        };

        private static readonly string[] DarkCliffs =
        {
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Dark Rock/Cliff_1_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Dark Rock/Cliff_3_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Dark Rock/Cliff_5_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Dark Rock/Cliff_7_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Dark Rock/Cliff_9_LODs.prefab",
            "Assets/Blocky Cliff Formations/Universal Render Pipeline (URP)/Prefabs/Dark Rock/Cliff_11_LODs.prefab"
        };

        private static readonly string[] Palms =
        {
            "Assets/PalmTreePack/Prefabs/1/Palm_1_1.prefab",
            "Assets/PalmTreePack/Prefabs/2/Palm_2_1.prefab",
            "Assets/PalmTreePack/Prefabs/Old/Palm_1_2.prefab",
            "Assets/PalmTreePack/Prefabs/Old/Palm_1_3.prefab",
            "Assets/PalmTreePack/Prefabs/Old/Palm_1_4.prefab",
            "Assets/PalmTreePack/Prefabs/Old/Palm_2_2.prefab",
            "Assets/PalmTreePack/Prefabs/Old/Palm_2_3.prefab"
        };

        private static readonly string[] BroadleafTrees =
        {
            "Assets/Island/Prefabs/Tree_a.prefab",
            "Assets/Island/Prefabs/Tree_b.prefab"
        };

        private static readonly string[] Rocks =
        {
            "Assets/RockFREE/LODGroups/rock1_LOD0.prefab",
            "Assets/RockFREE/LODGroups/rock2_LOD0.prefab",
            "Assets/RockFREE/LODGroups/rock3_LOD0.prefab",
            "Assets/RockFREE/LODGroups/rock4_LOD0.prefab",
            "Assets/RockFREE/LODGroups/rock5_LOD0.prefab",
            "Assets/RockFREE/LODGroups/rock6_LOD0.prefab",
            "Assets/A_piece_of_nature/Prefabs/Rock_3.prefab",
            "Assets/A_piece_of_nature/Prefabs/Rock_4.prefab"
        };

        [MenuItem("This Will Float DEMO/1. Build HQ Survival Island With Ultimate Stamp Volcano")]
        public static void BuildFromMenu()
        {
            BuildIsland(true);
        }

        public static void BuildIslandBatch()
        {
            BuildIsland(false);
        }

        [InitializeOnLoadMethod]
        private static void OpenCompletedSceneOnce()
        {
            if (Application.isBatchMode
                || SessionState.GetBool(AutoOpenSessionKey, false)
                || AssetDatabase.LoadAssetAtPath<SceneAsset>(CurrentApprovedScenePath) == null)
            {
                return;
            }

            SessionState.SetBool(AutoOpenSessionKey, true);
            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    return;
                }

                EditorSceneManager.OpenScene(CurrentApprovedScenePath, OpenSceneMode.Single);
                EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.GetCanvas();
                if (canvas != null)
                {
                    Selection.activeGameObject = canvas.gameObject;
                    FrameIsland(canvas);
                }
            };
        }

        public static void CaptureIslandBatch()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.GetCanvas();
            if (canvas == null)
            {
                throw new MissingReferenceException("High-quality island terrain was not found.");
            }

            GameObject cameraObject = new GameObject("Temporary HQ Island Capture Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 4500f;
            camera.aspect = 16f / 9f;
            camera.transform.position = new Vector3(690f, 860f, -700f);
            camera.transform.LookAt(new Vector3(VolcanoCenter.x, 82f, VolcanoCenter.y));

            RenderTexture renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            Texture2D capture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            capture.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            capture.Apply();
            File.WriteAllBytes("/tmp/twf-bright-large-island-hq-preview.png", capture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
        }

        private static void BuildIsland(bool showDialog)
        {
            EnsureFolders();
            RequireCoreAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject marker = new GameObject("HIGH QUALITY ASSET-BASED SURVIVAL ISLAND");
            marker.transform.position = Vector3.zero;

            EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.ResetOceanCanvas(1800f, 513);
            canvas.gameObject.name = "Walkable Island Terrain - Ultimate Stamp Volcano";
            canvas.GenerateBrightLargeSurvivalIsland();
            Texture2D volcanoStamp = StableIslandBuilderUtility.LoadPreferredVolcanoStamp();
            if (volcanoStamp == null)
            {
                throw new MissingReferenceException("Ultimate StampIt Volcano 07 could not be loaded.");
            }

            canvas.ApplyUltimateStampCalderaVolcano(volcanoStamp, VolcanoCenter, VolcanoRadius, 176f);
            ApplyTerrainMaterials(canvas);

            ConfigureOcean();
            ConfigureLighting();
            CompositionCounts counts = PopulateIsland(canvas);
            CreatePlayer(canvas);
            ValidateScene(canvas, counts);

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas savedCanvas = StableIslandBuilderUtility.GetCanvas();
            if (savedCanvas != null)
            {
                Selection.activeGameObject = savedCanvas.gameObject;
                FrameIsland(savedCanvas);
            }

            Debug.Log(
                $"HQ Ultimate Stamp volcano island built: {counts.Cliffs} cliffs, {counts.Palms} palms, " +
                $"{counts.Trees} broadleaf trees, {counts.Rocks} rocks.");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The high-quality survival island with a large Ultimate StampIt caldera volcano is ready and open.",
                    "OK");
            }
        }

        private static void RequireCoreAssets()
        {
            string[] required =
            {
                GrayCliffs[0],
                Palms[0],
                Rocks[0],
                BroadleafTrees[0],
                "Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Water Surface.mat"
            };

            foreach (string path in required)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null)
                {
                    throw new FileNotFoundException($"Required island asset is missing: {path}");
                }
            }
        }

        private static void ConfigureOcean()
        {
            GameObject ocean = FindSceneObject("Ocean - waves, visual only")
                ?? FindSceneObject("Ocean - visual only, no collider");
            if (ocean == null)
            {
                throw new MissingReferenceException("Ocean object was not created.");
            }

            ocean.name = "NVJOB Turquoise Ocean - Actual Water Asset";
            ocean.transform.position = new Vector3(0f, -0.14f, 0f);
            MeshRenderer renderer = ocean.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetTurquoiseWaterMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            SimpleWaveWater waves = ocean.GetComponent<SimpleWaveWater>();
            if (waves == null)
            {
                waves = ocean.AddComponent<SimpleWaveWater>();
            }

            waves.Configure(0.32f, 0.026f, 0.62f, 0.11f);
        }

        private static Material GetTurquoiseWaterMaterial()
        {
            EnsureFolders();
            const string targetPath = MaterialFolder + "/HQ Turquoise NVJOB Water.mat";
            Material target = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            if (target == null)
            {
                target = new Material(shader) { name = "HQ Turquoise NVJOB Water" };
                AssetDatabase.CreateAsset(target, targetPath);
            }
            else
            {
                target.shader = shader;
            }

            Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>(
                "Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water A 0.png");
            Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(
                "Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water N 0.png");
            SetTexture(target, albedo, "_BaseMap", "_MainTex");
            SetTexture(target, normal, "_BumpMap");
            SetColor(target, new Color(0.06f, 0.72f, 0.91f, 0.66f), "_BaseColor", "_Color");
            SetFloat(target, 0.82f, "_Smoothness", "_Glossiness");
            SetFloat(target, 0f, "_Metallic");
            SetFloat(target, 1f, "_Surface");
            SetFloat(target, 0f, "_ZWrite");
            SetFloat(target, (float)BlendMode.SrcAlpha, "_SrcBlend");
            SetFloat(target, (float)BlendMode.OneMinusSrcAlpha, "_DstBlend");
            SetFloat(target, 1f, "_EnvironmentReflections");
            SetFloat(target, 1f, "_SpecularHighlights");
            target.SetTextureScale("_BaseMap", new Vector2(42f, 42f));
            target.SetTextureScale("_BumpMap", new Vector2(52f, 52f));
            target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            target.EnableKeyword("_NORMALMAP");
            target.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            target.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            target.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(target);
            return target;
        }

        private static void ApplyTerrainMaterials(EditableIslandMeshCanvas canvas)
        {
            MeshRenderer renderer = canvas.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                throw new MissingReferenceException("Island terrain renderer is missing.");
            }

            renderer.sharedMaterials = new[]
            {
                GetTerrainMaterial(
                    "HQ Sunlit Beach Sand",
                    new Color(1f, 0.90f, 0.66f),
                    "Assets/YughuesFreeSandMaterials/Textures/T_YFSM_07_d.tga",
                    "Assets/YughuesFreeSandMaterials/Textures/T_YFSM_07_n.tga",
                    new Vector2(1.15f, 1.15f),
                    0.08f),
                GetTerrainMaterial(
                    "HQ Shallow Wet Sand",
                    new Color(0.76f, 0.86f, 0.73f),
                    "Assets/YughuesFreeSandMaterials/Textures/T_YFSM_03_d.tga",
                    "Assets/YughuesFreeSandMaterials/Textures/T_YFSM_03_n.tga",
                    new Vector2(1.2f, 1.2f),
                    0.16f),
                GetTerrainMaterial(
                    "HQ Warm Coastal Rock",
                    new Color(1.18f, 1.08f, 0.94f),
                    "Assets/AQUAS-Lite/Demo/Textures/Cliffs.jpg",
                    "Assets/AQUAS-Lite/Demo/Textures/Cliffs_normal.jpg",
                    new Vector2(0.72f, 0.72f),
                    0.11f),
                GetTerrainMaterial(
                    "HQ Lush Island Grass",
                    new Color(0.86f, 1.30f, 0.72f),
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Grass_02/Grass_02_Diffuse.tga",
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Grass_02/Grass_02_Normal.tga",
                    new Vector2(1.25f, 1.25f),
                    0.07f)
            };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static Material GetTerrainMaterial(
            string name,
            Color tint,
            string albedoPath,
            string normalPath,
            Vector2 tiling,
            float smoothness)
        {
            EnsureFolders();
            string path = $"{MaterialFolder}/{name}.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>(albedoPath);
            Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(normalPath);
            SetTexture(material, albedo, "_BaseMap", "_MainTex");
            SetTexture(material, normal, "_BumpMap");
            SetColor(material, tint, "_BaseColor", "_Color");
            SetFloat(material, smoothness, "_Smoothness", "_Glossiness");
            SetFloat(material, 0f, "_Metallic");
            SetFloat(material, 0f, "_Surface");
            SetFloat(material, 1f, "_ZWrite");
            SetFloat(material, (float)BlendMode.One, "_SrcBlend");
            SetFloat(material, (float)BlendMode.Zero, "_DstBlend");
            material.SetTextureScale("_BaseMap", tiling);
            material.SetTextureScale("_BumpMap", tiling);
            material.EnableKeyword("_NORMALMAP");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureLighting()
        {
            GameObject sun = new GameObject("Warm Tropical Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.76f);
            light.intensity = 1.22f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.72f;
            sun.transform.rotation = Quaternion.Euler(47f, -38f, 0f);

            Material sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/RockFREE/Textures/Skyboxes/SkyBoxDay.mat")
                ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/PalmTreePack/Textures/Sky/IBL_Sky.mat");
            if (sky != null && IsUsableShader(sky.shader))
            {
                RenderSettings.skybox = sky;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.43f, 0.68f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.43f, 0.57f, 0.43f);
            RenderSettings.ambientGroundColor = new Color(0.19f, 0.25f, 0.17f);
            RenderSettings.ambientIntensity = 1.1f;
            RenderSettings.reflectionIntensity = 0.72f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.56f, 0.83f, 0.94f);
            RenderSettings.fogStartDistance = 1650f;
            RenderSettings.fogEndDistance = 3900f;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 800f);

            GameObject probeObject = new GameObject("Island Reflection Probe");
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.size = new Vector3(1600f, 420f, 1400f);
            probe.center = new Vector3(0f, 90f, 0f);
            probe.intensity = 0.75f;
        }

        private static CompositionCounts PopulateIsland(EditableIslandMeshCanvas canvas)
        {
            CompositionCounts counts = new CompositionCounts();
            GameObject root = new GameObject("NATURAL ASSET COMPOSITION - No Buildings Or Bridges");
            Transform cliffRoot = CreateGroup(root.transform, "Blocky Cliff Formations - Coastal Walls And Highlands");
            Transform palmRoot = CreateGroup(root.transform, "PalmTreePack - Beach And Jungle Palms");
            Transform treeRoot = CreateGroup(root.transform, "Island Pack - Broadleaf Jungle");
            Transform rockRoot = CreateGroup(root.transform, "RockFREE - Shoreline And Trail Boulders");
            Transform beachRoot = CreateGroup(root.transform, "Yughues Sand - Conforming Beach Coves");
            Transform volcanoRoot = CreateGroup(root.transform, "Ultimate Stamp Volcano - Blocky Cliff Rim Details");

            CreateBeachPatches(canvas, beachRoot);
            CreateVolcanoSurfaceOverlay(canvas, volcanoRoot);
            CreateCraterFloor(canvas, volcanoRoot);
            counts.Cliffs = PlaceCliffFormations(canvas, cliffRoot);
            counts.Cliffs += PlaceVolcanoCliffs(canvas, volcanoRoot);
            counts.Palms = ScatterPalms(canvas, palmRoot);
            counts.Trees = ScatterBroadleafTrees(canvas, treeRoot);
            counts.Rocks = ScatterRocks(canvas, rockRoot);
            return counts;
        }

        private static int PlaceVolcanoCliffs(EditableIslandMeshCanvas canvas, Transform root)
        {
            int count = 0;
            for (int i = 0; i < 16; i++)
            {
                float angle = i / 16f * Mathf.PI * 2f + SignedHash(i * 4.17f) * 0.055f;
                float radius = Mathf.Lerp(76f, 96f, Hash01(i * 7.31f));
                Vector2 point = VolcanoCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector3 position = new Vector3(point.x, 0f, point.y);
                float ground = canvas.SampleHeightWorld(position);
                GameObject cliff = InstantiateAsset(DarkCliffs[i % DarkCliffs.Length], root);
                if (cliff == null)
                {
                    continue;
                }

                cliff.name = $"Dark Caldera Rim Cliff {i + 1:00}";
                cliff.transform.position = new Vector3(point.x, ground, point.y);
                cliff.transform.rotation = Quaternion.Euler(
                    SignedHash(i * 2.4f) * 7f,
                    -angle * Mathf.Rad2Deg + 90f + SignedHash(i * 8.6f) * 18f,
                    SignedHash(i * 5.7f) * 6f);
                ScalePrefabToHeight(
                    cliff,
                    Mathf.Lerp(10f, 18f, Hash01(i * 9.9f)),
                    ground - Mathf.Lerp(3f, 7f, Hash01(i * 3.2f)));
                EnsureSupportedMaterials(cliff);
                ApplyStylizedCliffMaterial(cliff, true);
                count++;
            }

            for (int i = 0; i < 22; i++)
            {
                float angle = i / 22f * Mathf.PI * 2f + SignedHash(i * 3.47f) * 0.13f;
                float radius = Mathf.Lerp(145f, 292f, Hash01(i * 11.3f));
                Vector2 point = VolcanoCenter + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector3 position = new Vector3(point.x, 0f, point.y);
                float ground = canvas.SampleHeightWorld(position);
                GameObject cliff = InstantiateAsset(
                    i % 3 == 0 ? DarkCliffs[i % DarkCliffs.Length] : GrayCliffs[i % GrayCliffs.Length],
                    root);
                if (cliff == null)
                {
                    continue;
                }

                cliff.name = $"Volcano Radial Ridge Outcrop {i + 1:00}";
                cliff.transform.position = new Vector3(point.x, ground, point.y);
                cliff.transform.rotation = Quaternion.Euler(
                    SignedHash(i * 4.2f) * 8f,
                    -angle * Mathf.Rad2Deg + SignedHash(i * 7.8f) * 24f,
                    SignedHash(i * 6.1f) * 7f);
                ScalePrefabToHeight(
                    cliff,
                    Mathf.Lerp(12f, 27f, Hash01(i * 5.9f)),
                    ground - Mathf.Lerp(3f, 8f, Hash01(i * 2.8f)));
                EnsureSupportedMaterials(cliff);
                ApplyStylizedCliffMaterial(cliff, i % 3 == 0);
                count++;
            }

            return count;
        }

        private static void CreateCraterFloor(EditableIslandMeshCanvas canvas, Transform root)
        {
            const float radius = 84f;
            float half = canvas.Size * 0.5f;
            float spacing = canvas.Size / canvas.QuadResolution;
            int minGridX = Mathf.Clamp(
                Mathf.FloorToInt((VolcanoCenter.x - radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int maxGridX = Mathf.Clamp(
                Mathf.CeilToInt((VolcanoCenter.x + radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int minGridZ = Mathf.Clamp(
                Mathf.FloorToInt((VolcanoCenter.y - radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int maxGridZ = Mathf.Clamp(
                Mathf.CeilToInt((VolcanoCenter.y + radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int xSegments = maxGridX - minGridX;
            int zSegments = maxGridZ - minGridZ;
            int columns = xSegments + 1;
            Vector3[] vertices = new Vector3[columns * (zSegments + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            bool[] valid = new bool[vertices.Length];
            float[] normalizedDistances = new float[vertices.Length];

            for (int z = 0; z <= zSegments; z++)
            {
                for (int x = 0; x <= xSegments; x++)
                {
                    float worldX = -half + (minGridX + x) * spacing;
                    float worldZ = -half + (minGridZ + z) * spacing;
                    float nx = (worldX - VolcanoCenter.x) / radius;
                    float nz = (worldZ - VolcanoCenter.y) / radius;
                    float angle = Mathf.Atan2(nz, nx);
                    float normalizedDistance = Mathf.Sqrt(nx * nx + nz * nz);
                    float irregularEdge = 0.98f
                        + Mathf.Sin(angle * 2.7f + 0.4f) * 0.075f
                        + Mathf.Sin(angle * 5.3f - 1.1f) * 0.028f;
                    Vector3 point = new Vector3(worldX, 0f, worldZ);
                    float height = canvas.SampleHeightWorld(point);
                    int index = z * columns + x;
                    vertices[index] = new Vector3(worldX, height + 0.48f, worldZ);
                    uvs[index] = new Vector2(worldX * 0.025f, worldZ * 0.025f);
                    normalizedDistances[index] = normalizedDistance;
                    valid[index] = normalizedDistance <= irregularEdge;
                }
            }

            List<int>[] subTriangles =
            {
                new List<int>(xSegments * zSegments * 3),
                new List<int>(xSegments * zSegments * 3)
            };
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int a = z * columns + x;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    if (valid[a] && valid[b] && valid[c] && valid[d])
                    {
                        float averageDistance = (normalizedDistances[a] + normalizedDistances[b]
                            + normalizedDistances[c] + normalizedDistances[d]) * 0.25f;
                        float centerX = (vertices[a].x + vertices[d].x) * 0.5f;
                        float centerZ = (vertices[a].z + vertices[d].z) * 0.5f;
                        float angle = Mathf.Atan2(centerZ - VolcanoCenter.y, centerX - VolcanoCenter.x);
                        float floorEdge = 0.61f + Mathf.Sin(angle * 3.4f - 0.6f) * 0.055f;
                        int layer = averageDistance <= floorEdge ? 0 : 1;
                        subTriangles[layer].Add(a);
                        subTriangles[layer].Add(c);
                        subTriangles[layer].Add(b);
                        subTriangles[layer].Add(b);
                        subTriangles[layer].Add(c);
                        subTriangles[layer].Add(d);
                    }
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Stylized Caldera Interior Mesh",
                vertices = vertices,
                uv = uvs,
                subMeshCount = 2
            };
            for (int i = 0; i < subTriangles.Length; i++)
            {
                mesh.SetTriangles(subTriangles[i], i);
            }

            mesh.RecalculateNormals();
            Vector3[] normals = mesh.normals;
            Vector3 fakeSunDirection = new Vector3(-0.38f, 0.82f, -0.43f).normalized;
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float light = Mathf.Clamp01((Vector3.Dot(normals[i], fakeSunDirection) + 0.05f) / 0.92f);
                float brightness = Mathf.Lerp(0.68f, 1.12f, light);
                colors[i] = new Color(brightness * 1.03f, brightness, brightness * 0.96f, 1f);
            }

            mesh.colors = colors;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            GameObject crater = new GameObject("Visible Caldera Walls And Floor");
            crater.transform.SetParent(root);
            crater.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = crater.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = GetCraterMaterials();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static void CreateVolcanoSurfaceOverlay(EditableIslandMeshCanvas canvas, Transform root)
        {
            const float radius = 334f;
            float half = canvas.Size * 0.5f;
            float spacing = canvas.Size / canvas.QuadResolution;
            int minGridX = Mathf.Clamp(
                Mathf.FloorToInt((VolcanoCenter.x - radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int maxGridX = Mathf.Clamp(
                Mathf.CeilToInt((VolcanoCenter.x + radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int minGridZ = Mathf.Clamp(
                Mathf.FloorToInt((VolcanoCenter.y - radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int maxGridZ = Mathf.Clamp(
                Mathf.CeilToInt((VolcanoCenter.y + radius + half) / spacing),
                0,
                canvas.QuadResolution);
            int xSegments = maxGridX - minGridX;
            int zSegments = maxGridZ - minGridZ;
            int columns = xSegments + 1;
            Vector3[] vertices = new Vector3[columns * (zSegments + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            bool[] valid = new bool[vertices.Length];
            float[] normalizedDistances = new float[vertices.Length];
            float[] angles = new float[vertices.Length];

            for (int z = 0; z <= zSegments; z++)
            {
                for (int x = 0; x <= xSegments; x++)
                {
                    float worldX = -half + (minGridX + x) * spacing;
                    float worldZ = -half + (minGridZ + z) * spacing;
                    float nx = (worldX - VolcanoCenter.x) / radius;
                    float nz = (worldZ - VolcanoCenter.y) / radius;
                    Vector3 point = new Vector3(worldX, 0f, worldZ);
                    float height = canvas.SampleHeightWorld(point);
                    float normalizedDistance = Mathf.Sqrt(nx * nx + nz * nz);
                    float angle = Mathf.Atan2(nz, nx);
                    float irregularOuter = 0.965f
                        + Mathf.Sin(angle * 3.1f + 0.6f) * 0.047f
                        + Mathf.Sin(angle * 7.4f - 1.2f) * 0.025f
                        + (Mathf.PerlinNoise(worldX * 0.008f + 18f, worldZ * 0.008f + 43f) - 0.5f) * 0.075f;
                    float irregularInner = 0.18f
                        + Mathf.Sin(angle * 4.3f + 0.3f) * 0.014f
                        + Mathf.Sin(angle * 9.7f) * 0.008f;
                    int index = z * columns + x;
                    vertices[index] = new Vector3(worldX, height + 0.42f, worldZ);
                    uvs[index] = new Vector2(worldX * 0.018f, worldZ * 0.018f);
                    normalizedDistances[index] = normalizedDistance;
                    angles[index] = angle;
                    valid[index] = normalizedDistance >= irregularInner
                        && normalizedDistance <= irregularOuter
                        && height >= canvas.WaterLevel + 12f;
                }
            }

            List<int>[] subTriangles =
            {
                new List<int>(xSegments * zSegments * 2),
                new List<int>(xSegments * zSegments * 2),
                new List<int>(xSegments * zSegments * 2)
            };
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int a = z * columns + x;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    if (valid[a] && valid[b] && valid[c] && valid[d])
                    {
                        float averageDistance = (normalizedDistances[a] + normalizedDistances[b]
                            + normalizedDistances[c] + normalizedDistances[d]) * 0.25f;
                        float averageAngle = Mathf.Atan2(
                            Mathf.Sin(angles[a]) + Mathf.Sin(angles[b]) + Mathf.Sin(angles[c]) + Mathf.Sin(angles[d]),
                            Mathf.Cos(angles[a]) + Mathf.Cos(angles[b]) + Mathf.Cos(angles[c]) + Mathf.Cos(angles[d]));
                        float largePatch = Mathf.PerlinNoise(
                            (vertices[a].x + 900f) * 0.0065f,
                            (vertices[a].z + 900f) * 0.0065f) - 0.5f;
                        float summitBoundary = 0.36f
                            + Mathf.Sin(averageAngle * 2.6f + 0.4f) * 0.035f
                            + largePatch * 0.065f;
                        float rockBoundary = 0.69f
                            + Mathf.Sin(averageAngle * 3.3f - 0.7f) * 0.045f
                            + largePatch * 0.085f;
                        int layer = averageDistance <= summitBoundary ? 2
                            : averageDistance <= rockBoundary ? 1
                            : 0;
                        subTriangles[layer].Add(a);
                        subTriangles[layer].Add(c);
                        subTriangles[layer].Add(b);
                        subTriangles[layer].Add(b);
                        subTriangles[layer].Add(c);
                        subTriangles[layer].Add(d);
                    }
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Stylized Three Band Volcanic Surface",
                vertices = vertices,
                uv = uvs,
                subMeshCount = 3
            };
            for (int i = 0; i < subTriangles.Length; i++)
            {
                mesh.SetTriangles(subTriangles[i], i);
            }

            mesh.RecalculateNormals();
            Vector3 fakeSunDirection = new Vector3(-0.38f, 0.82f, -0.43f).normalized;
            Vector3[] normals = mesh.normals;
            Color[] colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                float light = Mathf.Clamp01((Vector3.Dot(normals[i], fakeSunDirection) + 0.18f) / 1.05f);
                light = light * light * (3f - 2f * light);
                float largeDetail = (Mathf.PerlinNoise(vertices[i].x * 0.006f + 41f, vertices[i].z * 0.006f + 73f) - 0.5f) * 0.08f;
                float brightness = Mathf.Clamp(Mathf.Lerp(0.72f, 1.18f, light) + largeDetail, 0.64f, 1.24f);
                colors[i] = new Color(brightness * 1.025f, brightness, brightness * 0.97f, 1f);
            }

            mesh.colors = colors;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            GameObject surface = new GameObject("Stylized Volcano - Earth Rock And Charcoal Bands");
            surface.transform.SetParent(root);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = GetVolcanoSurfaceMaterials();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static Material[] GetVolcanoSurfaceMaterials()
        {
            return new[]
            {
                GetFlatVolcanoMaterial(
                    "HQ Volcano Base Muted Earth",
                    new Color(0.42f, 0.33f, 0.26f, 1f)),
                GetFlatVolcanoMaterial(
                    "HQ Volcano Mid Gray Brown Rock",
                    new Color(0.34f, 0.31f, 0.28f, 1f)),
                GetFlatVolcanoMaterial(
                    "HQ Volcano Summit Charcoal",
                    new Color(0.255f, 0.265f, 0.255f, 1f))
            };
        }

        private static Material[] GetCraterMaterials()
        {
            return new[]
            {
                GetFlatVolcanoMaterial(
                    "HQ Caldera Floor Dark Brown",
                    new Color(0.235f, 0.19f, 0.165f, 1f)),
                GetFlatVolcanoMaterial(
                    "HQ Caldera Inner Wall Gray Brown",
                    new Color(0.31f, 0.275f, 0.245f, 1f))
            };
        }

        private static Material GetFlatVolcanoMaterial(string materialName, Color color)
        {
            EnsureFolders();
            string path = MaterialFolder + "/" + materialName + ".mat";
            Shader shader = Shader.Find("This Will Float/HQ Volcano Vertex Shade")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            SetTexture(material, null, "_BaseMap", "_MainTex");
            SetColor(material, color, "_BaseColor", "_Color");
            SetFloat(material, 0f, "_Surface");
            SetFloat(material, 1f, "_ZWrite");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateBeachPatches(EditableIslandMeshCanvas canvas, Transform root)
        {
            Material sand = GetBeachOverlayMaterial();
            if (sand == null)
            {
                throw new MissingReferenceException("Beach sand material is missing.");
            }

            CreateConformingBeach(
                canvas,
                root,
                "Wide South Crescent Beach",
                new Vector2(-85f, -400f),
                new Vector2(310f, 122f),
                2f,
                sand);
            CreateConformingBeach(
                canvas,
                root,
                "Sheltered West Cove Beach",
                new Vector2(-500f, -205f),
                new Vector2(160f, 150f),
                -16f,
                sand);
            CreateConformingBeach(
                canvas,
                root,
                "Southeast Resource Beach",
                new Vector2(305f, -390f),
                new Vector2(215f, 102f),
                -10f,
                sand);
        }

        private static Material GetBeachOverlayMaterial()
        {
            EnsureFolders();
            const string path = MaterialFolder + "/HQ Bright Yughues Beach Overlay.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "HQ Bright Yughues Beach Overlay" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture sand = AssetDatabase.LoadAssetAtPath<Texture>(
                "Assets/YughuesFreeSandMaterials/Textures/T_YFSM_07_d.tga");
            SetTexture(material, sand, "_BaseMap", "_MainTex");
            SetColor(material, new Color(1f, 0.86f, 0.60f, 1f), "_BaseColor", "_Color");
            SetFloat(material, 0f, "_Surface");
            SetFloat(material, 1f, "_ZWrite");
            material.SetTextureScale("_BaseMap", new Vector2(1.15f, 1.15f));
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateConformingBeach(
            EditableIslandMeshCanvas canvas,
            Transform root,
            string name,
            Vector2 center,
            Vector2 radii,
            float rotationDegrees,
            Material material)
        {
            const int xSegments = 128;
            const int zSegments = 64;
            int columns = xSegments + 1;
            Vector3[] vertices = new Vector3[columns * (zSegments + 1)];
            Vector2[] uvs = new Vector2[vertices.Length];
            bool[] valid = new bool[vertices.Length];
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);

            for (int z = 0; z <= zSegments; z++)
            {
                for (int x = 0; x <= xSegments; x++)
                {
                    float nx = x / (float)xSegments * 2f - 1f;
                    float nz = z / (float)zSegments * 2f - 1f;
                    float localX = nx * radii.x;
                    float localZ = nz * radii.y;
                    float worldX = center.x + localX * cos - localZ * sin;
                    float worldZ = center.y + localX * sin + localZ * cos;
                    Vector3 point = new Vector3(worldX, 0f, worldZ);
                    float height = canvas.SampleHeightWorld(point);
                    int index = z * columns + x;
                    vertices[index] = new Vector3(worldX, height + 0.18f, worldZ);
                    uvs[index] = new Vector2(worldX * 0.085f, worldZ * 0.085f);
                    valid[index] = nx * nx + nz * nz <= 1f
                        && height >= canvas.WaterLevel + 0.35f
                        && height <= canvas.WaterLevel + 10.5f;
                }
            }

            List<int> triangles = new List<int>(xSegments * zSegments * 6);
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int a = z * columns + x;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    if (valid[a] && valid[b] && valid[c] && valid[d])
                    {
                        triangles.Add(a);
                        triangles.Add(c);
                        triangles.Add(b);
                        triangles.Add(b);
                        triangles.Add(c);
                        triangles.Add(d);
                    }
                }
            }

            if (triangles.Count == 0)
            {
                throw new InvalidOperationException($"No visible sand triangles were generated for {name}.");
            }

            Mesh mesh = new Mesh
            {
                name = name + " Mesh",
                vertices = vertices,
                uv = uvs,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            GameObject beach = new GameObject(name);
            beach.transform.SetParent(root);
            beach.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = beach.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static int PlaceCliffFormations(EditableIslandMeshCanvas canvas, Transform root)
        {
            int count = 0;

            for (int i = 0; i < 34; i++)
            {
                float t = i / 33f;
                float x = Mathf.Lerp(-520f, 500f, t);
                float z = 265f + Mathf.Sin(t * Mathf.PI * 3.2f) * 62f + SignedHash(i * 2.17f) * 22f;
                count += PlaceCliff(canvas, root, new Vector2(x, z), Mathf.Lerp(42f, 78f, Hash01(i * 6.3f)), i, false);
            }

            for (int i = 0; i < 27; i++)
            {
                float t = i / 26f;
                float x = 420f + Mathf.Sin(t * Mathf.PI * 2.4f) * 75f + SignedHash(i * 3.31f) * 18f;
                float z = Mathf.Lerp(-335f, 350f, t);
                count += PlaceCliff(canvas, root, new Vector2(x, z), Mathf.Lerp(34f, 68f, Hash01(i * 7.7f)), 100 + i, false);
            }

            for (int i = 0; i < 17; i++)
            {
                float t = i / 16f;
                float x = -475f + Mathf.Sin(t * Mathf.PI * 2f) * 72f;
                float z = Mathf.Lerp(-270f, 185f, t) + SignedHash(i * 4.19f) * 24f;
                count += PlaceCliff(canvas, root, new Vector2(x, z), Mathf.Lerp(25f, 52f, Hash01(i * 8.8f)), 200 + i, false);
            }

            Vector2[] coastalStacks =
            {
                new Vector2(-555f, -315f), new Vector2(-480f, -405f), new Vector2(-345f, -485f),
                new Vector2(305f, -505f), new Vector2(455f, -420f), new Vector2(565f, -235f),
                new Vector2(590f, 30f), new Vector2(535f, 235f), new Vector2(-585f, 95f),
                new Vector2(-610f, -85f), new Vector2(170f, 465f), new Vector2(-210f, 480f)
            };

            for (int i = 0; i < coastalStacks.Length; i++)
            {
                count += PlaceCliff(
                    canvas,
                    root,
                    coastalStacks[i],
                    Mathf.Lerp(24f, 46f, Hash01(310f + i * 4.7f)),
                    300 + i,
                    true);
            }

            return count;
        }

        private static int PlaceCliff(
            EditableIslandMeshCanvas canvas,
            Transform root,
            Vector2 point,
            float targetHeight,
            int seed,
            bool coastal)
        {
            Vector3 position = new Vector3(point.x, 0f, point.y);
            float ground = canvas.SampleHeightWorld(position);
            if (ground < canvas.WaterLevel - 1.5f || ground > canvas.WaterLevel + 128f)
            {
                return 0;
            }

            string[] source = seed % 5 == 0 ? DarkCliffs : GrayCliffs;
            GameObject instance = InstantiateAsset(source[Mathf.Abs(seed) % source.Length], root);
            if (instance == null)
            {
                return 0;
            }

            instance.name = $"Asset Cliff Formation {seed:000}";
            instance.transform.position = new Vector3(point.x, ground, point.y);
            instance.transform.rotation = Quaternion.Euler(
                SignedHash(seed * 1.23f) * 5f,
                Hash01(seed * 5.61f) * 360f,
                SignedHash(seed * 8.17f) * 4f);
            float bottom = coastal
                ? Mathf.Min(ground - 2f, canvas.WaterLevel - 3.5f)
                : ground - Mathf.Lerp(3f, 9f, Hash01(seed * 3.44f));
            ScalePrefabToHeight(instance, targetHeight, bottom);
            EnsureSupportedMaterials(instance);
            ApplyStylizedCliffMaterial(instance, seed % 5 == 0);
            return 1;
        }

        private static int ScatterPalms(EditableIslandMeshCanvas canvas, Transform root)
        {
            System.Random random = new System.Random(76193);
            List<Vector2> occupied = new List<Vector2>();
            int placed = 0;
            for (int attempt = 0; attempt < 7600 && placed < 175; attempt++)
            {
                float x = NextRange(random, -640f, 640f);
                float z = NextRange(random, -510f, 500f);
                Vector3 point = new Vector3(x, 0f, z);
                float height = canvas.SampleHeightWorld(point) - canvas.WaterLevel;
                if (height < 2.2f || height > 38f || IsReservedTraversalZone(x, z) || IsVolcanoClearZone(x, z))
                {
                    continue;
                }

                bool coastal = height < 13f;
                float minSpacing = coastal ? 17f : 21f;
                if (!IsFarEnough(occupied, new Vector2(x, z), minSpacing))
                {
                    continue;
                }

                int layer = canvas.SamplePaintLayerWorld(point);
                if (!coastal && layer == 2)
                {
                    continue;
                }

                point.y = canvas.SampleHeightWorld(point);
                GameObject palm = InstantiateAsset(Palms[random.Next(Palms.Length)], root);
                if (palm == null)
                {
                    continue;
                }

                palm.name = $"PalmTreePack Palm {placed + 1:000}";
                palm.transform.position = point;
                palm.transform.rotation = Quaternion.Euler(
                    NextRange(random, -3f, 4f),
                    NextRange(random, 0f, 360f),
                    NextRange(random, -3f, 3f));
                ScalePrefabToHeight(palm, NextRange(random, 15f, coastal ? 25f : 29f), point.y - 0.18f);
                EnsureSupportedMaterials(palm);
                EnsureTreeCollider(palm);
                occupied.Add(new Vector2(x, z));
                placed++;
            }

            return placed;
        }

        private static int ScatterBroadleafTrees(EditableIslandMeshCanvas canvas, Transform root)
        {
            System.Random random = new System.Random(48721);
            List<Vector2> occupied = new List<Vector2>();
            int placed = 0;
            for (int attempt = 0; attempt < 8200 && placed < 145; attempt++)
            {
                float x = NextRange(random, -560f, 555f);
                float z = NextRange(random, -430f, 455f);
                Vector3 point = new Vector3(x, 0f, z);
                float height = canvas.SampleHeightWorld(point) - canvas.WaterLevel;
                if (height < 7f || height > 66f || IsReservedTraversalZone(x, z) || IsVolcanoClearZone(x, z))
                {
                    continue;
                }

                if (canvas.SamplePaintLayerWorld(point) == 2
                    || !IsFarEnough(occupied, new Vector2(x, z), 23f))
                {
                    continue;
                }

                point.y = canvas.SampleHeightWorld(point);
                GameObject tree = InstantiateAsset(BroadleafTrees[random.Next(BroadleafTrees.Length)], root);
                if (tree == null)
                {
                    continue;
                }

                tree.name = $"Island Pack Broadleaf Tree {placed + 1:000}";
                tree.transform.position = point;
                tree.transform.rotation = Quaternion.Euler(0f, NextRange(random, 0f, 360f), 0f);
                ScalePrefabToHeight(tree, NextRange(random, 18f, 30f), point.y - 0.15f);
                EnsureSupportedMaterials(tree);
                EnsureTreeCollider(tree);
                occupied.Add(new Vector2(x, z));
                placed++;
            }

            return placed;
        }

        private static int ScatterRocks(EditableIslandMeshCanvas canvas, Transform root)
        {
            System.Random random = new System.Random(21947);
            List<Vector2> occupied = new List<Vector2>();
            int placed = 0;
            for (int attempt = 0; attempt < 4400 && placed < 96; attempt++)
            {
                float angle = NextRange(random, 0f, Mathf.PI * 2f);
                float radius = NextRange(random, 250f, 680f);
                float x = Mathf.Cos(angle) * radius + NextRange(random, -45f, 45f);
                float z = Mathf.Sin(angle) * radius * 0.82f + NextRange(random, -35f, 35f);
                Vector3 point = new Vector3(x, 0f, z);
                float height = canvas.SampleHeightWorld(point) - canvas.WaterLevel;
                if (height < 0.7f || height > 54f || IsReservedTraversalZone(x, z))
                {
                    continue;
                }

                if (!IsFarEnough(occupied, new Vector2(x, z), height < 10f ? 19f : 27f))
                {
                    continue;
                }

                point.y = canvas.SampleHeightWorld(point);
                GameObject rock = InstantiateAsset(Rocks[random.Next(Rocks.Length)], root);
                if (rock == null)
                {
                    continue;
                }

                rock.name = $"Actual Asset Boulder {placed + 1:000}";
                rock.transform.position = point;
                rock.transform.rotation = Quaternion.Euler(
                    NextRange(random, -8f, 12f),
                    NextRange(random, 0f, 360f),
                    NextRange(random, -7f, 9f));
                float targetHeight = height < 10f
                    ? NextRange(random, 4f, 14f)
                    : NextRange(random, 3f, 9f);
                ScalePrefabToHeight(rock, targetHeight, point.y - NextRange(random, 0.3f, 1.6f));
                EnsureSupportedMaterials(rock);
                occupied.Add(new Vector2(x, z));
                placed++;
            }

            return placed;
        }

        private static bool IsReservedTraversalZone(float x, float z)
        {
            Vector2 point = new Vector2(x, z);
            if ((point - new Vector2(-330f, -205f)).sqrMagnitude < 105f * 105f)
            {
                return true;
            }

            Vector2 pathStart = new Vector2(-330f, -205f);
            Vector2 pathEnd = VolcanoCenter + new Vector2(-VolcanoRadius * 0.82f, -VolcanoRadius * 0.28f);
            Vector2 segment = pathEnd - pathStart;
            float t = Mathf.Clamp01(Vector2.Dot(point - pathStart, segment) / segment.sqrMagnitude);
            return Vector2.Distance(point, pathStart + segment * t) < 24f;
        }

        private static bool IsVolcanoClearZone(float x, float z)
        {
            return (new Vector2(x, z) - VolcanoCenter).sqrMagnitude < 302f * 302f;
        }

        private static bool IsFarEnough(List<Vector2> points, Vector2 candidate, float minimumDistance)
        {
            float squared = minimumDistance * minimumDistance;
            foreach (Vector2 point in points)
            {
                if ((point - candidate).sqrMagnitude < squared)
                {
                    return false;
                }
            }

            return true;
        }

        private static void CreatePlayer(EditableIslandMeshCanvas canvas)
        {
            Vector3 start = new Vector3(-330f, 0f, -205f);
            start.y = canvas.SampleHeightWorld(start) + 0.2f;
            Quaternion rotation = Quaternion.Euler(0f, 22f, 0f);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Survival Player - Central Meadow Start";
            player.transform.SetPositionAndRotation(start, rotation);
            Renderer playerRenderer = player.GetComponent<Renderer>();
            if (playerRenderer != null)
            {
                playerRenderer.enabled = false;
            }

            Collider primitiveCollider = player.GetComponent<Collider>();
            if (primitiveCollider != null)
            {
                Object.DestroyImmediate(primitiveCollider);
            }

            CharacterController characterController = player.AddComponent<CharacterController>();
            characterController.height = 1.8f;
            characterController.radius = 0.35f;
            characterController.center = new Vector3(0f, 0.9f, 0f);

            GameObject cameraObject = new GameObject("Survival First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 2800f;
            cameraObject.AddComponent<AudioListener>();

            DemoFirstPersonPlayer controller = player.AddComponent<DemoFirstPersonPlayer>();
            SerializedObject serialized = new SerializedObject(controller);
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("groundMask").intValue = ~0;
            serialized.FindProperty("startPosition").vector3Value = start;
            serialized.FindProperty("startRotation").quaternionValue = rotation;
            serialized.FindProperty("resetIfBelowY").floatValue = -12f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject InstantiateAsset(string path, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Island prefab could not be loaded: {path}");
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance != null)
            {
                instance.transform.SetParent(parent);
            }

            return instance;
        }

        private static void ScalePrefabToHeight(GameObject instance, float targetHeight, float targetBottom)
        {
            Bounds bounds = CalculateBounds(instance);
            if (bounds.size.y < 0.001f)
            {
                return;
            }

            float factor = targetHeight / bounds.size.y;
            instance.transform.localScale *= factor;
            bounds = CalculateBounds(instance);
            instance.transform.position += Vector3.up * (targetBottom - bounds.min.y);
        }

        private static Bounds CalculateBounds(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = new Bounds(instance.transform.position, Vector3.zero);
            bool initialized = false;
            foreach (Renderer renderer in renderers)
            {
                if (!initialized)
                {
                    bounds = renderer.bounds;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static void EnsureTreeCollider(GameObject tree)
        {
            if (tree.GetComponentInChildren<Collider>() != null)
            {
                return;
            }

            Bounds bounds = CalculateBounds(tree);
            CapsuleCollider collider = tree.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.09f, 0.22f, 1.15f);
            collider.height = Mathf.Max(collider.radius * 2f, bounds.size.y * 0.72f);
            Vector3 localCenter = tree.transform.InverseTransformPoint(
                new Vector3(bounds.center.x, bounds.min.y + collider.height * 0.5f, bounds.center.z));
            collider.center = localCenter;
        }

        private static void EnsureSupportedMaterials(GameObject instance)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    Material source = materials[i];
                    if (source == null || IsUsableShader(source.shader))
                    {
                        continue;
                    }

                    materials[i] = GetCompatibleMaterial(source);
                    changed = true;
                }

                if (changed)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private static void ApplyStylizedCliffMaterial(GameObject instance, bool dark)
        {
            Material material = GetStylizedCliffMaterial(dark);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] source = renderer.sharedMaterials;
                Material[] replacements = new Material[Mathf.Max(1, source.Length)];
                for (int i = 0; i < replacements.Length; i++)
                {
                    replacements[i] = material;
                }

                renderer.sharedMaterials = replacements;
            }
        }

        private static Material GetStylizedCliffMaterial(bool dark)
        {
            EnsureFolders();
            string name = dark ? "HQ Stylized Dark Blocky Cliff" : "HQ Stylized Gray Blocky Cliff";
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Texture")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>("Assets/AQUAS-Lite/Demo/Textures/Cliffs.jpg");
            Texture normal = AssetDatabase.LoadAssetAtPath<Texture>("Assets/AQUAS-Lite/Demo/Textures/Cliffs_normal.jpg");
            SetTexture(material, albedo, "_BaseMap", "_MainTex");
            SetTexture(material, normal, "_BumpMap");
            SetColor(
                material,
                dark ? new Color(0.60f, 0.56f, 0.50f, 1f) : new Color(1.02f, 0.98f, 0.88f, 1f),
                "_BaseColor",
                "_Color");
            SetFloat(material, dark ? 0.08f : 0.12f, "_Smoothness", "_Glossiness");
            SetFloat(material, 0f, "_Metallic");
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureScale("_BumpMap", Vector2.one);
            material.EnableKeyword("_NORMALMAP");
            material.renderQueue = -1;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetCompatibleMaterial(Material source)
        {
            EnsureFolders();
            string safeName = string.Concat(source.name.Split(Path.GetInvalidFileNameChars()));
            string path = $"{MaterialFolder}/URP Converted {safeName}.mat";
            Material converted = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
            if (converted == null)
            {
                converted = new Material(shader) { name = "URP Converted " + source.name };
                AssetDatabase.CreateAsset(converted, path);
            }

            Texture main = GetTexture(source, "_BaseMap", "_MainTex", "_Albedo");
            Texture normal = GetTexture(source, "_BumpMap", "_NormalMap");
            Color color = GetColor(source, "_BaseColor", "_Color");
            SetTexture(converted, main, "_BaseMap", "_MainTex");
            SetTexture(converted, normal, "_BumpMap");
            SetColor(converted, color, "_BaseColor", "_Color");
            SetFloat(converted, 0.18f, "_Smoothness", "_Glossiness");
            if (normal != null)
            {
                converted.EnableKeyword("_NORMALMAP");
            }

            if (color.a < 0.98f || source.renderQueue >= (int)RenderQueue.AlphaTest)
            {
                SetFloat(converted, 1f, "_AlphaClip");
                SetFloat(converted, 0.36f, "_Cutoff");
                converted.EnableKeyword("_ALPHATEST_ON");
                converted.renderQueue = (int)RenderQueue.AlphaTest;
            }

            EditorUtility.SetDirty(converted);
            return converted;
        }

        private static Texture GetTexture(Material material, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static Color GetColor(Material material, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    return material.GetColor(property);
                }
            }

            return Color.white;
        }

        private static void ValidateScene(EditableIslandMeshCanvas canvas, CompositionCounts counts)
        {
            if (counts.Cliffs < 70 || counts.Palms < 150 || counts.Trees < 120 || counts.Rocks < 70)
            {
                throw new InvalidOperationException(
                    $"Island population was incomplete: cliffs {counts.Cliffs}, palms {counts.Palms}, " +
                    $"trees {counts.Trees}, rocks {counts.Rocks}.");
            }

            MeshRenderer terrainRenderer = canvas.GetComponent<MeshRenderer>();
            if (terrainRenderer == null || terrainRenderer.sharedMaterials.Length < 4)
            {
                throw new InvalidOperationException("Terrain materials were not assigned.");
            }

            Mesh terrainMesh = canvas.GetComponent<MeshFilter>()?.sharedMesh;
            if (terrainMesh != null)
            {
                for (int i = 0; i < Mathf.Min(terrainMesh.subMeshCount, terrainRenderer.sharedMaterials.Length); i++)
                {
                    Debug.Log(
                        $"Terrain layer {i}: {terrainRenderer.sharedMaterials[i].name}, " +
                        $"{terrainMesh.GetIndexCount(i) / 3} triangles.");
                }
            }

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>())
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        throw new MissingReferenceException($"Missing material on {renderer.name}.");
                    }

                    if (!IsUsableShader(material.shader))
                    {
                        throw new InvalidOperationException(
                            $"Unsupported shader remains on {renderer.name}: {material.name}");
                    }
                }
            }
        }

        private static bool IsUsableShader(Shader shader)
        {
            return shader != null
                && shader.isSupported
                && shader.name != "Hidden/InternalErrorShader";
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent);
            return group.transform;
        }

        private static GameObject FindSceneObject(string objectName)
        {
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj != null
                    && obj.scene.IsValid()
                    && !EditorUtility.IsPersistent(obj)
                    && obj.name == objectName)
                {
                    return obj;
                }
            }

            return null;
        }

        private static void FrameIsland(EditableIslandMeshCanvas canvas)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || canvas == null)
            {
                return;
            }

            view.in2DMode = false;
            view.LookAt(
                new Vector3(VolcanoCenter.x, 86f, VolcanoCenter.y),
                Quaternion.Euler(57f, -42f, 0f),
                canvas.Size * 0.40f);
            view.Repaint();
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder(MaterialFolder);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void SetTexture(Material material, Texture texture, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    material.SetTexture(property, texture);
                }
            }
        }

        private static void SetColor(Material material, Color color, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    material.SetColor(property, color);
                }
            }
        }

        private static void SetFloat(Material material, float value, params string[] properties)
        {
            foreach (string property in properties)
            {
                if (material.HasProperty(property))
                {
                    material.SetFloat(property, value);
                }
            }
        }

        private static float Hash01(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
        }

        private static float SignedHash(float value)
        {
            return Hash01(value) * 2f - 1f;
        }

        private static float NextRange(System.Random random, float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)random.NextDouble());
        }

        private struct CompositionCounts
        {
            public int Cliffs;
            public int Palms;
            public int Trees;
            public int Rocks;
        }
    }
}
