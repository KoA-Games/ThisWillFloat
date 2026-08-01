using System;
using System.Collections.Generic;
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
    public static class VolcanoTargetGuideTools
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string EnvironmentScenePath = "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string SourceBackupPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_LOCKED_BEFORE_TARGET_GUIDE_HERO_RIDGES.unity";
        private const string EnvironmentBackupPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_LOCKED_BEFORE_TARGET_GUIDE_HERO_RIDGES.unity";
        private const string SurfaceObjectName = "Stylized Volcano - Earth Rock And Charcoal Bands";
        private const string CraterObjectName = "Visible Caldera Walls And Floor - Crater Only Refinement";
        private const string RockRootName = "LOCKED CRATER - STYLIZED VOLCANO ROCK DRESSING";
        private const string SurfaceMaterialPath =
            "Assets/_Project/GeneratedMaterials/Final Volcano Stylized Macro Surface.mat";
        private const string RockMaterialPath =
            "Assets/_Project/GeneratedMaterials/Final Rocks Stylized Warm Volcanic Matte.mat";
        private const string CaptureFolderName = "VolcanoTargetGuideCaptures";
        private static readonly Vector2 VolcanoCenter = new Vector2(35f, 75f);
        private static readonly Vector2 CraterCenter = new Vector2(28f, 80f);

        private readonly struct RockState
        {
            public readonly Transform Transform;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public readonly float Ground;

            public RockState(Transform transform, float ground)
            {
                Transform = transform;
                Position = transform.position;
                Rotation = transform.rotation;
                Scale = transform.localScale;
                Ground = ground;
            }
        }

        [MenuItem("This Will Float DEMO/7. BUILD TARGET GUIDE VOLCANO FORMS")]
        public static void BuildFromMenu() => Build(true);

        [MenuItem("This Will Float DEMO/7. OPEN TARGET GUIDE VOLCANO")]
        public static void OpenFromMenu() => EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);

        public static void BuildBatch() => Build(false);

        private static void Build(bool showDialog)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) != null,
                "The approved volcano source scene is missing.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScenePath) != null,
                "The approved environment scene is missing.");

            string captureFolder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                CaptureFolderName);
            Directory.CreateDirectory(captureFolder);

            Scene environment = EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas environmentCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(environment.IsValid() && environmentCanvas != null, "The approved environment is incomplete.");
            string environmentSignature = CaptureEnvironmentSignature();
            string beforePlayer = Path.Combine(captureFolder, "01_before_same_player.png");
            string beforeTop = Path.Combine(captureFolder, "02_before_top45.png");
            if (!File.Exists(beforePlayer)) CapturePlayer(environmentCanvas, beforePlayer);
            if (!File.Exists(beforeTop)) CaptureTop45(environmentCanvas, beforeTop);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentBackupPath) == null)
            {
                Require(EditorSceneManager.SaveScene(environment, EnvironmentBackupPath, true),
                    "Could not preserve the approved environment before target-guide refinement.");
            }

            Scene source;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceBackupPath) == null)
            {
                source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
                Require(EditorSceneManager.SaveScene(source, SourceBackupPath, true),
                    "Could not preserve the approved volcano before target-guide refinement.");
            }
            else
            {
                source = EditorSceneManager.OpenScene(SourceBackupPath, OpenSceneMode.Single);
                Require(EditorSceneManager.SaveScene(source, SourceScenePath, true),
                    "Could not restore the approved pre-target-guide volcano source.");
                source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            }

            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            GameObject surface = GameObject.Find(SurfaceObjectName);
            GameObject crater = GameObject.Find(CraterObjectName);
            GameObject rockRoot = GameObject.Find(RockRootName);
            Require(canvas != null && surface != null && crater != null && rockRoot != null,
                "The approved volcano surface, crater or rock dressing is incomplete.");
            MeshFilter surfaceFilter = surface.GetComponent<MeshFilter>();
            MeshRenderer surfaceRenderer = surface.GetComponent<MeshRenderer>();
            MeshFilter craterFilter = crater.GetComponent<MeshFilter>();
            Require(surfaceFilter != null && surfaceRenderer != null && craterFilter != null,
                "The approved visible volcano meshes are incomplete.");

            canvas.CopyState(out float[] beforeHeights, out int[] beforePaint);
            float beforePeak = beforeHeights.Max();
            Vector3[] beforeCraterVertices = craterFilter.sharedMesh.vertices.ToArray();
            List<RockState> rocks = CaptureRockStates(canvas, rockRoot);

            canvas.ApplyTargetGuideHeroRidges(VolcanoCenter, CraterCenter);
            VolcanoRockStyleTestTools.RebuildFinalConeSurfaceOverlay(canvas);
            RebuildTargetGuideBrokenCraterOverlay(canvas, crater);

            Material surfaceMaterial = AssetDatabase.LoadAssetAtPath<Material>(SurfaceMaterialPath);
            Material rockMaterial = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
            Require(surfaceMaterial != null && rockMaterial != null,
                "The approved target-guide surface or rock material is missing.");
            surfaceRenderer = surface.GetComponent<MeshRenderer>();
            surfaceRenderer.sharedMaterial = surfaceMaterial;
            EditorUtility.SetDirty(surfaceRenderer);

            SnapRocksToRefinedSurface(canvas, rocks, rockMaterial);
            canvas.CopyState(out float[] afterHeights, out int[] afterPaint);
            ValidateTerrainChange(canvas, beforeHeights, afterHeights, beforePaint, afterPaint, beforePeak);
            ValidateTargetGuideCrater(beforeCraterVertices, crater.GetComponent<MeshFilter>().sharedMesh.vertices, canvas);
            ValidateRockStates(canvas, rocks);

            EditorSceneManager.MarkSceneDirty(source);
            Require(EditorSceneManager.SaveScene(source, SourceScenePath),
                "Could not save the target-guide volcano source scene.");
            AssetDatabase.SaveAssets();
            EnvironmentMoodTestTools.BuildTestSceneBatch();

            Scene finalScene = SceneManager.GetActiveScene();
            EditableIslandMeshCanvas finalCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(finalScene.path == EnvironmentScenePath && finalCanvas != null,
                "The target-guide environment scene was not rebuilt and opened.");
            Require(environmentSignature == CaptureEnvironmentSignature(),
                "Sky, water, lighting or ambient settings changed during volcano refinement.");

            finalCanvas.CopyState(out float[] finalHeights, out int[] finalPaint);
            Require(afterHeights.SequenceEqual(finalHeights) && afterPaint.SequenceEqual(finalPaint),
                "The environment rebuild changed the refined volcano terrain.");

            CapturePlayer(finalCanvas, Path.Combine(captureFolder, "03_after_same_player.png"));
            CaptureHero(finalCanvas, Path.Combine(captureFolder, "04_after_target_hero_player.png"));
            CaptureTop45(finalCanvas, Path.Combine(captureFolder, "05_after_top45.png"));
            CaptureSummit(finalCanvas, Path.Combine(captureFolder, "06_after_summit_close.png"));

            int changed = CountChanged(beforeHeights, afterHeights);
            float maxRaise = MaxDelta(beforeHeights, afterHeights);
            float maxCut = MaxDelta(afterHeights, beforeHeights);
            Debug.Log(
                "VOLCANO_TARGET_GUIDE_COMPLETE" +
                $"|changedVertices={changed}|maxRaise={maxRaise:F2}|maxCut={maxCut:F2}" +
                $"|peakBefore={beforePeak:F2}|peakAfter={afterHeights.Max():F2}" +
                "|baseWidthChange=none|craterFootprintChange=none|craterFloorLowerWallChange=none" +
                "|craterUpperRim=4BroadCuts|heroRidges=5|broadValleys=5|highFrequencyNoise=none" +
                $"|rocksResnapped={rocks.Count}|rockXZRotationChange=none" +
                "|rockScale=crater1.35x,upper1.55x,base1.18x|rockBurial=45pct" +
                "|surfaceMaterial=Final Volcano Stylized Macro Surface|rockMaterial=Final Rocks Stylized Warm Volcanic Matte" +
                "|skyChange=none|waterChange=none|islandOuterChange=none|vegetationChange=none|lightingChange=none|gameplayChange=none");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The target-guide pass added five broad light-catching hero ridges, five large valleys and a four-beat broken outer rim. Base width, peak height and crater footprint remain fixed; selective rock clusters were enlarged and re-buried at 45% for gameplay-distance readability.",
                    "OK");
            }
        }

        private static List<RockState> CaptureRockStates(EditableIslandMeshCanvas canvas, GameObject root)
        {
            List<RockState> result = new List<RockState>();
            foreach (Transform cluster in root.transform.Cast<Transform>())
            {
                foreach (Transform rock in cluster.Cast<Transform>())
                {
                    float ground = canvas.SampleHeightWorld(rock.position);
                    result.Add(new RockState(rock, ground));
                }
            }
            Require(result.Count >= 60, "The approved clustered rock dressing is unexpectedly incomplete.");
            return result;
        }

        private static void SnapRocksToRefinedSurface(
            EditableIslandMeshCanvas canvas,
            IEnumerable<RockState> rocks,
            Material material)
        {
            foreach (RockState state in rocks)
            {
                float newGround = canvas.SampleHeightWorld(state.Position);
                state.Transform.position = state.Position + Vector3.up * (newGround - state.Ground);
                state.Transform.localScale = state.Scale * TargetGuideRockScale(state.Transform);
                foreach (Renderer renderer in state.Transform.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                Require(TryGetRendererBounds(state.Transform, out Bounds bounds),
                    "A target-guide rock has no renderer bounds.");
                float desiredBottom = newGround - bounds.size.y * 0.45f;
                state.Transform.position += Vector3.up * (desiredBottom - bounds.min.y);
            }
        }

        private static float TargetGuideRockScale(Transform rock)
        {
            string cluster = rock.parent == null ? string.Empty : rock.parent.name;
            if (cluster.StartsWith("Crater", StringComparison.OrdinalIgnoreCase)) return 1.35f;
            if (cluster.StartsWith("Upper", StringComparison.OrdinalIgnoreCase)) return 1.55f;
            if (cluster.StartsWith("Base", StringComparison.OrdinalIgnoreCase)) return 1.18f;
            return 1f;
        }

        private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.size.y > 0.001f;
        }

        private static void ValidateRockStates(EditableIslandMeshCanvas canvas, IEnumerable<RockState> rocks)
        {
            foreach (RockState state in rocks)
            {
                Transform rock = state.Transform;
                Require(Mathf.Abs(rock.position.x - state.Position.x) < 0.001f &&
                        Mathf.Abs(rock.position.z - state.Position.z) < 0.001f,
                    "A rock moved laterally during terrain re-snapping.");
                Require(Quaternion.Angle(rock.rotation, state.Rotation) < 0.001f,
                    "A rock rotation changed during terrain re-snapping.");
                Vector3 expectedScale = state.Scale * TargetGuideRockScale(rock);
                Require((rock.localScale - expectedScale).sqrMagnitude < 0.000001f,
                    "A rock did not retain its target-guide scale emphasis.");
                Require(TryGetRendererBounds(rock, out Bounds bounds),
                    "A target-guide rock has no renderer bounds after placement.");
                float ground = canvas.SampleHeightWorld(rock.position);
                float burial = (ground - bounds.min.y) / Mathf.Max(0.001f, bounds.size.y);
                Require(burial >= 0.35f && burial <= 0.55f,
                    "A target-guide rock is floating or over-buried.");
            }
        }

        private static void ValidateTerrainChange(
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
            float half = canvas.Size * 0.5f;
            float spacing = canvas.Size / (resolution - 1f);
            int changed = 0;
            const float rotation = -8f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            for (int i = 0; i < before.Length; i++)
            {
                float delta = after[i] - before[i];
                if (Mathf.Abs(delta) <= 0.0001f) continue;
                changed++;
                int x = i % resolution;
                int z = i / resolution;
                Vector2 point = new Vector2(-half + x * spacing, -half + z * spacing);
                Vector2 volcanoOffset = point - VolcanoCenter;
                Require(volcanoOffset.magnitude <= 327f,
                    "Terrain changed outside the target-guide volcano edit radius.");
                Vector2 craterOffset = point - CraterCenter;
                float rx = craterOffset.x * cos + craterOffset.y * sin;
                float rz = -craterOffset.x * sin + craterOffset.y * cos;
                float craterDistance = Mathf.Sqrt(rx * rx / (34f * 34f) + rz * rz / (29f * 29f));
                Require(craterDistance > 0.60f, "The approved crater floor or lower inner wall changed.");
                Require(delta <= 21.51f && delta >= -10.01f,
                    "A target-guide terrain edit exceeded its broad-form limit.");
            }
            Require(changed > 1200, "Too few vertices changed for readable hero ridges.");
            Require(Mathf.Abs(after.Max() - beforePeak) < 0.001f, "The approved volcano peak height changed.");
        }

        private static void RebuildTargetGuideBrokenCraterOverlay(
            EditableIslandMeshCanvas canvas,
            GameObject crater)
        {
            MeshFilter filter = crater.GetComponent<MeshFilter>();
            MeshRenderer renderer = crater.GetComponent<MeshRenderer>();
            Require(filter != null && renderer != null && renderer.sharedMaterial != null,
                "The approved crater overlay is incomplete.");

            // Twenty broad radial faces keep the summit deliberately low-poly. The XZ footprint
            // remains the approved 34x29m warped ellipse; only a restrained crest lift is added.
            const int segments = 20;
            float[] ringDistances = { 0.28f, 0.52f, 0.76f, 0.96f, 1.10f, 1.22f };
            Vector3[] vertices = new Vector3[1 + segments * ringDistances.Length];
            Color[] colors = new Color[vertices.Length];
            List<int> triangles = new List<int>(segments * ringDistances.Length * 6);
            const float rotation = -8f * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);

            vertices[0] = new Vector3(
                CraterCenter.x,
                canvas.SampleHeightWorld(new Vector3(CraterCenter.x, 0f, CraterCenter.y)) + 0.58f,
                CraterCenter.y);
            colors[0] = new Color(1f, 0f, 0f, 1f);

            for (int ring = 0; ring < ringDistances.Length; ring++)
            {
                float distance = ringDistances[ring];
                float crestEnvelope = Mathf.Exp(-Mathf.Pow((distance - 0.98f) / 0.16f, 2f));
                for (int segment = 0; segment < segments; segment++)
                {
                    float angle = segment / (float)segments * Mathf.PI * 2f;
                    float warp = 1f
                        + Mathf.Sin(angle * 3f + 0.35f) * 0.032f
                        + Mathf.Sin(angle * 2f - 0.8f) * 0.022f;
                    float px = Mathf.Cos(angle) * 34f * distance * warp;
                    float pz = Mathf.Sin(angle) * 29f * distance * warp;
                    float rotatedX = px * cos - pz * sin;
                    float rotatedZ = px * sin + pz * cos;
                    float worldX = CraterCenter.x + rotatedX;
                    float worldZ = CraterCenter.y + rotatedZ;

                    // Four readable height beats, with one deliberately lowered/broken sector.
                    // The range is intentionally small enough to avoid crown-like spikes.
                    float broadBeat = 1.0f
                        + Mathf.Sin(angle * 4f + 0.42f) * 0.38f
                        + Mathf.Cos(angle * 2f - 0.75f) * 0.22f;
                    float brokenSector = Mathf.Exp(-Mathf.Pow(Mathf.DeltaAngle(
                        angle * Mathf.Rad2Deg, -38f) / 25f, 2f));
                    broadBeat -= brokenSector * 0.45f;
                    broadBeat = Mathf.Clamp(broadBeat, 0.25f, 1.55f);

                    int index = 1 + ring * segments + segment;
                    vertices[index] = new Vector3(
                        worldX,
                        canvas.SampleHeightWorld(new Vector3(worldX, 0f, worldZ))
                            + 0.58f + crestEnvelope * broadBeat,
                        worldZ);

                    float floorWeight = 1f - Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.24f, 0.40f, distance));
                    float rimWeight = Mathf.SmoothStep(0f, 1f,
                        Mathf.InverseLerp(0.70f, 0.96f, distance));
                    float wallWeight = Mathf.Clamp01(1f - floorWeight - rimWeight);
                    float sum = Mathf.Max(0.0001f, floorWeight + wallWeight + rimWeight);
                    colors[index] = new Color(
                        floorWeight / sum,
                        wallWeight / sum,
                        rimWeight / sum,
                        1f);
                }
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                triangles.Add(0);
                triangles.Add(1 + segment);
                triangles.Add(1 + next);
            }
            for (int ring = 0; ring < ringDistances.Length - 1; ring++)
            {
                int innerStart = 1 + ring * segments;
                int outerStart = innerStart + segments;
                for (int segment = 0; segment < segments; segment++)
                {
                    int next = (segment + 1) % segments;
                    int a = innerStart + segment;
                    int b = innerStart + next;
                    int c = outerStart + segment;
                    int d = outerStart + next;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Target Guide Broken Crater - 20 Broad Faces",
                vertices = vertices,
                colors = colors,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            EditorUtility.SetDirty(filter);
        }

        private static void ValidateTargetGuideCrater(
            Vector3[] before,
            Vector3[] after,
            EditableIslandMeshCanvas canvas)
        {
            Require(before.Length > 16 && after.Length == 121,
                "The target-guide crater topology is incomplete.");
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector3 vertex in after)
            {
                min = Vector2.Min(min, new Vector2(vertex.x, vertex.z));
                max = Vector2.Max(max, new Vector2(vertex.x, vertex.z));
            }
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = max - min;
            Require(Vector2.Distance(center, CraterCenter) < 2.5f,
                "The crater placement changed.");
            Require(size.x >= 77f && size.x <= 88f && size.y >= 66f && size.y <= 78f,
                "The crater footprint changed outside the approved envelope.");
            float expectedFloor = canvas.SampleHeightWorld(
                new Vector3(CraterCenter.x, 0f, CraterCenter.y)) + 0.58f;
            Require(Mathf.Abs(after[0].y - expectedFloor) < 0.001f,
                "The approved crater floor depth changed.");
        }

        private static int CountChanged(float[] before, float[] after)
        {
            int count = 0;
            for (int i = 0; i < before.Length; i++)
                if (Mathf.Abs(after[i] - before[i]) > 0.0001f) count++;
            return count;
        }

        private static float MaxDelta(float[] before, float[] after)
        {
            float max = 0f;
            for (int i = 0; i < before.Length; i++) max = Mathf.Max(max, after[i] - before[i]);
            return max;
        }

        private static void CapturePlayer(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(35f, 0f, -545f);
            position.y = Mathf.Max(canvas.SampleHeightWorld(position) + 3.2f, canvas.WaterLevel + 3.2f);
            Capture(path, position, new Vector3(28f, canvas.WaterLevel + 116f, 80f), 58f);
        }

        private static void CaptureHero(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 position = new Vector3(35f, canvas.WaterLevel + 5.2f, -468f);
            Capture(path, position, new Vector3(28f, canvas.WaterLevel + 128f, 80f), 50f);
        }

        private static void CaptureTop45(EditableIslandMeshCanvas canvas, string path)
        {
            Capture(path,
                new Vector3(CraterCenter.x - 245f, canvas.WaterLevel + 438f, CraterCenter.y - 285f),
                new Vector3(35f, canvas.WaterLevel + 116f, 75f),
                49f);
        }

        private static void CaptureSummit(EditableIslandMeshCanvas canvas, string path)
        {
            Capture(path,
                new Vector3(CraterCenter.x - 118f, canvas.WaterLevel + 282f, CraterCenter.y - 156f),
                new Vector3(CraterCenter.x, canvas.WaterLevel + 205f, CraterCenter.y),
                44f);
        }

        private static void Capture(string path, Vector3 position, Vector3 target, float fov)
        {
            GameObject cameraObject = new GameObject("Temporary Target Guide Volcano Camera");
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

        private static string CaptureEnvironmentSignature()
        {
            string sky = AssetDatabase.GetAssetPath(RenderSettings.skybox);
            Light sun = GameObject.Find("Sun_Tropical_Test")?.GetComponent<Light>();
            GameObject ocean = GameObject.Find("Ocean - waves, visual only");
            string oceanMaterials = ocean == null ? "none" : string.Join(",",
                ocean.GetComponentsInChildren<Renderer>(true)
                    .SelectMany(r => r.sharedMaterials)
                    .Where(m => m != null)
                    .Select(AssetDatabase.GetAssetPath)
                    .OrderBy(p => p));
            return string.Format(
                "sky={0}|ambient={1}:{2:F4}|fog={3}|sun={4}|ocean={5}",
                sky,
                RenderSettings.ambientMode,
                RenderSettings.ambientIntensity,
                RenderSettings.fog,
                sun == null ? "none" : string.Format(
                    "{0:F4},{1:F4},{2:F4},{3:F4}:{4:F4}:{5:F3},{6:F3}",
                    sun.color.r, sun.color.g, sun.color.b, sun.intensity,
                    sun.shadowStrength, sun.transform.eulerAngles.x, sun.transform.eulerAngles.y),
                oceanMaterials);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
