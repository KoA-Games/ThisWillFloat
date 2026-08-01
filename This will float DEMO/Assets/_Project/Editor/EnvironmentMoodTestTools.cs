using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class EnvironmentMoodTestTools
    {
        private const string SourceScenePath =
            "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string BackupScenePath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_LOCKED_BEFORE_ENVIRONMENT_MOOD_TEST.unity";
        public const string TestScenePath =
            "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string PreviousProceduralSkyMaterialPath =
            "Assets/_Project/GeneratedMaterials/Environment Mood Test Procedural Tropical Sky.mat";
        private const string FastSkyTemplateMaterialPath =
            "Assets/FastSky/Materials/StylisedSky.mat";
        private const string SkyMaterialPath =
            "Assets/_Project/GeneratedMaterials/Environment Mood Test FastSky Tropical Sparse Clouds.mat";

        [MenuItem("This Will Float DEMO/0. OPEN ENVIRONMENT MOOD TEST - SKY SUN")]
        public static void OpenTestSceneFromMenu()
        {
            OpenTestScene(true);
        }

        [MenuItem("This Will Float DEMO/1. BUILD ENVIRONMENT MOOD TEST - SKY SUN")]
        public static void BuildTestSceneFromMenu()
        {
            BuildTestScene(true);
        }

        public static void BuildTestSceneBatch()
        {
            BuildTestScene(false);
        }

        public static void CaptureSubmissionBatch()
        {
            string outputFolder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "EnvironmentMoodTest_SkySun_Captures");
            Directory.CreateDirectory(outputFolder);

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas sourceCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(source.IsValid() && sourceCanvas != null, "The locked volcano source scene is unavailable.");
            CaptureMainPlayerView(sourceCanvas, Path.Combine(outputFolder, "01_before_player_game_view.png"));

            Scene test = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas testCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(test.IsValid() && testCanvas != null, "The sky/sun test scene is unavailable.");
            CaptureMainPlayerView(testCanvas, Path.Combine(outputFolder, "02_after_same_player_game_view.png"));
            CaptureBeachView(testCanvas, Path.Combine(outputFolder, "03_after_beach_to_volcano_game_view.png"));
            CaptureSideLightingView(testCanvas, Path.Combine(outputFolder, "04_after_volcano_side_light_game_view.png"));
            Debug.Log("ENVIRONMENT_MOOD_TEST_CAPTURES_COMPLETE: " + outputFolder);
        }

        private static void BuildTestScene(bool showDialog)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) != null,
                "The final locked volcano scene is missing.");

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            Require(source.IsValid(), "The final locked volcano scene could not be opened.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupScenePath) == null)
            {
                Require(EditorSceneManager.SaveScene(source, BackupScenePath, true),
                    "The locked volcano backup scene could not be created.");
            }

            EditableIslandMeshCanvas sourceCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(sourceCanvas != null, "The final volcano terrain is missing.");
            sourceCanvas.CopyState(out float[] beforeHeights, out int[] beforePaint);
            string beforeTransforms = CaptureNonLightingTransformSignature();
            string beforeMaterials = CaptureRendererMaterialSignature();

            Require(EditorSceneManager.SaveScene(source, TestScenePath, true),
                "The environment mood test scene copy could not be created.");
            Scene test = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            Require(test.IsValid(), "The environment mood test scene copy could not be opened.");

            ApplyFastSkyTropicalSky();
            Light sun = CreateTropicalTestSun();

            EditableIslandMeshCanvas testCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(testCanvas != null, "The copied environment test terrain is missing.");
            testCanvas.CopyState(out float[] afterHeights, out int[] afterPaint);
            Require(beforeHeights.SequenceEqual(afterHeights), "Terrain heights changed during the sky/sun test.");
            Require(beforePaint.SequenceEqual(afterPaint), "Terrain paint changed during the sky/sun test.");
            Require(beforeTransforms == CaptureNonLightingTransformSignature(),
                "A non-light object transform or active state changed during the sky/sun test.");
            Require(beforeMaterials == CaptureRendererMaterialSignature(),
                "A renderer material changed during the sky/sun test.");
            Require(!RenderSettings.fog, "Fog must remain disabled in the tropical daytime test.");
            Require(RenderSettings.sun == sun, "The test sun was not assigned as the scene sun.");

            EditorSceneManager.MarkSceneDirty(test);
            Require(EditorSceneManager.SaveScene(test, TestScenePath),
                "The environment mood test scene could not be saved.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string pipeline = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.GetType().Name
                : "Built-in Render Pipeline";
            Vector3 rotation = sun.transform.eulerAngles;
            Debug.Log(
                $"ENVIRONMENT_MOOD_TEST_COMPLETE|scene={TestScenePath}" +
                $"|pipeline={pipeline}|skyShader={RenderSettings.skybox.shader.name}|skyPreset=FastSky-Stylised|fog=off" +
                $"|sunName={sun.name}|sunRotation={rotation.x:F1},{rotation.y:F1},{rotation.z:F1}" +
                $"|sunColor=#{ColorUtility.ToHtmlStringRGB(sun.color)}|sunIntensity={sun.intensity:F2}" +
                $"|shadowStrength={sun.shadowStrength:F2}|ambientMode={RenderSettings.ambientMode}" +
                "|terrainChange=none|rendererMaterialChange=none|nonLightTransformChange=none" +
                "|waterChange=none|gameplayChange=none");

            if (showDialog)
            {
                OpenTestScene(false);
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "EnvironmentMoodTest_SkySun was created from the locked final volcano. Only the sky, ambient settings, fog state, and Sun_Tropical_Test were changed.",
                    "OK");
            }
        }

        private static void ApplyFastSkyTropicalSky()
        {
            Material template = AssetDatabase.LoadAssetAtPath<Material>(FastSkyTemplateMaterialPath);
            Require(template != null, "FastSky StylisedSky material is unavailable.");
            Require(template.shader != null && template.shader.name == "GapperGames/FastSky",
                "The installed FastSky material does not use the expected GapperGames/FastSky shader.");

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(template) { name = "Environment Mood Test FastSky Tropical Sparse Clouds" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            else
            {
                sky.shader = template.shader;
                sky.CopyPropertiesFromMaterial(template);
            }

            // Bright, clean tropical daytime scattering.
            sky.SetColor("_DayColour", new Color(0.91f, 0.97f, 1.00f, 1f));
            sky.SetFloat("_Saturation", 1.28f);
            sky.SetFloat("_DensityPower", 8.4f);
            sky.SetFloat("_DayScatterStrength", 1.08f);
            sky.SetFloat("_DayBrightness", 1.12f);
            sky.SetFloat("_EveningThreshold", 2.0f);

            // Sparse, broad cloud groups with soft edges. Keep plenty of open blue sky.
            sky.SetColor("_CloudColour", Color.white);
            sky.SetFloat("_CloudBrightness", 14.0f);
            sky.SetFloat("_CloudDensity", 1.35f);
            sky.SetFloat("_CloudDensitySmoothness", 0.72f);
            sky.SetFloat("_CloudHeight", 0.12f);
            sky.SetFloat("_CloudThickness", 0.17f);
            sky.SetFloat("_CloudSoftness", 0.28f);
            sky.SetFloat("_CloudSmoothness", 0.14f);
            sky.SetFloat("_CloudScale", 0.42f);
            sky.SetFloat("_CloudTwirl", 0.35f);
            sky.SetFloat("_CloudSpeed", 0.22f);
            sky.SetFloat("_CloudAnisotropy", 0.42f);

            sky.SetFloat("_SunSize", 0.045f);
            sky.SetFloat("_SunIntensity", 35f);
            sky.SetFloat("_StarBrightness", 0f);
            EditorUtility.SetDirty(sky);

            RenderSettings.skybox = sky;
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.82f, 1.00f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.76f, 0.90f, 0.94f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.36f, 0.40f, 0.42f, 1f);
            RenderSettings.ambientIntensity = 0.82f;
            RenderSettings.reflectionIntensity = 0.62f;
            RenderSettings.subtractiveShadowColor = new Color(0.28f, 0.35f, 0.42f, 1f);
        }

        private static Light CreateTropicalTestSun()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Light source = lights.FirstOrDefault(light => light.type == LightType.Directional && light.name != "Sun_Tropical_Test");
            foreach (Light light in lights)
            {
                if (light.type == LightType.Directional && light.name == "Sun_Tropical_Test")
                {
                    Object.DestroyImmediate(light.gameObject);
                }
            }

            GameObject sunObject = source != null
                ? Object.Instantiate(source.gameObject)
                : new GameObject("Sun_Tropical_Test");
            sunObject.name = "Sun_Tropical_Test";
            Light sun = sunObject.GetComponent<Light>() ?? sunObject.AddComponent<Light>();
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light != sun && light.type == LightType.Directional) light.enabled = false;
            }

            sun.enabled = true;
            sun.type = LightType.Directional;
            sun.color = new Color(1.00f, 0.945f, 0.84f, 1f);
            sun.intensity = 1.06f;
            sun.bounceIntensity = 0.90f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.58f;
            sun.shadowBias = 0.045f;
            sun.shadowNormalBias = 0.32f;
            sun.useColorTemperature = false;
            sun.transform.position = Vector3.zero;
            sun.transform.rotation = Quaternion.Euler(42f, -38f, 0f);
            RenderSettings.sun = sun;
            return sun;
        }

        private static string CaptureNonLightingTransformSignature()
        {
            StringBuilder builder = new StringBuilder(1 << 20);
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform transform in transforms
                .Where(item => item.GetComponent<Light>() == null)
                .OrderBy(BuildHierarchyPath, StringComparer.Ordinal))
            {
                builder.Append(BuildHierarchyPath(transform)).Append('|')
                    .Append(transform.gameObject.activeSelf ? '1' : '0').Append('|')
                    .Append(FormatVector(transform.localPosition)).Append('|')
                    .Append(FormatQuaternion(transform.localRotation)).Append('|')
                    .Append(FormatVector(transform.localScale)).Append('\n');
            }
            return builder.ToString();
        }

        private static string CaptureRendererMaterialSignature()
        {
            StringBuilder builder = new StringBuilder(1 << 20);
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Renderer renderer in renderers.OrderBy(item => BuildHierarchyPath(item.transform), StringComparer.Ordinal))
            {
                builder.Append(BuildHierarchyPath(renderer.transform));
                foreach (Material material in renderer.sharedMaterials)
                {
                    string path = material != null ? AssetDatabase.GetAssetPath(material) : "<null>";
                    builder.Append('|').Append(AssetDatabase.AssetPathToGUID(path));
                }
                builder.Append('\n');
            }
            return builder.ToString();
        }

        private static string BuildHierarchyPath(Transform transform)
        {
            Stack<string> parts = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                parts.Push(current.GetSiblingIndex().ToString("D4", CultureInfo.InvariantCulture) + ":" + current.name);
                current = current.parent;
            }
            return string.Join("/", parts);
        }

        private static string FormatVector(Vector3 value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R}", value.x, value.y, value.z);
        }

        private static string FormatQuaternion(Quaternion value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:R},{1:R},{2:R},{3:R}", value.x, value.y, value.z, value.w);
        }

        private static void CaptureMainPlayerView(EditableIslandMeshCanvas canvas, string outputPath)
        {
            Vector3 position = new Vector3(35f, 0f, -545f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.2f, canvas.WaterLevel + 3.2f);
            CapturePerspective(outputPath, position, new Vector3(28f, canvas.WaterLevel + 116f, 80f), 58f);
        }

        private static void CaptureBeachView(EditableIslandMeshCanvas canvas, string outputPath)
        {
            Vector3 position = new Vector3(-285f, 0f, -430f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.4f, canvas.WaterLevel + 3.4f);
            CapturePerspective(outputPath, position, new Vector3(28f, canvas.WaterLevel + 120f, 80f), 58f);
        }

        private static void CaptureSideLightingView(EditableIslandMeshCanvas canvas, string outputPath)
        {
            Vector3 position = new Vector3(-285f, 0f, -430f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.4f, canvas.WaterLevel + 3.4f);
            CapturePerspective(outputPath, position, new Vector3(42f, canvas.WaterLevel + 132f, 88f), 50f);
        }

        private static void CapturePerspective(string outputPath, Vector3 position, Vector3 target, float fieldOfView)
        {
            GameObject cameraObject = new GameObject("Temporary Environment Mood Test Capture Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 3500f;
            camera.transform.position = position;
            camera.transform.LookAt(target);
            WriteCameraPng(camera, outputPath);
            Object.DestroyImmediate(cameraObject);
        }

        private static void WriteCameraPng(Camera camera, string outputPath)
        {
            const int width = 1600;
            const int height = 900;
            RenderTexture renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
            Texture2D capture = new Texture2D(width, height, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            camera.aspect = width / (float)height;
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            capture.Apply();
            File.WriteAllBytes(outputPath, capture.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(renderTexture);
        }

        private static void OpenTestScene(bool frame)
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TestScenePath);
            if (scene == null)
            {
                Debug.LogError("ENVIRONMENT MOOD TEST SCENE NOT FOUND: " + TestScenePath);
                return;
            }

            EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            GameObject sun = GameObject.Find("Sun_Tropical_Test");
            Selection.activeGameObject = sun;
            if (frame && sun != null && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
