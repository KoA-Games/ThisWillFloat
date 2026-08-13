#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
                if (renderers < 2_099 || meshFilters < 2_099)
                    throw new InvalidOperationException($"Unexpected mesh count: renderers={renderers}, filters={meshFilters}");

                foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (renderer.sharedMaterials.Any(material => material == null))
                        throw new InvalidOperationException("A renderer has a missing material: " + renderer.name);
                    if (renderer.GetComponent<MeshFilter>()?.sharedMesh == null)
                        throw new InvalidOperationException("A renderer has a missing mesh: " + renderer.name);
                }

                Debug.Log($"R70_UNITY_MIGRATION_VALIDATION_PASS scene={scene.path} renderers={renderers} meshFilters={meshFilters}");
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

            Material volcanoMaterial = CreateVolcanoMaterial();
            Dictionary<string, Material> vegetationMaterials = CreateVegetationMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("R70_VOLCANO_MIGRATION");
            root.isStatic = true;

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

            ConfigureTexture(Source + "/Textures/R70_Volcano_Albedo.png", true, false, 2048);
            ConfigureTexture(Source + "/Textures/R70_Volcano_Normal.png", false, true, 2048);
            ConfigureTexture(Source + "/Textures/R70_Volcano_Roughness.png", false, false, 2048);
            ConfigureTexture(Source + "/Textures/R70_Volcano_ErodedAsh.png", true, false, 2048);
            ConfigureTexture(Source + "/Textures/R70_ApprovedReference.png", true, false, 2048);
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

        private static void ConfigureTexture(string path, bool sRgb, bool normalMap, int maxSize)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            importer.sRGBTexture = sRgb;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
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
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_NormalStrength", 0.24f);
            material.SetFloat("_Roughness", 1.0f);
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

            foreach (Placement placement in placementSet.placements)
            {
                if (!prototypes.TryGetValue(placement.source, out Mesh mesh))
                    throw new InvalidOperationException("Missing prototype mesh: " + placement.source);
                if (!transforms.TryGetValue(placement.name, out Transform sourceTransform))
                    throw new InvalidOperationException("Missing placement transform: " + placement.name);

                GameObject instance = new GameObject(placement.name);
                instance.transform.SetParent(vegetationRoot.transform, false);
                instance.transform.localPosition = sourceTransform.localPosition;
                instance.transform.localRotation = sourceTransform.localRotation;
                instance.transform.localScale = sourceTransform.localScale;
                instance.isStatic = true;

                MeshFilter filter = instance.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = placement.materials.Select(name =>
                {
                    if (!materials.TryGetValue(name, out Material material))
                        throw new InvalidOperationException("Missing generated material: " + name);
                    return material;
                }).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            }
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
