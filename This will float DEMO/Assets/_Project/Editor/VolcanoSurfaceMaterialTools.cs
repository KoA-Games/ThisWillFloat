using System;
using System.IO;
using System.Linq;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class VolcanoSurfaceMaterialTools
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string EnvironmentScenePath = "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string SourceLockedPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_LOCKED_BEFORE_REFERENCE_BOARD_SURFACE.unity";
        private const string EnvironmentLockedPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_LOCKED_BEFORE_REFERENCE_BOARD_SURFACE.unity";
        private const string SurfaceObjectName = "Stylized Volcano - Earth Rock And Charcoal Bands";
        private const string CraterObjectName = "Visible Caldera Walls And Floor - Crater Only Refinement";
        private const string RockRootName = "LOCKED CRATER - STYLIZED VOLCANO ROCK DRESSING";
        private const string RemovedRimClusterName = "Crater 03 - East Rim Fragment";
        private const string ShaderName = "This Will Float/Final Volcano Stylized Macro Surface";
        private const string MaterialPath =
            "Assets/_Project/GeneratedMaterials/Final Volcano Stylized Macro Surface.mat";
        private const string RockShaderName = "This Will Float/Warm Volcanic Rocks Stylized";
        private const string RockMaterialPath =
            "Assets/_Project/GeneratedMaterials/Final Rocks Stylized Warm Volcanic Matte.mat";
        private const string CaptureFolderName = "VolcanoReferenceBoardCaptures";
        private static readonly Vector2 CraterCenter = new Vector2(28f, 80f);

        [MenuItem("This Will Float DEMO/5. BUILD STYLIZED VOLCANO SURFACE MATERIAL")]
        public static void BuildFromMenu() => Build(true);

        [MenuItem("This Will Float DEMO/5. OPEN STYLIZED VOLCANO SURFACE MATERIAL")]
        public static void OpenFromMenu() => EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);

        public static void BuildBatch() => Build(false);

        public static void CaptureBatch()
        {
            string folder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                CaptureFolderName);
            Directory.CreateDirectory(folder);

            Require(File.Exists(Path.Combine(folder, "01_before_reference_player.png")),
                "Run the reference-board build first so the true pre-change frame can be preserved.");

            Scene afterScene = EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas afterCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(afterScene.IsValid() && afterCanvas != null, "The material test scene is missing.");
            CapturePlayer(afterCanvas, Path.Combine(folder, "02_after_reference_same_player.png"));
            CaptureTop45(afterCanvas, Path.Combine(folder, "03_after_reference_top45.png"));
            CaptureSide(afterCanvas, Path.Combine(folder, "04_after_reference_side.png"));
            CaptureBase(afterCanvas, Path.Combine(folder, "05_after_reference_base_rocks.png"));
            Debug.Log("VOLCANO_REFERENCE_BOARD_CAPTURES_COMPLETE: " + folder);
        }

        private static void Build(bool showDialog)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) != null, "The approved volcano source scene is missing.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScenePath) != null, "The approved environment scene is missing.");

            Scene environment = EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            string environmentSignature = CaptureEnvironmentSignature();
            EditableIslandMeshCanvas environmentCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(environmentCanvas != null, "The approved environment terrain is missing.");
            string captureFolder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                CaptureFolderName);
            Directory.CreateDirectory(captureFolder);
            string beforePlayerPath = Path.Combine(captureFolder, "01_before_reference_player.png");
            string beforeTopPath = Path.Combine(captureFolder, "00_before_reference_top45.png");
            if (!File.Exists(beforePlayerPath)) CapturePlayer(environmentCanvas, beforePlayerPath);
            if (!File.Exists(beforeTopPath)) CaptureTop45(environmentCanvas, beforeTopPath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentLockedPath) == null)
            {
                Require(EditorSceneManager.SaveScene(environment, EnvironmentLockedPath, true),
                    "Could not lock the pre-material environment.");
            }

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceLockedPath) == null)
            {
                Require(EditorSceneManager.SaveScene(source, SourceLockedPath, true),
                    "Could not lock the pre-material volcano source.");
            }

            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            GameObject surface = GameObject.Find(SurfaceObjectName);
            GameObject crater = GameObject.Find(CraterObjectName);
            GameObject rocks = GameObject.Find(RockRootName);
            Require(canvas != null && surface != null && crater != null && rocks != null,
                "The approved volcano, crater, surface or rock baseline is incomplete.");
            MeshFilter surfaceFilter = surface.GetComponent<MeshFilter>();
            MeshRenderer surfaceRenderer = surface.GetComponent<MeshRenderer>();
            MeshFilter craterFilter = crater.GetComponent<MeshFilter>();
            Require(surfaceFilter != null && surfaceFilter.sharedMesh != null && surfaceRenderer != null,
                "The volcano surface renderer is incomplete.");
            Require(craterFilter != null && craterFilter.sharedMesh != null, "The crater geometry is incomplete.");

            canvas.CopyState(out float[] beforeHeights, out int[] beforePaint);
            Vector3[] beforeSurfaceVertices = surfaceFilter.sharedMesh.vertices.ToArray();
            Vector3[] beforeCraterVertices = craterFilter.sharedMesh.vertices.ToArray();
            string preservedRockTransforms = CaptureTransformSignature(rocks, RemovedRimClusterName);
            int rockCountBefore = rocks.GetComponentsInChildren<MeshRenderer>(true).Length;
            int removedRimRocks = RemoveSelectedRimCluster(rocks);

            Material material = GetOrCreateSurfaceMaterial();
            surfaceRenderer.sharedMaterial = material;
            EditorUtility.SetDirty(surfaceRenderer);
            Material rockMaterial = GetOrCreateRockMaterial();
            foreach (Renderer renderer in rocks.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = rockMaterial;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
            }

            canvas.CopyState(out float[] afterHeights, out int[] afterPaint);
            Require(beforeHeights.SequenceEqual(afterHeights), "Terrain heights changed during the material-only edit.");
            Require(beforePaint.SequenceEqual(afterPaint), "Terrain paint changed during the material-only edit.");
            Require(beforeSurfaceVertices.SequenceEqual(surfaceFilter.sharedMesh.vertices), "The volcano surface mesh changed.");
            Require(beforeCraterVertices.SequenceEqual(craterFilter.sharedMesh.vertices), "The crater geometry changed.");
            Require(preservedRockTransforms == CaptureTransformSignature(rocks, RemovedRimClusterName),
                "A preserved rock transform changed while opening the crater rim.");
            Require(rocks.transform.Cast<Transform>().Count(t => t.name.StartsWith("Crater ", StringComparison.Ordinal)) == 2,
                "The reference-board crater rim must retain exactly two separated rock clusters.");

            EditorSceneManager.MarkSceneDirty(source);
            Require(EditorSceneManager.SaveScene(source, SourceScenePath), "Could not save the material-only source scene.");
            AssetDatabase.SaveAssets();
            EnvironmentMoodTestTools.BuildTestSceneBatch();

            EditableIslandMeshCanvas finalCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            MeshFilter finalSurface = GameObject.Find(SurfaceObjectName).GetComponent<MeshFilter>();
            MeshFilter finalCrater = GameObject.Find(CraterObjectName).GetComponent<MeshFilter>();
            GameObject finalRocks = GameObject.Find(RockRootName);
            finalCanvas.CopyState(out float[] finalHeights, out int[] finalPaint);
            Require(beforeHeights.SequenceEqual(finalHeights) && beforePaint.SequenceEqual(finalPaint),
                "Environment rebuild changed terrain data.");
            Require(beforeSurfaceVertices.SequenceEqual(finalSurface.sharedMesh.vertices),
                "Environment rebuild changed volcano vertices.");
            Require(beforeCraterVertices.SequenceEqual(finalCrater.sharedMesh.vertices),
                "Environment rebuild changed crater vertices.");
            Require(preservedRockTransforms == CaptureTransformSignature(finalRocks, RemovedRimClusterName),
                "Environment rebuild changed preserved rock transforms.");
            Require(environmentSignature == CaptureEnvironmentSignature(),
                "Sky, water, lighting or ambient settings changed during the reference-board pass.");

            CapturePlayer(finalCanvas, Path.Combine(captureFolder, "02_after_reference_same_player.png"));
            CaptureTop45(finalCanvas, Path.Combine(captureFolder, "03_after_reference_top45.png"));
            CaptureSide(finalCanvas, Path.Combine(captureFolder, "04_after_reference_side.png"));
            CaptureBase(finalCanvas, Path.Combine(captureFolder, "05_after_reference_base_rocks.png"));

            Debug.Log(
                "VOLCANO_REFERENCE_BOARD_COMPLETE" +
                "|geometryChange=none|terrainHeightChange=none|terrainPaintChange=none" +
                "|craterGeometryChange=none|rockTransformChange=removedEastRimClusterOnly" +
                "|material=Final Volcano Stylized Macro Surface" +
                "|soil=#806044|midAsh=#6D6253|upperRock=#525553|summit=#393C3D|flow=#413D37" +
                "|metallic=0.00|smoothness=0.025|normalMap=none|textureNoise=none" +
                "|mapping=worldSpace|macro=threeLowFrequencyFields|flows=5UnequalInterruptedColorFields" +
                $"|rocksBefore={rockCountBefore}|rocksRemoved={removedRimRocks}" +
                $"|rocksRecolored={finalRocks.GetComponentsInChildren<MeshRenderer>(true).Length}" +
                "|rimClusters=2|upperOutcrops=3|baseClusters=6|middleSlope=open" +
                "|rockMaterial=Final Rocks Stylized Warm Volcanic Matte" +
                "|rockBase=#514942|rockShadow=#35322F|rockHighlight=#746A60|rockMetallic=0.00|rockSmoothness=0.02" +
                "|lighting=URPMainLightSoftShadow+4BandStylizedDiffuse|FastSky=preserved" +
                "|waterChange=none|islandChange=none|treeChange=none|gameplayChange=none");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The approved volcano geometry is locked. The reference-board four-zone macro surface and the warmer embedded-rock palette were applied without moving any rock or changing the environment.",
                    "OK");
            }
        }

        private static Material GetOrCreateSurfaceMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            Require(shader != null, "The stylized macro volcano shader is missing or failed to compile.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Final Volcano Stylized Macro Surface" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_SoilColor", Hex("#806044"));
            material.SetColor("_MidColor", Hex("#6D6253"));
            material.SetColor("_UpperRockColor", Hex("#525553"));
            material.SetColor("_SummitColor", Hex("#393C3D"));
            material.SetColor("_FlowColor", Hex("#413D37"));
            material.SetColor("_ShadowTint", Hex("#ADA9A3"));
            material.SetFloat("_MacroStrength", 0.08f);
            material.SetFloat("_FlowStrength", 0.24f);
            material.SetFloat("_Smoothness", 0.025f);
            material.SetFloat("_Metallic", 0f);
            material.SetVector("_VolcanoCenter", new Vector4(35f, 75f, 350f, 0f));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetOrCreateRockMaterial()
        {
            Shader shader = Shader.Find(RockShaderName);
            Require(shader != null, "The stylized warm volcanic-rock shader is missing or failed to compile.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Final Rocks Stylized Warm Volcanic Matte" };
                AssetDatabase.CreateAsset(material, RockMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", Hex("#514942"));
            material.SetColor("_ShadowColor", Hex("#35322F"));
            material.SetColor("_HighlightColor", Hex("#746A60"));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.02f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Color Hex(string value)
        {
            Require(ColorUtility.TryParseHtmlString(value, out Color color), "Invalid color: " + value);
            return color;
        }

        private static int RemoveSelectedRimCluster(GameObject root)
        {
            Transform cluster = root.transform.Cast<Transform>()
                .FirstOrDefault(t => t.name == RemovedRimClusterName);
            if (cluster == null) return 0;
            int removed = cluster.GetComponentsInChildren<MeshRenderer>(true).Length;
            Object.DestroyImmediate(cluster.gameObject);
            return removed;
        }

        private static string CaptureTransformSignature(GameObject root, string excludedRootName = null)
        {
            return string.Join("|", root.GetComponentsInChildren<Transform>(true)
                .Where(t => string.IsNullOrEmpty(excludedRootName) && t != null ||
                    !IsUnderNamedRoot(t, excludedRootName))
                .OrderBy(t => GetPath(t))
                .Select(t => string.Format(
                    "{0}:{1:F5},{2:F5},{3:F5}:{4:F5},{5:F5},{6:F5},{7:F5}:{8:F5},{9:F5},{10:F5}:{11}",
                    GetPath(t),
                    t.localPosition.x, t.localPosition.y, t.localPosition.z,
                    t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w,
                    t.localScale.x, t.localScale.y, t.localScale.z,
                    t.gameObject.activeSelf)));
        }

        private static bool IsUnderNamedRoot(Transform transform, string rootName)
        {
            if (string.IsNullOrEmpty(rootName)) return false;
            while (transform != null)
            {
                if (transform.name == rootName) return true;
                transform = transform.parent;
            }
            return false;
        }

        private static string GetPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }

        private static void CapturePlayer(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(35f, 0f, -545f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.2f, canvas.WaterLevel + 3.2f);
            Capture(path, position, new Vector3(28f, canvas.WaterLevel + 116f, 80f), 58f);
        }

        private static void CaptureTop45(EditableIslandMeshCanvas canvas, string path)
        {
            Capture(
                path,
                new Vector3(CraterCenter.x - 250f, canvas.WaterLevel + 450f, CraterCenter.y - 300f),
                new Vector3(35f, canvas.WaterLevel + 112f, 75f),
                50f);
        }

        private static void CaptureSide(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(610f, 0f, 95f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 8f, canvas.WaterLevel + 8f);
            Capture(path, position, new Vector3(32f, canvas.WaterLevel + 112f, 78f), 58f);
        }

        private static void CaptureBase(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(92f, canvas.WaterLevel + 72f, -322f);
            Vector3 target = new Vector3(10f, canvas.WaterLevel + 21f, -225f);
            Capture(path, position, target, 50f);
        }

        private static string CaptureEnvironmentSignature()
        {
            string sky = AssetDatabase.GetAssetPath(RenderSettings.skybox);
            Light sun = GameObject.Find("Sun_Tropical_Test")?.GetComponent<Light>();
            string sunSignature = sun == null
                ? "none"
                : string.Format(
                    "{0:F4},{1:F4},{2:F4},{3:F4}:{4:F4}:{5:F4}:{6:F4}",
                    sun.color.r, sun.color.g, sun.color.b, sun.intensity,
                    sun.shadowStrength,
                    sun.transform.eulerAngles.x,
                    sun.transform.eulerAngles.y);
            GameObject ocean = GameObject.Find("Ocean - waves, visual only");
            string oceanMaterials = ocean == null
                ? "none"
                : string.Join(",", ocean.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null)
                    .Select(AssetDatabase.GetAssetPath)
                    .OrderBy(p => p));
            return string.Format(
                "sky={0}|ambientMode={1}|ambient={2:F4},{3:F4},{4:F4},{5:F4}|fog={6}|sun={7}|ocean={8}",
                sky,
                RenderSettings.ambientMode,
                RenderSettings.ambientLight.r,
                RenderSettings.ambientLight.g,
                RenderSettings.ambientLight.b,
                RenderSettings.ambientIntensity,
                RenderSettings.fog,
                sunSignature,
                oceanMaterials);
        }

        private static void Capture(string path, Vector3 position, Vector3 target, float fov)
        {
            GameObject cameraObject = new GameObject("Temporary Volcano Surface Material Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 3500f;
            camera.transform.position = position;
            camera.transform.LookAt(target);
            RenderTexture texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            Texture2D image = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            image.ReadPixels(new Rect(0f, 0f, 1600, 900), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
