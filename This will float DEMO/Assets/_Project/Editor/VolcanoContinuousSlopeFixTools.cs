using System;
using System.IO;
using System.Linq;
using System.Text;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class VolcanoContinuousSlopeFixTools
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string CurrentScenePath = "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string SourceBackupPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_BEFORE_CONTINUOUS_SUMMIT_SLOPE.unity";
        private const string EnvironmentBackupPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_BEFORE_CONTINUOUS_SUMMIT_SLOPE.unity";
        private const string MarkerName = "CONTINUOUS SUMMIT SLOPE - NO TWO STAGE SHOULDER";
        private const string CraterObjectOriginal = "Visible Caldera Walls And Floor";
        private const string CraterObjectRefined = "Visible Caldera Walls And Floor - Crater Only Refinement";
        private const string OriginalVolcanoName = "Ultimate Stamp Volcano - Blocky Cliff Rim Details";
        private static readonly Vector2 VolcanoCenter = new Vector2(35f, 75f);
        private static readonly Vector2 CraterCenter = VolcanoCenter + new Vector2(-7f, 5f);

        [MenuItem("This Will Float DEMO/2. BUILD CONTINUOUS VOLCANO SUMMIT SLOPE")]
        public static void BuildFromMenu() => Build(true);

        [MenuItem("This Will Float DEMO/2. OPEN CONTINUOUS VOLCANO SUMMIT SLOPE")]
        public static void OpenFromMenu()
        {
            EditorSceneManager.OpenScene(CurrentScenePath, OpenSceneMode.Single);
        }

        public static void BuildBatch() => Build(false);

        public static void CaptureBatch()
        {
            string folder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "VolcanoContinuousSlopeCaptures");
            Directory.CreateDirectory(folder);

            EditorSceneManager.OpenScene(EnvironmentBackupPath, OpenSceneMode.Single);
            EditableIslandMeshCanvas before = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(before != null, "The before-slope environment backup is missing terrain.");
            CaptureFront(before, Path.Combine(folder, "01_before_two_stage_shoulder.png"));

            EditorSceneManager.OpenScene(CurrentScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas after = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(after != null, "The continuous-slope environment scene is missing terrain.");
            CaptureFront(after, Path.Combine(folder, "02_after_continuous_slope.png"));
            CaptureSide(after, Path.Combine(folder, "03_after_side.png"));
            CaptureTop(after, Path.Combine(folder, "04_after_crater_top.png"));
            Debug.Log("VOLCANO_CONTINUOUS_SLOPE_CAPTURES_COMPLETE: " + folder);
        }

        public static void ReportProfilesBatch()
        {
            EditorSceneManager.OpenScene(CurrentScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(canvas != null, "Island terrain is missing.");
            StringBuilder report = new StringBuilder("VOLCANO_CONTINUOUS_SLOPE_PROFILE");
            float[] directions = { -90f, -45f, 0f, 45f, 90f, 135f, 180f, 225f };
            foreach (float degrees in directions)
            {
                float radians = degrees * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                report.Append("\nangle=").Append(degrees.ToString("F0"));
                for (int radius = 0; radius <= 72; radius += 4)
                {
                    Vector2 point = CraterCenter + direction * radius;
                    float height = canvas.SampleHeightWorld(new Vector3(point.x, 0f, point.y));
                    report.Append('|').Append(radius).Append(':').Append(height.ToString("F2"));
                }
            }
            Debug.Log(report.ToString());
        }

        private static void Build(bool showDialog)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) != null,
                "The current final volcano source scene is missing.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(CurrentScenePath) != null,
                "The FastSky environment test scene is missing.");

            Scene environment = EditorSceneManager.OpenScene(CurrentScenePath, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentBackupPath) == null)
            {
                Require(EditorSceneManager.SaveScene(environment, EnvironmentBackupPath, true),
                    "Could not preserve the current FastSky environment scene.");
            }

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceBackupPath) == null)
            {
                Require(EditorSceneManager.SaveScene(source, SourceBackupPath, true),
                    "Could not preserve the current volcano source scene.");
            }

            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(canvas != null, "The current volcano terrain is missing.");
            canvas.CopyState(out float[] beforeHeights, out int[] beforePaint);
            float beforePeak = beforeHeights.Max();

            if (GameObject.Find(MarkerName) == null)
            {
                canvas.ApplyContinuousSummitSlopeFix(VolcanoCenter, CraterCenter);
                VolcanoRockStyleTestTools.RebuildFinalConeSurfaceOverlay(canvas);
                GameObject crater = GameObject.Find(CraterObjectRefined) ?? GameObject.Find(CraterObjectOriginal);
                Require(crater != null, "The current crater overlay is missing.");
                VolcanoRockStyleTestTools.RefineExistingCraterOverlay(canvas, crater);

                GameObject volcanoRoot = GameObject.Find(OriginalVolcanoName);
                Require(volcanoRoot != null, "The current volcano root is missing.");
                GameObject marker = new GameObject(MarkerName);
                marker.transform.SetParent(volcanoRoot.transform, false);
                marker.transform.SetSiblingIndex(0);
            }

            canvas.CopyState(out float[] afterHeights, out int[] afterPaint);
            ValidateTerrainEdit(canvas, beforeHeights, afterHeights, beforePaint, afterPaint, beforePeak);
            EditorSceneManager.MarkSceneDirty(source);
            Require(EditorSceneManager.SaveScene(source, SourceScenePath),
                "Could not save the continuous-slope volcano source scene.");
            AssetDatabase.SaveAssets();

            // Rebuild the existing environment copy from the corrected volcano source. This
            // reapplies the already-approved FastSky and Sun_Tropical_Test settings unchanged.
            EnvironmentMoodTestTools.BuildTestSceneBatch();
            Scene result = SceneManager.GetActiveScene();
            Require(result.path == CurrentScenePath, "The corrected FastSky environment scene was not rebuilt.");
            Require(GameObject.Find(MarkerName) != null, "The corrected slope marker is missing from the result.");

            Debug.Log(
                "VOLCANO_CONTINUOUS_SLOPE_COMPLETE" +
                "|peakChange=none|baseWidthChange=none|craterOpeningChange=none" +
                "|editedAnnulus=outsideCraterTo142m|environment=FastSkyPreserved" +
                "|waterChange=none|gameplayChange=none");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The two-stage summit shoulder was removed. The cone now rises continuously to the crater rim while the peak, opening, base and FastSky environment remain preserved.",
                    "OK");
            }
        }

        private static void ValidateTerrainEdit(
            EditableIslandMeshCanvas canvas,
            float[] before,
            float[] after,
            int[] beforePaint,
            int[] afterPaint,
            float beforePeak)
        {
            Require(before.Length == after.Length && beforePaint.SequenceEqual(afterPaint),
                "Terrain paint or data dimensions changed.");
            int resolution = Mathf.RoundToInt(Mathf.Sqrt(before.Length));
            Require(resolution * resolution == before.Length, "Unexpected terrain data dimensions.");
            float half = canvas.Size * 0.5f;
            float spacing = canvas.Size / (resolution - 1f);
            int changed = 0;
            float maxLift = 0f;

            const float rotation = -8f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    float delta = after[index] - before[index];
                    if (Mathf.Abs(delta) <= 0.0001f) continue;
                    changed++;
                    Require(delta >= -0.0001f, "The slope fix lowered terrain unexpectedly.");
                    maxLift = Mathf.Max(maxLift, delta);

                    Vector2 point = new Vector2(-half + x * spacing, -half + z * spacing);
                    Vector2 offset = point - CraterCenter;
                    Require(offset.magnitude <= 143f, "Terrain changed outside the summit correction radius.");
                    float rx = offset.x * cos + offset.y * sin;
                    float rz = -offset.x * sin + offset.y * cos;
                    float craterDistance = Mathf.Sqrt(rx * rx / (34f * 34f) + rz * rz / (29f * 29f));
                    Require(craterDistance > 1.08f, "The crater opening or interior changed.");
                }
            }

            Require(changed > 0, "The continuous summit slope fix changed no terrain vertices.");
            Require(maxLift <= 13.5f, "The summit shoulder lift exceeded the limited correction range.");
            Require(Mathf.Abs(after.Max() - beforePeak) < 0.001f, "The volcano peak height changed.");
            Debug.Log($"VOLCANO_CONTINUOUS_SLOPE_VALIDATED|changedVertices={changed}|maxLift={maxLift:F2}|peak={beforePeak:F2}");
        }

        private static void CaptureFront(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(35f, 0f, -545f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.2f, canvas.WaterLevel + 3.2f);
            Capture(path, position, new Vector3(28f, canvas.WaterLevel + 116f, 80f), 58f);
        }

        private static void CaptureSide(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(-285f, 0f, -430f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.4f, canvas.WaterLevel + 3.4f);
            Capture(path, position, new Vector3(42f, canvas.WaterLevel + 132f, 88f), 50f);
        }

        private static void CaptureTop(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(CraterCenter.x - 150f, canvas.WaterLevel + 330f, CraterCenter.y - 185f);
            Capture(path, position, new Vector3(CraterCenter.x, canvas.WaterLevel + 196f, CraterCenter.y), 48f);
        }

        private static void Capture(string path, Vector3 position, Vector3 target, float fov)
        {
            GameObject cameraObject = new GameObject("Temporary Continuous Slope Capture Camera");
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
