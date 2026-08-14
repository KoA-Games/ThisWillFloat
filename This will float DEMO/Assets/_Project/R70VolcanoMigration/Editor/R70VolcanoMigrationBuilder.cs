#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using ThisWillFloat.R70Migration;

namespace ThisWillFloat.EditorTools
{
    public static class R70VolcanoMigrationBuilder
    {
        private const string Root = "Assets/_Project/R70VolcanoMigration";
        private const string Source = Root + "/Source";
        private const string Generated = Root + "/Generated";
        private const string ScenePath = "Assets/_Project/Scenes/R70_Volcano_UnityMigration.unity";
        private const string VolcanoFbx = Source + "/Meshes/R70_Volcano.fbx";
        private const string PrototypeFbx = Source + "/Meshes/R70_VegetationPrototypes.fbx";
        private const string PlacementFbx = Source + "/Meshes/R70_VegetationPlacementTransforms.fbx";
        private const string ReviewRigFbx = Source + "/Meshes/R70_ReviewRig.fbx";
        private const string PlacementJson = Source + "/R70_VegetationPlacement.json";
        private const string MaterialJson = Source + "/R70_VegetationMaterials.json";
        private const string R70PipelinePath = Generated + "/R70_LongRangeShadow_RPAsset.asset";
        private const string PcPipelinePath = "Assets/Settings/PC_RPAsset.asset";

        [Serializable]
        private sealed class PlacementSet { public Placement[] placements; }

        [Serializable]
        private sealed class Placement
        {
            public string name;
            public string source;
            public string[] materials;
            public string sourceObject;
            public string sourceCollection;
        }

        [Serializable]
        private sealed class MaterialSet { public MaterialDefinition[] materials; }

        [Serializable]
        private sealed class MaterialDefinition
        {
            public string name;
            public float[] color;
            public float roughness;
            public bool doubleSided;
            public bool useVertexColor;
            public float vertexColorWeight;
            public float noiseScale;
            public float noiseStrength;
        }

        [MenuItem("This Will Float/R70 Migration/Build High-Fidelity Unity Scene")]
        public static void BuildFromMenu() => Build();

