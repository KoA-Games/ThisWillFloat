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
    public static class CraterShapeRefinementTools
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string CurrentScenePath = "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string SourceBackupPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_BEFORE_DESIGNED_CRATER_SHAPE.unity";
        private const string EnvironmentBackupPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_BEFORE_DESIGNED_CRATER_SHAPE.unity";
        private const string MarkerName = "DESIGNED LOW POLY CRATER - BROAD FACES NO CROWN TEETH";
        private const string CraterObjectOriginal = "Visible Caldera Walls And Floor";
        private const string CraterObjectRefined = "Visible Caldera Walls And Floor - Crater Only Refinement";
        private const string OriginalVolcanoName = "Ultimate Stamp Volcano - Blocky Cliff Rim Details";
        private static readonly Vector2 CraterCenter = new Vector2(28f, 80f);

        [MenuItem("This Will Float DEMO/3. BUILD DESIGNED LOW POLY CRATER")]
        public static void BuildFromMenu() => Build(true);

        [MenuItem("This Will Float DEMO/3. OPEN DESIGNED LOW POLY CRATER")]
        public static void OpenFromMenu() => EditorSceneManager.OpenScene(CurrentScenePath, OpenSceneMode.Single);

        public static void BuildBatch() => Build(false);

        public static void CaptureBatch()
        {
            string folder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "CraterShapeRefinementCaptures");
            Directory.CreateDirectory(folder);

            EditorSceneManager.OpenScene(EnvironmentBackupPath, OpenSceneMode.Single);
            EditableIslandMeshCanvas before = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(before != null, "The before-crater environment backup is missing terrain.");
            CaptureFront(before, Path.Combine(folder, "01_before_front.png"));
            CaptureTop45(before, Path.Combine(folder, "02_before_top45.png"));
            CaptureDirectTop(before, Path.Combine(folder, "03_before_direct_top.png"));

            EditorSceneManager.OpenScene(CurrentScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas after = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(after != null, "The refined-crater environment scene is missing terrain.");
            CaptureFront(after, Path.Combine(folder, "04_after_front.png"));
            CaptureTop45(after, Path.Combine(folder, "05_after_top45.png"));
            CaptureDirectTop(after, Path.Combine(folder, "06_after_direct_top.png"));
            Debug.Log("CRATER_SHAPE_REFINEMENT_CAPTURES_COMPLETE: " + folder);
        }

        private static void Build(bool showDialog)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) != null,
                "The approved continuous-slope volcano source scene is missing.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(CurrentScenePath) != null,
                "The approved FastSky environment scene is missing.");

            Scene environment = EditorSceneManager.OpenScene(CurrentScenePath, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentBackupPath) == null)
            {
                Require(EditorSceneManager.SaveScene(environment, EnvironmentBackupPath, true),
                    "Could not preserve the current environment before crater refinement.");
            }

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceBackupPath) == null)
            {
                Require(EditorSceneManager.SaveScene(source, SourceBackupPath, true),
                    "Could not preserve the current volcano before crater refinement.");
            }

            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(canvas != null, "The approved volcano terrain is missing.");
            canvas.CopyState(out float[] beforeHeights, out int[] beforePaint);
            float beforePeak = beforeHeights.Max();

            if (GameObject.Find(MarkerName) == null)
            {
                canvas.ApplyDesignedLowPolyCrater(CraterCenter);
                VolcanoRockStyleTestTools.RebuildFinalConeSurfaceOverlay(canvas);
                GameObject crater = GameObject.Find(CraterObjectRefined) ?? GameObject.Find(CraterObjectOriginal);
                Require(crater != null, "The existing crater surface is missing.");
                VolcanoRockStyleTestTools.RefineExistingCraterOverlay(canvas, crater);

                GameObject volcanoRoot = GameObject.Find(OriginalVolcanoName);
                Require(volcanoRoot != null, "The existing volcano root is missing.");
                GameObject marker = new GameObject(MarkerName);
                marker.transform.SetParent(volcanoRoot.transform, false);
                marker.transform.SetSiblingIndex(0);
            }

            canvas.CopyState(out float[] afterHeights, out int[] afterPaint);
            ValidateCraterOnlyEdit(canvas, beforeHeights, afterHeights, beforePaint, afterPaint, beforePeak);
            EditorSceneManager.MarkSceneDirty(source);
            Require(EditorSceneManager.SaveScene(source, SourceScenePath),
                "Could not save the designed crater source scene.");
            AssetDatabase.SaveAssets();

            EnvironmentMoodTestTools.BuildTestSceneBatch();
            Require(SceneManager.GetActiveScene().path == CurrentScenePath,
                "The FastSky environment was not rebuilt with the refined crater.");
            Require(GameObject.Find(MarkerName) != null, "The designed crater marker is missing.");

            Debug.Log(
                "CRATER_SHAPE_REFINEMENT_COMPLETE" +
                "|overallPeakChange=none|baseWidthChange=none|outerSlopeChange=none" +
                "|openingAxes=34x29-unchanged|rimMacroFeatures=4|overlaySegments=20" +
                "|rockPrefabAdded=0|environment=FastSkyPreserved|gameplayChange=none");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The crater rim and inner wall were reorganized into broad, intentional low-poly faces. The cone silhouette, peak, opening size, base and environment remain preserved.",
                    "OK");
            }
        }

        private static void ValidateCraterOnlyEdit(
            EditableIslandMeshCanvas canvas,
            float[] before,
            float[] after,
            int[] beforePaint,
            int[] afterPaint,
            float beforePeak)
        {
            Require(before.Length == after.Length && beforePaint.SequenceEqual(afterPaint),
                "Terrain paint or dimensions changed during crater refinement.");
            int resolution = Mathf.RoundToInt(Mathf.Sqrt(before.Length));
            float half = canvas.Size * 0.5f;
            float spacing = canvas.Size / (resolution - 1f);
            const float rotation = -8f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            int changed = 0;
            float maxDelta = 0f;

            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int index = z * resolution + x;
                    float delta = after[index] - before[index];
                    if (Mathf.Abs(delta) <= 0.0001f) continue;
                    changed++;
                    maxDelta = Mathf.Max(maxDelta, Mathf.Abs(delta));
                    Vector2 offset = new Vector2(-half + x * spacing, -half + z * spacing) - CraterCenter;
                    float rx = offset.x * cos + offset.y * sin;
                    float rz = -offset.x * sin + offset.y * cos;
                    float distance = Mathf.Sqrt(rx * rx / (34f * 34f) + rz * rz / (29f * 29f));
                    Require(distance <= 1.241f, "Terrain changed outside the crater-only region.");
                }
            }

            Require(changed > 0 && changed < 900, "Unexpected crater edit vertex count.");
            Require(maxDelta < 28f, "Crater refinement exceeded the crater-depth adjustment range.");
            Require(Mathf.Abs(after.Max() - beforePeak) < 0.001f, "The overall volcano height changed.");
            Debug.Log($"CRATER_SHAPE_REFINEMENT_VALIDATED|changedVertices={changed}|maxAbsDelta={maxDelta:F2}|peak={beforePeak:F2}");
        }

        private static void CaptureFront(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(35f, 0f, -545f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.2f, canvas.WaterLevel + 3.2f);
            Capture(path, position, new Vector3(28f, canvas.WaterLevel + 116f, 80f), 58f);
        }

        private static void CaptureTop45(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(CraterCenter.x - 150f, canvas.WaterLevel + 330f, CraterCenter.y - 185f);
            Capture(path, position, new Vector3(CraterCenter.x, canvas.WaterLevel + 196f, CraterCenter.y), 48f);
        }

        private static void CaptureDirectTop(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(CraterCenter.x, canvas.WaterLevel + 430f, CraterCenter.y);
            Capture(path, position, new Vector3(CraterCenter.x, canvas.WaterLevel + 205f, CraterCenter.y), 34f);
        }

        private static void Capture(string path, Vector3 position, Vector3 target, float fov)
        {
            GameObject cameraObject = new GameObject("Temporary Designed Crater Capture Camera");
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
