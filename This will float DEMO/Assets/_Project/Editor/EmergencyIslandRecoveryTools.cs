using System.IO;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class EmergencyIslandRecoveryTools
    {
        private const string RecoveredApproxMeshPath = "Assets/_Project/Scenes/Recovered_Screenshot_Approx_Island.unity";
        [MenuItem("This Will Float DEMO/0. Emergency Recovery/Rebuild Ultimate Stamp Volcano Island")]
        public static void RebuildScreenshotApproxMeshIsland()
        {
            RebuildUltimateStampReferenceIsland(true);
        }

        public static void RebuildReferenceIslandBatch()
        {
            RebuildUltimateStampReferenceIsland(false);
        }

        public static void CaptureReferenceIslandBatch()
        {
            EditorSceneManager.OpenScene(RecoveredApproxMeshPath, OpenSceneMode.Single);
            EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.GetCanvas();
            if (canvas == null)
            {
                throw new MissingReferenceException("Reference volcano canvas was not found.");
            }

            GameObject cameraObject = new GameObject("Temporary Reference Capture Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.035f, 0.09f, 1f);
            camera.fieldOfView = 42f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 3200f;
            camera.aspect = 16f / 9f;
            camera.transform.position = new Vector3(760f, 840f, -760f);
            camera.transform.LookAt(canvas.transform.position + new Vector3(-12f, 58f, 18f));

            RenderTexture renderTexture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
            Texture2D capture = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = renderTexture;
            capture.ReadPixels(new Rect(0f, 0f, 1600f, 900f), 0, 0);
            capture.Apply();
            File.WriteAllBytes("/tmp/twf-volcano-reference-preview.png", capture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(capture);
            Object.DestroyImmediate(renderTexture);
            Object.DestroyImmediate(cameraObject);
            Debug.Log("Reference volcano preview captured to /tmp/twf-volcano-reference-preview.png");
        }

        private static void RebuildUltimateStampReferenceIsland(bool showDialog)
        {
            EnsureSceneFolder();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateRecoveryLighting();
            EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.ResetOceanCanvas(1400f, 513);
            canvas.gameObject.name = "REFERENCE - Ultimate Stamp Caldera Volcano Island";
            canvas.GenerateUltimateStampVolcanoIsland(StableIslandBuilderUtility.LoadPreferredVolcanoStamp());
            StableIslandBuilderUtility.ApplyReferenceVolcanoPresentation(canvas);

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), RecoveredApproxMeshPath);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(RecoveredApproxMeshPath, OpenSceneMode.Single);
            EditableIslandMeshCanvas recoveredCanvas = StableIslandBuilderUtility.GetCanvas();
            if (recoveredCanvas != null)
            {
                Selection.activeGameObject = recoveredCanvas.gameObject;
                StableIslandBuilderUtility.FrameReferenceVolcano(recoveredCanvas);
            }

            Debug.Log("Ultimate Stamp Volcano 07 reference island rebuilt successfully.");
            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "Volcano 07 was rebuilt as a pale caldera island with radial erosion ridges and a deep navy ocean.",
                    "OK");
            }
        }

        private static void EnsureSceneFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project"))
            {
                AssetDatabase.CreateFolder("Assets", "_Project");
            }

            if (!AssetDatabase.IsValidFolder("Assets/_Project/Scenes"))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "Scenes");
            }
        }

        private static void CreateRecoveryLighting()
        {
            GameObject sun = new GameObject("Recovery Sun");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            RenderSettings.ambientLight = new Color(0.64f, 0.68f, 0.7f);
        }
    }
}