        public static void BuildBatch()
        {
            try
            {
                Build();
                Debug.Log("R70_UNITY_MIGRATION_BATCH_SUCCESS");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void ValidateBatch()
        {
            try
            {
                if (!File.Exists(ToDiskPath(ScenePath)))
                    throw new FileNotFoundException("Generated R70 scene is missing", ScenePath);

                Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                GameObject root = GameObject.Find("R70_VOLCANO_MIGRATION");
                if (root == null) throw new InvalidOperationException("R70 root object missing");

                Transform vegetation = root.transform.Find("Vegetation");
                Transform volcano = root.transform.Find("Volcano");
                if (vegetation == null || volcano == null) throw new InvalidOperationException("Required scene groups missing");

                int renderers = root.GetComponentsInChildren<MeshRenderer>(true).Length;
                int meshFilters = root.GetComponentsInChildren<MeshFilter>(true).Length;
                // 2,100 source renderers minus 122 intentionally excluded exposed
                // DUF helper instances, plus volcano/water review geometry.
                if (renderers < 1_978 || meshFilters < 1_978)
                    throw new InvalidOperationException($"Unexpected mesh count: renderers={renderers}, filters={meshFilters}");

                Transform mixedFill = vegetation.Find("R70_LowerGreen_MixedFill");
                int mixedFillCount = mixedFill?.GetComponentsInChildren<MeshRenderer>(true).Length ?? 0;
                if (mixedFillCount != 960)
                    throw new InvalidOperationException($"Lower-green mixed fill is incomplete: {mixedFillCount}/960");
                Transform[] addedTrees = mixedFill.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.Contains("Tree", StringComparison.Ordinal))
                    .ToArray();
                float minimumTreeHeightRatio = float.PositiveInfinity;
                float maximumTreeBaseGapRatio = 0f;
                int fallenTreeCount = 0;
                foreach (Transform addedTree in addedTrees)
                {
                    MeshRenderer treeRenderer = addedTree.GetComponent<MeshRenderer>();
                    if (treeRenderer == null) { fallenTreeCount++; continue; }

                    Bounds treeBounds = treeRenderer.bounds;
                    float horizontalExtent = Mathf.Max(treeBounds.size.x, treeBounds.size.z);
                    float heightRatio = treeBounds.size.y / Mathf.Max(horizontalExtent, 0.001f);
                    float baseGapRatio = Mathf.Abs(treeBounds.min.y - addedTree.position.y) /
                                         Mathf.Max(treeBounds.size.y, 0.001f);
                    minimumTreeHeightRatio = Mathf.Min(minimumTreeHeightRatio, heightRatio);
                    maximumTreeBaseGapRatio = Mathf.Max(maximumTreeBaseGapRatio, baseGapRatio);

                    // Imported FBX tree meshes use a converted local axis, so
                    // Transform.up is not their visual trunk direction. Validate
                    // the rendered geometry instead: a fallen tree becomes much
                    // wider than tall and swings its crown below the ground pivot.
                    if (heightRatio < 0.32f || baseGapRatio > 0.28f)
                        fallenTreeCount++;
                }
                if (addedTrees.Length != 205 || fallenTreeCount != 0)
                    throw new InvalidOperationException(
                        $"Added tree geometry validation failed: count={addedTrees.Length}, fallen={fallenTreeCount}, " +
                        $"minHeightRatio={minimumTreeHeightRatio:F3}, maxBaseGapRatio={maximumTreeBaseGapRatio:F3}");

                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterials.Any(material => material == null))
                        throw new InvalidOperationException("A renderer has a missing material: " + renderer.name);
                    if (renderer.GetComponent<MeshFilter>()?.sharedMesh == null)
                        throw new InvalidOperationException("A renderer has a missing mesh: " + renderer.name);
                }

                int vegetationShadowCasters = vegetation.GetComponentsInChildren<MeshRenderer>(true)
                    .Count(renderer => renderer.shadowCastingMode != ShadowCastingMode.Off);
                if (vegetationShadowCasters != vegetation.GetComponentsInChildren<MeshRenderer>(true).Length)
                    throw new InvalidOperationException("One or more vegetation renderers do not cast shadows");

                R70SceneRenderProfile renderProfile = root.GetComponent<R70SceneRenderProfile>();
                if (renderProfile == null || renderProfile.pipelineAsset == null)
                    throw new InvalidOperationException("R70 long-range shadow profile is missing");
                SerializedObject pipeline = new SerializedObject(renderProfile.pipelineAsset);
                float shadowDistance = pipeline.FindProperty("m_ShadowDistance").floatValue;
                int shadowResolution = pipeline.FindProperty("m_MainLightShadowmapResolution").intValue;
                int cascadeCount = pipeline.FindProperty("m_ShadowCascadeCount").intValue;
                if (shadowDistance < 350f || shadowResolution < 4096 || cascadeCount != 4)
                    throw new InvalidOperationException($"R70 shadow profile is insufficient: distance={shadowDistance}, resolution={shadowResolution}, cascades={cascadeCount}");

                TextureImporter normalImporter = AssetImporter.GetAtPath(Source + "/Textures/R70_Volcano_Normal.png") as TextureImporter;
                if (normalImporter == null || normalImporter.maxTextureSize < 4096 || normalImporter.textureCompression != TextureImporterCompression.Uncompressed)
                    throw new InvalidOperationException("R70 volcano normal is not imported as uncompressed 4K");

                Material volcanoMaterial = volcano.GetComponentInChildren<MeshRenderer>(true)?.sharedMaterial;
                if (volcanoMaterial == null || volcanoMaterial.GetTexture("_AshMap") == null ||
                    volcanoMaterial.GetFloat("_NormalStrength") < 1.0f ||
                    volcanoMaterial.GetFloat("_MacroColorStrength") < 0.27f ||
                    volcanoMaterial.GetFloat("_AshStrength") < 0.4f)
                    throw new InvalidOperationException("R70 volcano layered material calibration is incomplete");

                Debug.Log($"R70_UNITY_MIGRATION_VALIDATION_PASS scene={scene.path} renderers={renderers} meshFilters={meshFilters} mixedFill={mixedFillCount} addedTrees={addedTrees.Length} fallenTrees={fallenTreeCount} minTreeHeightRatio={minimumTreeHeightRatio:F3} maxTreeBaseGapRatio={maximumTreeBaseGapRatio:F3} vegetationShadowCasters={vegetationShadowCasters} shadowDistance={shadowDistance} shadowResolution={shadowResolution} cascades={cascadeCount} normalMaxSize={normalImporter.maxTextureSize} ashLayer=enabled");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void RenderValidationBatch()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Camera camera = GameObject.Find("R70_ReviewCamera")?.GetComponent<Camera>();
                GameObject root = GameObject.Find("R70_VOLCANO_MIGRATION");
                if (camera == null || root == null) throw new InvalidOperationException("Review camera or migration root missing");

                const int width = 1600;
                const int height = 900;
                RenderTexture target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                target.antiAliasing = 4;
                RenderTexture previous = RenderTexture.active;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                Texture2D image = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                string assetPath = Source + "/Reports/R70_UnityValidation.png";
                File.WriteAllBytes(ToDiskPath(assetPath), image.EncodeToPNG());
                camera.targetTexture = null;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(image);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);

                Bounds bounds = default;
                bool initialized = false;
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!initialized) { bounds = renderer.bounds; initialized = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                Vector3 projected = camera.WorldToViewportPoint(bounds.center);
                Debug.Log($"R70_UNITY_RENDER_PASS output={assetPath} boundsCenter={bounds.center} boundsSize={bounds.size} camera={camera.transform.position} forward={camera.transform.forward} projectedCenter={projected} cullingMask={camera.cullingMask}");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            EnsureFolders();
            ConfigureImporters();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            UniversalRenderPipelineAsset pipeline = CreateR70PipelineAsset();
            QualitySettings.renderPipeline = pipeline;

            Material volcanoMaterial = CreateVolcanoMaterial();
            Dictionary<string, Material> vegetationMaterials = CreateVegetationMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("R70_VOLCANO_MIGRATION");
            root.isStatic = true;
            R70SceneRenderProfile renderProfile = root.AddComponent<R70SceneRenderProfile>();
            renderProfile.pipelineAsset = pipeline;
            renderProfile.ApplyProfile();

            BuildVolcano(root.transform, volcanoMaterial);
            BuildVegetation(root.transform, vegetationMaterials);
            BuildReviewEnvironment(root.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("R70 Unity migration scene built: " + ScenePath);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project", "R70VolcanoMigration");
            EnsureFolder(Root, "Generated");
            EnsureFolder(Generated, "Materials");
            EnsureFolder(Root, "Runtime");
            EnsureFolder("Assets/_Project", "Scenes");
        }

        private static void EnsureFolder(string parent, string child)
        {
            string path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        private static void ConfigureImporters()
        {
            ConfigureFbx(VolcanoFbx, true);
            ConfigureFbx(PrototypeFbx, true);
            ConfigureFbx(PlacementFbx, false);
            ConfigureReviewRig();

            ConfigureTexture(Source + "/Textures/R70_Volcano_Albedo.png", true, false, 2048, true, true);
            ConfigureTexture(Source + "/Textures/R70_Volcano_Normal.png", false, true, 4096, true);
            ConfigureTexture(Source + "/Textures/R70_Volcano_Roughness.png", false, false, 4096, true);
            ConfigureTexture(Source + "/Textures/R70_Volcano_MacroRelief.png", false, false, 4096, true);
            ConfigureTexture(Source + "/Textures/R70_Volcano_ErodedAsh.png", true, false, 2048, false);
            ConfigureTexture(Source + "/Textures/R70_ApprovedReference.png", true, false, 2048, false);
        }

        private static void ConfigureFbx(string path, bool importMaterials)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) return;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.materialImportMode = importMaterials ? ModelImporterMaterialImportMode.ImportStandard : ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static void ConfigureReviewRig()
        {
            AssetDatabase.ImportAsset(ReviewRigFbx, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(ReviewRigFbx) is not ModelImporter importer) return;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importCameras = true;
            importer.importLights = true;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();
        }

        private static void ConfigureTexture(string path, bool sRgb, bool normalMap, int maxSize, bool uncompressed, bool readable = false)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.sRGBTexture = sRgb;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.maxTextureSize = maxSize;
            importer.isReadable = readable;
            importer.textureCompression = uncompressed ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static UniversalRenderPipelineAsset CreateR70PipelineAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(R70PipelinePath) == null)
            {
                if (!AssetDatabase.CopyAsset(PcPipelinePath, R70PipelinePath))
                    throw new InvalidOperationException("Could not create the R70 long-range URP profile");
            }

            UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(R70PipelinePath);
            if (asset == null) throw new InvalidOperationException("R70 long-range URP profile failed to load");
            SerializedObject serialized = new SerializedObject(asset);
            serialized.FindProperty("m_ShadowDistance").floatValue = 360f;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
            serialized.FindProperty("m_ShadowCascadeCount").intValue = 4;
            serialized.FindProperty("m_Cascade4Split").vector3Value = new Vector3(0.08f, 0.22f, 0.5f);
            serialized.FindProperty("m_SoftShadowQuality").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static Material CreateVolcanoMaterial()
        {
            Shader shader = Shader.Find("This Will Float/R70 Volcano");
            if (shader == null) throw new InvalidOperationException("R70 Volcano shader did not compile");
            string path = Generated + "/Materials/M_R70_Volcano.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "M_R70_Volcano" };
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/R70_Volcano_Albedo.png"));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/R70_Volcano_Normal.png"));
            material.SetTexture("_RoughnessMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/R70_Volcano_Roughness.png"));
            material.SetTexture("_MacroMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/R70_Volcano_MacroRelief.png"));
            material.SetTexture("_AshMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/R70_Volcano_ErodedAsh.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_NormalStrength", 1.05f);
            material.SetFloat("_Roughness", 1.0f);
            material.SetFloat("_MacroColorStrength", 0.28f);
            material.SetFloat("_AshStrength", 0.42f);
            material.SetFloat("_AshScale", 2.35f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Dictionary<string, Material> CreateVegetationMaterials()
        {
            MaterialSet set = JsonUtility.FromJson<MaterialSet>(File.ReadAllText(ToDiskPath(MaterialJson)));
            Shader shader = Shader.Find("This Will Float/R70 Foliage");
            if (shader == null) throw new InvalidOperationException("R70 Foliage shader did not compile");
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (MaterialDefinition definition in set.materials)
            {
                string safe = SanitizeFileName(definition.name);
                string path = Generated + "/Materials/" + safe + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = definition.name };
                    AssetDatabase.CreateAsset(material, path);
                }
                else material.shader = shader;
                float[] c = definition.color;
                material.SetColor("_BaseColor", new Color(c[0], c[1], c[2], c.Length > 3 ? c[3] : 1f));
                material.SetFloat("_Roughness", definition.roughness);
                material.SetFloat("_NoiseScale", definition.noiseScale);
                material.SetFloat("_NoiseStrength", definition.noiseStrength);
                material.SetFloat("_VertexColorWeight", definition.useVertexColor ? definition.vertexColorWeight : 0f);
                material.SetFloat("_AmbientLift", 0.26f);
                string lowerName = definition.name.ToLowerInvariant();
                bool darkFoliage = lowerName.Contains("dark") || lowerName.Contains("core") || lowerName.Contains("charcoal") || lowerName.Contains("deepolive") || lowerName.Contains("interior");
                bool woodyMaterial = lowerName.Contains("bark") || lowerName.Contains("stem") || lowerName.Contains("trunk");
                material.SetColor("_DarkFloor", woodyMaterial
                    ? new Color(0.105f, 0.055f, 0.028f, 1f)
                    : new Color(0.070f, 0.180f, 0.075f, 1f));
                material.SetFloat("_DarkFloorStrength", 1f);
                material.enableInstancing = true;
                material.doubleSidedGI = definition.doubleSided;
                EditorUtility.SetDirty(material);
                result[definition.name] = material;
            }
            return result;
        }

        private static void BuildVolcano(Transform parent, Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(VolcanoFbx);
            if (source == null) throw new FileNotFoundException("Volcano FBX failed to import", VolcanoFbx);
            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) throw new InvalidOperationException("Could not instantiate volcano FBX");
            instance.name = "Volcano";
            instance.transform.SetParent(parent, false);
            instance.isStatic = true;
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void BuildVegetation(Transform parent, Dictionary<string, Material> materials)
        {
            PlacementSet placementSet = JsonUtility.FromJson<PlacementSet>(File.ReadAllText(ToDiskPath(PlacementJson)));
            GameObject prototypeFbx = AssetDatabase.LoadAssetAtPath<GameObject>(PrototypeFbx);
            GameObject placementFbx = AssetDatabase.LoadAssetAtPath<GameObject>(PlacementFbx);
            if (prototypeFbx == null || placementFbx == null) throw new InvalidOperationException("Vegetation FBX import failed");

            Dictionary<string, Mesh> prototypes = prototypeFbx
                .GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null)
                .GroupBy(filter => filter.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First().sharedMesh, StringComparer.Ordinal);

            Dictionary<string, Transform> transforms = placementFbx
                .GetComponentsInChildren<Transform>(true)
                .Where(transform => transform.name.StartsWith("R70P_", StringComparison.Ordinal))
                .GroupBy(transform => transform.name, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

            GameObject vegetationRoot = new GameObject("Vegetation");
            vegetationRoot.transform.SetParent(parent, false);
            vegetationRoot.isStatic = true;

            var activePlacements = new List<Placement>();

            foreach (Placement placement in placementSet.placements)
            {
                // DUF_01-04 are internal shadow fillers. In the full-volcano R70
                // export they are exposed as 122 isolated near-black shrubs along
                // the shoreline and open slope. Exclude those helper instances;
                // retain the actual bushes, groundcover and all tree families.
                if (placement.source == "R70SRC_007" || placement.source == "R70SRC_008" ||
                    placement.source == "R70SRC_009" || placement.source == "R70SRC_010")
                    continue;
                if (!prototypes.TryGetValue(placement.source, out Mesh mesh))
                    throw new InvalidOperationException("Missing prototype mesh: " + placement.source);
                if (!transforms.TryGetValue(placement.name, out Transform sourceTransform))
                    throw new InvalidOperationException("Missing placement transform: " + placement.name);

                CreateVegetationInstance(vegetationRoot.transform, placement.name, mesh, placement,
                    sourceTransform.localPosition, sourceTransform.localRotation, sourceTransform.localScale, materials);
                activePlacements.Add(placement);
            }

            BuildLowerGreenFill(vegetationRoot.transform, activePlacements, prototypes, transforms, materials);
        }

        private static void BuildLowerGreenFill(
            Transform vegetationRoot,
            List<Placement> activePlacements,
            Dictionary<string, Mesh> prototypes,
            Dictionary<string, Transform> transforms,
            Dictionary<string, Material> materials)
        {
            GameObject fillRoot = new GameObject("R70_LowerGreen_MixedFill");
            fillRoot.transform.SetParent(vegetationRoot, false);
            fillRoot.isStatic = true;

            MeshFilter volcanoFilter = GameObject.Find("Volcano")?
                .GetComponentsInChildren<MeshFilter>(true)
                .FirstOrDefault(item => item.sharedMesh != null);
            if (volcanoFilter == null)
                throw new InvalidOperationException("Volcano surface mesh unavailable for lower-green fill");
            MeshCollider terrainCollider = volcanoFilter.gameObject.GetComponent<MeshCollider>();
            bool temporaryCollider = terrainCollider == null;
            if (temporaryCollider)
            {
                terrainCollider = volcanoFilter.gameObject.AddComponent<MeshCollider>();
                terrainCollider.sharedMesh = volcanoFilter.sharedMesh;
            }
            Physics.SyncTransforms();

            Dictionary<string, List<Placement>> templatesBySource = activePlacements
                .GroupBy(item => item.source, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            List<Transform> lowerAnchors = activePlacements
                .Select(item => transforms[item.name])
                .Where(item => item.localPosition.y <= 14.5f &&
                               new Vector2(item.localPosition.x, item.localPosition.z).magnitude >= 32f)
                .ToList();
            if (lowerAnchors.Count < 20)
                throw new InvalidOperationException("Insufficient lower-green anchors for mixed fill");

            var plans = new[]
            {
                new FillPlan("Groundcover", 150, new[] { "R70SRC_011", "R70SRC_014", "R70SRC_017", "R70SRC_020", "R70SRC_023", "R70SRC_026", "R70SRC_029" }, 1.25f, 2.5f, 15.0f, 15.0f, 0.80f, 0.86f, 1.18f),
                new FillPlan("Bush",       100, new[] { "R70SRC_003", "R70SRC_004", "R70SRC_005", "R70SRC_006" },                                            1.8f, 3.5f, 13.0f, 14.5f, 0.88f, 0.80f, 1.10f),
                new FillPlan("Understory",  80, new[] { "R70SRC_159", "R70SRC_160", "R70SRC_161", "R70SRC_162", "R70SRC_163", "R70SRC_164", "R70SRC_165", "R70SRC_166" }, 2.4f, 4.0f, 14.0f, 13.5f, 0.92f, 0.76f, 1.05f),
                new FillPlan("SmallTree",   50, new[] { "R70SRC_118", "R70SRC_137", "R70SRC_149", "R70SRC_154" }, 3.2f, 4.5f, 14.0f, 12.0f, 0.92f, 0.76f, 0.98f),
                new FillPlan("MediumTree",  30, new[] { "R70SRC_106", "R70SRC_109", "R70SRC_112", "R70SRC_115", "R70SRC_123", "R70SRC_129" }, 4.4f, 6.0f, 14.5f, 10.5f, 0.86f, 0.62f, 0.82f)
            };

            var random = new System.Random(70193);
            int totalCreated = 0;

            foreach (FillPlan plan in plans)
            {
                // Spacing is enforced inside each visual layer. Cross-layer overlap
                // is intentional: low foliage must sit beneath bushes and trees so
                // the lower green zone reads as one planted community, not samples.
                var categoryPositions = new List<Vector3>();
                string[] validSources = plan.sources.Where(templatesBySource.ContainsKey).ToArray();
                if (validSources.Length == 0)
                    throw new InvalidOperationException("No source templates for lower fill category " + plan.label);

                int created = 0;
                int attempts = 0;
                while (created < plan.count && attempts++ < plan.count * 35)
                {
                    Transform anchor = lowerAnchors[random.Next(lowerAnchors.Count)];
                    float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                    float distance = Mathf.Lerp(plan.minOffset, plan.maxOffset, (float)random.NextDouble());
                    Vector3 candidate = anchor.localPosition + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);

                    Vector3 rayOrigin = new Vector3(candidate.x, 120f, candidate.z);
                    if (!terrainCollider.Raycast(new Ray(rayOrigin, Vector3.down), out RaycastHit hit, 220f))
                        continue;
                    candidate = hit.point + hit.normal * 0.035f;

                    if (candidate.y > plan.maxHeight) continue;
                    float radius = new Vector2(candidate.x, candidate.z).magnitude;
                    if (radius < 31f || radius > 78f) continue;
                    if (categoryPositions.Any(position => HorizontalDistance(position, candidate) < plan.minSpacing)) continue;

                    Transform nearestAnchor = lowerAnchors
                        .OrderBy(item => HorizontalDistance(item.localPosition, candidate))
                        .First();
                    if (HorizontalDistance(nearestAnchor.localPosition, candidate) > 10f) continue;

                    string source = validSources[random.Next(validSources.Length)];
                    Placement template = templatesBySource[source][random.Next(templatesBySource[source].Count)];
                    Transform templateTransform = transforms[template.name];
                    float uniformScale = Mathf.Lerp(plan.minScale, plan.maxScale, (float)random.NextDouble());
                    Vector3 scale = templateTransform.localScale * uniformScale;
                    scale.y *= plan.verticalScale;
                    Quaternion rotation = CreateSurfaceAwareRotation(
                        plan.label, hit.normal, (float)random.NextDouble() * 360f,
                        templateTransform.localRotation);

                    string instanceName = $"R70_FILL_{plan.label}_{created + 1:000}";
                    CreateVegetationInstance(fillRoot.transform, instanceName, prototypes[source], template,
                        candidate, rotation, scale, materials);
                    categoryPositions.Add(candidate);
                    created++;
                    totalCreated++;
                }

                if (created < plan.count)
                    throw new InvalidOperationException($"Only created {created}/{plan.count} lower fill instances for {plan.label}");
            }

            int targetedFlankCount = BuildTargetedRightFlankFill(
                fillRoot.transform, terrainCollider, templatesBySource, prototypes, transforms, materials);
            totalCreated += targetedFlankCount;

            int greenCoverageCount = BuildAlbedoGreenCoverageFill(
                fillRoot.transform, terrainCollider, lowerAnchors, templatesBySource,
                prototypes, transforms, materials);
            totalCreated += greenCoverageCount;

            if (temporaryCollider)
                UnityEngine.Object.DestroyImmediate(terrainCollider);

            Debug.Log($"R70_LOWER_GREEN_MIXED_FILL created={totalCreated} anchors={lowerAnchors.Count} general=410 targetedRightFlank={targetedFlankCount} albedoGreenCoverage={greenCoverageCount}");
        }

        private static int BuildAlbedoGreenCoverageFill(
            Transform parent,
            MeshCollider terrainCollider,
            List<Transform> lowerAnchors,
            Dictionary<string, List<Placement>> templatesBySource,
            Dictionary<string, Mesh> prototypes,
            Dictionary<string, Transform> transforms,
            Dictionary<string, Material> materials)
        {
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + "/Textures/R70_Volcano_Albedo.png");
            if (albedo == null || !albedo.isReadable)
                throw new InvalidOperationException("Readable R70 albedo is required for green-zone coverage fill");

            var plans = new[]
            {
                new FillPlan("GreenGroundcover", 150, new[] { "R70SRC_011", "R70SRC_014", "R70SRC_017", "R70SRC_020", "R70SRC_023", "R70SRC_026", "R70SRC_029" }, 1.0f, 0f, 0f, 15.0f, 0.80f, 0.88f, 1.18f),
                new FillPlan("GreenBush",        90, new[] { "R70SRC_003", "R70SRC_004", "R70SRC_005", "R70SRC_006" }, 1.55f, 0f, 0f, 14.0f, 0.88f, 0.82f, 1.10f),
                new FillPlan("GreenUnderstory",  70, new[] { "R70SRC_159", "R70SRC_160", "R70SRC_161", "R70SRC_162", "R70SRC_163", "R70SRC_164", "R70SRC_165", "R70SRC_166" }, 1.9f, 0f, 0f, 13.0f, 0.92f, 0.78f, 1.04f),
                new FillPlan("GreenSmallTree",   60, new[] { "R70SRC_118", "R70SRC_137", "R70SRC_149", "R70SRC_154" }, 2.8f, 0f, 0f, 11.0f, 0.92f, 0.76f, 0.98f),
                new FillPlan("GreenMediumTree",  30, new[] { "R70SRC_106", "R70SRC_109", "R70SRC_112", "R70SRC_115", "R70SRC_123", "R70SRC_129" }, 4.0f, 0f, 0f, 8.5f, 0.84f, 0.62f, 0.80f),
            };

            var random = new System.Random(70603);
            int total = 0;
            foreach (FillPlan plan in plans)
            {
                var positions = new List<Vector3>();
                string[] validSources = plan.sources.Where(templatesBySource.ContainsKey).ToArray();
                int created = 0;
                int attempts = 0;
                while (created < plan.count && attempts++ < plan.count * 120)
                {
                    float radius = Mathf.Sqrt(Mathf.Lerp(32f * 32f, 73f * 73f, (float)random.NextDouble()));
                    float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                    Vector3 rayOrigin = new Vector3(Mathf.Cos(angle) * radius, 120f, Mathf.Sin(angle) * radius);
                    if (!terrainCollider.Raycast(new Ray(rayOrigin, Vector3.down), out RaycastHit hit, 220f)) continue;
                    if (hit.point.y > plan.maxHeight || hit.normal.y < 0.43f) continue;

                    Color terrainColor = albedo.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                    bool isGreen = terrainColor.g > terrainColor.r * 1.05f &&
                                   terrainColor.g > terrainColor.b * 1.12f;
                    if (!isGreen) continue;

                    Vector3 candidate = hit.point + hit.normal * 0.035f;
                    if (positions.Any(position => HorizontalDistance(position, candidate) < plan.minSpacing)) continue;
                    float originalClearance = plan.label.Contains("Tree", StringComparison.Ordinal) ? 2.4f : 0.9f;
                    if (lowerAnchors.Any(anchor => HorizontalDistance(anchor.localPosition, candidate) < originalClearance)) continue;

                    string source = validSources[random.Next(validSources.Length)];
                    Placement template = templatesBySource[source][random.Next(templatesBySource[source].Count)];
                    Transform templateTransform = transforms[template.name];
                    float uniformScale = Mathf.Lerp(plan.minScale, plan.maxScale, (float)random.NextDouble());
                    Vector3 scale = templateTransform.localScale * uniformScale;
                    scale.y *= plan.verticalScale;
                    Quaternion rotation = CreateSurfaceAwareRotation(
                        plan.label, hit.normal, (float)random.NextDouble() * 360f,
                        templateTransform.localRotation);
                    CreateVegetationInstance(parent, $"R70_{plan.label}_{created + 1:000}", prototypes[source],
                        template, candidate, rotation, scale, materials);
                    positions.Add(candidate);
                    created++;
                    total++;
                }
                if (created < plan.count)
                    throw new InvalidOperationException($"Only created {created}/{plan.count} green coverage instances for {plan.label}");
            }
            return total;
        }

        private static int BuildTargetedRightFlankFill(
            Transform parent,
            MeshCollider terrainCollider,
            Dictionary<string, List<Placement>> templatesBySource,
            Dictionary<string, Mesh> prototypes,
            Dictionary<string, Transform> transforms,
            Dictionary<string, Material> materials)
        {
            // Camera-right lower flank was intentionally left open during the ring
            // cleanup, but the remaining opening is too broad. These three unequal,
            // overlapping pockets fill its interior without completing a circular
            // belt around the island.
            Vector2[] centers =
            {
                new Vector2(-58f, 15f),
                new Vector2(-68f, 2f),
                new Vector2(-50f, 28f),
            };
            var plans = new[]
            {
                new FillPlan("FlankGroundcover", 55, new[] { "R70SRC_011", "R70SRC_014", "R70SRC_017", "R70SRC_020", "R70SRC_023", "R70SRC_026", "R70SRC_029" }, 1.1f, 0f, 0f, 14.0f, 0.80f, 0.86f, 1.16f),
                new FillPlan("FlankBush",       35, new[] { "R70SRC_003", "R70SRC_004", "R70SRC_005", "R70SRC_006" }, 1.7f, 0f, 0f, 13.0f, 0.88f, 0.80f, 1.08f),
                new FillPlan("FlankUnderstory", 25, new[] { "R70SRC_159", "R70SRC_160", "R70SRC_161", "R70SRC_162", "R70SRC_163", "R70SRC_164", "R70SRC_165", "R70SRC_166" }, 2.0f, 0f, 0f, 12.0f, 0.92f, 0.76f, 1.02f),
                new FillPlan("FlankSmallTree",  25, new[] { "R70SRC_118", "R70SRC_137", "R70SRC_149", "R70SRC_154" }, 2.7f, 0f, 0f, 10.5f, 0.92f, 0.76f, 0.98f),
                new FillPlan("FlankMediumTree", 10, new[] { "R70SRC_106", "R70SRC_109", "R70SRC_112", "R70SRC_115", "R70SRC_123", "R70SRC_129" }, 3.8f, 0f, 0f, 8.5f, 0.84f, 0.62f, 0.80f),
            };

            var random = new System.Random(70421);
            int total = 0;
            foreach (FillPlan plan in plans)
            {
                var positions = new List<Vector3>();
                string[] validSources = plan.sources.Where(templatesBySource.ContainsKey).ToArray();
                int created = 0;
                int attempts = 0;
                while (created < plan.count && attempts++ < plan.count * 60)
                {
                    int centerIndex = random.Next(centers.Length);
                    Vector2 center = centers[centerIndex];
                    float angle = (float)(random.NextDouble() * Mathf.PI * 2f);
                    float radial = Mathf.Sqrt((float)random.NextDouble());
                    float width = centerIndex == 0 ? 13f : centerIndex == 1 ? 10f : 9f;
                    float depth = centerIndex == 0 ? 10f : centerIndex == 1 ? 8f : 11f;
                    float x = center.x + Mathf.Cos(angle) * width * radial;
                    float z = center.y + Mathf.Sin(angle) * depth * radial;
                    Vector3 rayOrigin = new Vector3(x, 120f, z);
                    if (!terrainCollider.Raycast(new Ray(rayOrigin, Vector3.down), out RaycastHit hit, 220f)) continue;
                    if (hit.point.y > plan.maxHeight || hit.normal.y < 0.43f) continue;
                    float radius = new Vector2(hit.point.x, hit.point.z).magnitude;
                    if (radius < 35f || radius > 74f) continue;
                    Vector3 candidate = hit.point + hit.normal * 0.035f;
                    if (positions.Any(position => HorizontalDistance(position, candidate) < plan.minSpacing)) continue;

                    string source = validSources[random.Next(validSources.Length)];
                    Placement template = templatesBySource[source][random.Next(templatesBySource[source].Count)];
                    Transform templateTransform = transforms[template.name];
                    float uniformScale = Mathf.Lerp(plan.minScale, plan.maxScale, (float)random.NextDouble());
                    Vector3 scale = templateTransform.localScale * uniformScale;
                    scale.y *= plan.verticalScale;
                    Quaternion rotation = CreateSurfaceAwareRotation(
                        plan.label, hit.normal, (float)random.NextDouble() * 360f,
                        templateTransform.localRotation);
                    CreateVegetationInstance(parent, $"R70_{plan.label}_{created + 1:000}", prototypes[source],
                        template, candidate, rotation, scale, materials);
                    positions.Add(candidate);
                    created++;
                    total++;
                }
                if (created < plan.count)
                    throw new InvalidOperationException($"Only created {created}/{plan.count} targeted right-flank instances for {plan.label}");
            }
            return total;
        }

        private static Quaternion CreateSurfaceAwareRotation(
            string label, Vector3 surfaceNormal, float yaw, Quaternion templateRotation)
        {
            // Preserve the imported FBX orientation that makes the source mesh
            // visually upright. Discarding it can leave a transform whose Up is
            // vertical while the trunk geometry itself lies sideways.
            bool uprightAsset = label.Contains("Tree", StringComparison.Ordinal) ||
                                label.Contains("Understory", StringComparison.Ordinal) ||
                                label.Contains("Bush", StringComparison.Ordinal);
            if (uprightAsset)
                return Quaternion.AngleAxis(yaw, Vector3.up) * templateRotation;

            // Only broad ground-hugging foliage conforms strongly to the slope.
            float normalAlignment = 0.82f;
            Quaternion slopeRotation = Quaternion.Slerp(
                Quaternion.identity,
                Quaternion.FromToRotation(Vector3.up, surfaceNormal),
                normalAlignment);
            return slopeRotation * Quaternion.Euler(0f, yaw, 0f);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b) =>
            Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        private static void CreateVegetationInstance(
            Transform parent,
            string name,
            Mesh mesh,
            Placement placement,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Dictionary<string, Material> materials)
        {
            GameObject instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = localRotation;
            instance.transform.localScale = localScale;
            instance.isStatic = true;

            MeshFilter filter = instance.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = placement.materials.Select(materialName =>
            {
                string resolvedName = ResolveVisibleFoliageMaterial(materialName, materials);
                if (!materials.TryGetValue(resolvedName, out Material material))
                    throw new InvalidOperationException("Missing generated material: " + resolvedName);
                return material;
            }).ToArray();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
        }

        private sealed class FillPlan
        {
            public readonly string label;
            public readonly int count;
            public readonly string[] sources;
            public readonly float minSpacing;
            public readonly float minOffset;
            public readonly float maxOffset;
            public readonly float maxHeight;
            public readonly float verticalScale;
            public readonly float minScale;
            public readonly float maxScale;

            public FillPlan(string label, int count, string[] sources, float minSpacing, float minOffset,
                float maxOffset, float maxHeight, float verticalScale, float minScale, float maxScale)
            {
                this.label = label;
                this.count = count;
                this.sources = sources;
                this.minSpacing = minSpacing;
                this.minOffset = minOffset;
                this.maxOffset = maxOffset;
                this.maxHeight = maxHeight;
                this.verticalScale = verticalScale;
                this.minScale = minScale;
                this.maxScale = maxScale;
            }
        }

        private static string ResolveVisibleFoliageMaterial(string name, Dictionary<string, Material> materials)
        {
            // R70's darkest Blender vertex-color ramps collapse into black dots
            // after card overlap in Unity. Preserve the mesh and physical shadow,
            // but use the approved neighbouring green value for visible foliage.
            if (name == "M_DSM_Core_Dark") return "M_DSM_Surface_Forest";
            if (name == "M_SFP_R50_Interior_Olive") return "M_SFP_R50_Surface_MossOlive";
            if (name == "M_DUF_R50_CharcoalGreen" || name == "M_DUF_R50_DeepOlive")
                return "M_DUF_R50_SubduedMoss";
            if (name == "M_TV_Understory_Leaf_Dark") return "M_TV_Understory_Leaf_Mid";
            if (name.Contains("_Leaf_Dark", StringComparison.Ordinal))
            {
                string midVariant = name.Replace("_Leaf_Dark", "_Leaf_Mid");
                if (materials.ContainsKey(midVariant)) return midVariant;
            }
            return name;
        }

        private static void BuildReviewEnvironment(Transform parent)
        {
            GameObject rigSource = AssetDatabase.LoadAssetAtPath<GameObject>(ReviewRigFbx);
            GameObject rig = rigSource != null ? PrefabUtility.InstantiatePrefab(rigSource) as GameObject : null;
            if (rig == null) throw new InvalidOperationException("Could not instantiate the exported Blender review rig");
            rig.name = "R70_BlenderReviewRig";
            rig.transform.SetParent(parent, false);

            Camera importedCamera = rig.GetComponentsInChildren<Camera>(true).FirstOrDefault();
            Light[] importedLights = rig.GetComponentsInChildren<Light>(true);
            Light importedSun = importedLights.FirstOrDefault(item => item.type == LightType.Directional);
            Light importedFill = importedLights.FirstOrDefault(item => item != importedSun);
            if (importedCamera == null || importedSun == null)
                throw new InvalidOperationException("Exported Blender camera or sun is missing from R70_ReviewRig.fbx");

            foreach (Camera extra in rig.GetComponentsInChildren<Camera>(true)) extra.enabled = false;
            foreach (Light extra in importedLights) extra.enabled = false;

            GameObject lightObject = new GameObject("R70_Neutral_Daylight");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = importedSun.transform.rotation;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.941176f, 0.847059f);
            light.intensity = 1.6f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;
            light.shadowBias = 0.018f;
            light.shadowNormalBias = 0.14f;

            if (importedFill != null)
            {
                GameObject fillObject = new GameObject("R70_Sky_Fill_Approximation");
                fillObject.transform.SetParent(parent, false);
                fillObject.transform.rotation = importedFill.transform.rotation;
                Light fill = fillObject.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.color = new Color(0.72f, 0.81f, 0.81f);
                fill.intensity = 0.42f;
                fill.shadows = LightShadows.None;
            }

            GameObject cameraObject = new GameObject("R70_ReviewCamera");
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = importedCamera.transform.position;
            cameraObject.transform.rotation = importedCamera.transform.rotation;
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.usePhysicalProperties = false;
            // Blender's 55 mm camera stores horizontal sensor fit; Unity uses
            // vertical FOV. At the approved 16:9 framing this is 20.87 degrees.
            camera.fieldOfView = 20.87f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            camera.allowMSAA = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.64f, 0.66f, 0.65f, 1f);
            camera.tag = "MainCamera";

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "R70_Water";
            water.transform.SetParent(parent, false);
            water.transform.position = new Vector3(0f, 0.16f, 0f);
            water.transform.localScale = new Vector3(56f, 1f, 56f);
            UnityEngine.Object.DestroyImmediate(water.GetComponent<Collider>());
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit != null)
            {
                Material waterMaterial = new Material(urpLit) { name = "M_R70_Water_Runtime" };
                waterMaterial.SetColor("_BaseColor", new Color(0.27f, 0.38f, 0.41f, 1f));
                waterMaterial.SetFloat("_Smoothness", 0.36f);
                water.GetComponent<MeshRenderer>().sharedMaterial = waterMaterial;
                water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.36f, 0.39f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.25f, 0.27f, 0.25f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.16f, 0.14f);
            RenderSettings.ambientIntensity = 0.88f;
            RenderSettings.fog = false;

            Debug.Log($"R70_IMPORTED_RIG camera={cameraObject.transform.position} forward={cameraObject.transform.forward} fov={camera.fieldOfView} sunForward={lightObject.transform.forward}");
        }

        private static void FitReviewCamera(GameObject root)
        {
            Camera camera = GameObject.Find("R70_ReviewCamera")?.GetComponent<Camera>();
            if (camera == null) return;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.name != "R70_Water")
                .ToArray();
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            foreach (Renderer renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            Vector3 target = new Vector3(bounds.center.x, bounds.min.y + bounds.size.y * 0.33f, bounds.center.z);
            Vector3 direction = (camera.transform.position - target).normalized;
            camera.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);

            for (int iteration = 0; iteration < 4; iteration++)
            {
                Vector3[] corners = BoundsCorners(bounds);
                float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
                float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
                foreach (Vector3 corner in corners)
                {
                    Vector3 projected = camera.WorldToViewportPoint(corner);
                    minX = Mathf.Min(minX, projected.x);
                    minY = Mathf.Min(minY, projected.y);
                    maxX = Mathf.Max(maxX, projected.x);
                    maxY = Mathf.Max(maxY, projected.y);
                }
                float occupancy = Mathf.Max((maxX - minX) / 0.91f, (maxY - minY) / 0.88f);
                float distance = Vector3.Distance(camera.transform.position, target);
                camera.transform.position = target + direction * Mathf.Clamp(distance * occupancy, 85f, 310f);
            }
            camera.transform.rotation = Quaternion.LookRotation(target - camera.transform.position, Vector3.up);
        }

        private static Vector3[] BoundsCorners(Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            return new[]
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, max.y, max.z), new Vector3(max.x, max.y, max.z),
            };
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Replace('/', '_').Replace('\\', '_');
        }

        private static string ToDiskPath(string assetPath)
        {
            string project = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project path unavailable");
            return Path.Combine(project, assetPath);
        }
    }
}
#endif
