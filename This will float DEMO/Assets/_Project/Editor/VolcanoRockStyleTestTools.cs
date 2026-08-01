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
    public static class VolcanoRockStyleTestTools
    {
        private const string BaselineScenePath =
            "Assets/_Project/Scenes/VolcanoConeRefinement.unity";
        public const string TestScenePath =
            "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string MaterialFolder = "Assets/_Project/GeneratedMaterials";
        private const string CraterShaderName = "This Will Float/VRST Crater Only Blend";
        private const string FinalSurfaceShaderName = "This Will Float/Final Volcano Four Color Blend";
        private const string CraterObjectOriginal = "Visible Caldera Walls And Floor";
        private const string CraterObjectRefined = "Visible Caldera Walls And Floor - Crater Only Refinement";
        private const string VolcanoSurfaceObject = "Stylized Volcano - Earth Rock And Charcoal Bands";
        private const string OriginalVolcanoName = "Ultimate Stamp Volcano - Blocky Cliff Rim Details";
        private const string RockTexturePath =
            "Assets/PolyOne/Rocks Stylized/Texture/Rocks_Stylized_Color.png";
        private static readonly Vector2 VolcanoCenter = new Vector2(35f, 75f);
        private static readonly Vector2 CraterCenter = VolcanoCenter + new Vector2(-7f, 5f);

        private static readonly string[] WideRocks =
        {
            "Assets/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_03.prefab",
            "Assets/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_01.prefab"
        };

        private static readonly string[] MediumRocks =
        {
            "Assets/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_04.prefab",
            "Assets/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_07.prefab",
            "Assets/PolyOne/Rocks Stylized/Prefabs/SM_Rocks_08.prefab"
        };

        private readonly struct RockAnchor
        {
            public readonly Vector3 Center;
            public readonly Vector3 Size;
            public readonly Quaternion Rotation;

            public RockAnchor(Vector3 center, Vector3 size, Quaternion rotation)
            {
                Center = center;
                Size = size;
                Rotation = rotation;
            }
        }

        private readonly struct TerrainEditStats
        {
            public readonly int ChangedVertices;
            public readonly float MaxRaise;
            public readonly float BeforeMax;
            public readonly float AfterMax;

            public TerrainEditStats(int changedVertices, float maxRaise, float beforeMax, float afterMax)
            {
                ChangedVertices = changedVertices;
                MaxRaise = maxRaise;
                BeforeMax = beforeMax;
                AfterMax = afterMax;
            }
        }

        [MenuItem("This Will Float DEMO/0. OPEN FINAL VOLCANO POLISH")]
        public static void OpenTestSceneFromMenu()
        {
            OpenTestScene(true);
        }

        [MenuItem("This Will Float DEMO/1. BUILD FINAL VOLCANO POLISH")]
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
                "VolcanoFinalPolishCaptures");
            Directory.CreateDirectory(outputFolder);

            EditorSceneManager.OpenScene(BaselineScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas beforeCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(beforeCanvas != null, "The current-version baseline terrain is missing.");
            CaptureFront(beforeCanvas, Path.Combine(outputFolder, "01_before_front_game_view.png"));
            CaptureCraterHigh(Path.Combine(outputFolder, "03_before_top_game_view.png"));

            EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas afterCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(afterCanvas != null, "The tightened-crater terrain is missing.");
            CaptureFront(afterCanvas, Path.Combine(outputFolder, "02_after_front_game_view.png"));
            CaptureSide(afterCanvas, Path.Combine(outputFolder, "03_after_side_game_view.png"));
            CaptureCraterHigh(Path.Combine(outputFolder, "04_after_top_game_view.png"));
            Debug.Log("VOLCANO_FINAL_POLISH_CAPTURES_COMPLETE: " + outputFolder);
        }

        private static void BuildTestScene(bool showDialog)
        {
            EnsureBaselineScene();
            Scene baseline = EditorSceneManager.OpenScene(BaselineScenePath, OpenSceneMode.Single);
            Require(baseline.IsValid(), "The preserved current-version baseline could not be opened.");
            Require(
                EditorSceneManager.SaveScene(baseline, TestScenePath, true),
                "The crater-tightening test copy could not be saved.");

            Scene testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(canvas != null, "The copied test scene has no island terrain.");
            canvas.CopyState(out float[] beforeHeights, out int[] beforePaint);

            GameObject originalVolcano = FindSceneObject(OriginalVolcanoName);
            Require(originalVolcano != null && originalVolcano.activeSelf,
                "The original volcano must remain active.");

            canvas.ApplyFinalVolcanoPolish(VolcanoCenter, CraterCenter);
            RebuildFinalConeSurfaceOverlay(canvas);
            GameObject crater = FindSceneObject(CraterObjectRefined) ?? FindSceneObject(CraterObjectOriginal);
            Require(crater != null, "The existing crater interior object is missing.");
            RefineExistingCraterOverlay(canvas, crater);

            canvas.CopyState(out float[] afterHeights, out int[] afterPaint);
            TerrainEditStats stats = ValidateLocalTerrainEdit(
                canvas,
                beforeHeights,
                afterHeights,
                beforePaint,
                afterPaint);
            GameObject marker = new GameObject("FINAL VOLCANO POLISH - SHARP SUMMIT FIVE HERO RIDGES");
            marker.transform.SetParent(originalVolcano.transform, false);
            marker.transform.SetSiblingIndex(0);

            EditorSceneManager.MarkSceneDirty(testScene);
            Require(EditorSceneManager.SaveScene(testScene, TestScenePath),
                "The tightened-crater test scene could not be saved.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"VOLCANO_FINAL_POLISH_COMPLETE|scene={TestScenePath}" +
                $"|changedTerrainVertices={stats.ChangedVertices}|maxRaise={stats.MaxRaise:F2}" +
                $"|beforeMax={stats.BeforeMax:F2}|afterMax={stats.AfterMax:F2}" +
                $"|heightRatio={stats.AfterMax / stats.BeforeMax:F3}" +
                $"|volcanoEditRadius=320|summitHeightChange=6pct|summitWidthChange=-7pct|heroRidges=5|craterAxes=34x29" +
                "|craterSizeChange=none|baseWidthChange=none|environmentChange=none");

            if (showDialog)
            {
                OpenTestScene(false);
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The current cone was preserved while the summit, crater rim, five hero ridges, and four-color blending received their final limited polish.",
                    "OK");
            }
        }

        private static void EnsureBaselineScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BaselineScenePath) != null)
            {
                return;
            }

            Scene current = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            Require(current.IsValid(), "The current volcano version could not be opened for baseline preservation.");
            Require(
                FindSceneObject(CraterObjectRefined) != null,
                "The current crater-only version was not found; refusing to create a baseline from another scene.");
            Require(
                EditorSceneManager.SaveScene(current, BaselineScenePath, true),
                "The current volcano baseline could not be preserved.");
            AssetDatabase.SaveAssets();
        }

        internal static void RefineExistingCraterOverlay(EditableIslandMeshCanvas canvas, GameObject crater)
        {
            MeshFilter filter = crater.GetComponent<MeshFilter>();
            MeshRenderer renderer = crater.GetComponent<MeshRenderer>();
            Require(filter != null && filter.sharedMesh != null, "The crater overlay mesh is missing.");
            Require(renderer != null, "The crater overlay renderer is missing.");

            const int segments = 20;
            float[] ringDistances = { 0.28f, 0.52f, 0.76f, 0.96f, 1.10f, 1.22f };
            Vector3[] vertices = new Vector3[1 + segments * ringDistances.Length];
            Color[] colors = new Color[vertices.Length];
            List<int> triangles = new List<int>(segments * (ringDistances.Length * 6));
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
                    int index = 1 + ring * segments + segment;
                    vertices[index] = new Vector3(
                        worldX,
                        canvas.SampleHeightWorld(new Vector3(worldX, 0f, worldZ)) + 0.58f,
                        worldZ);

                    float floorWeight = 1f - Smooth01(Mathf.InverseLerp(0.24f, 0.38f, distance));
                    float rimWeight = Smooth01(Mathf.InverseLerp(0.72f, 0.94f, distance));
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
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Designed Low Poly Crater - 20 Broad Radial Faces",
                vertices = vertices,
                colors = colors,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;

            Material craterMaterial = GetCraterMaterial();
            Material[] materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = craterMaterial;
            }

            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            crater.name = CraterObjectRefined;
        }

        private static void RebuildConeSurfaceOverlay(EditableIslandMeshCanvas canvas)
        {
            GameObject surface = FindSceneObject(VolcanoSurfaceObject);
            Require(surface != null, "The existing stylized volcano surface overlay is missing.");
            MeshFilter filter = surface.GetComponent<MeshFilter>();
            MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
            Require(filter != null && filter.sharedMesh != null,
                "The existing stylized volcano surface mesh is missing.");
            Require(renderer != null, "The existing stylized volcano surface renderer is missing.");

            const float radius = 350f;
            float half = canvas.Size * 0.5f;
            float spacing = canvas.Size / canvas.QuadResolution;
            int minGridX = Mathf.Clamp(Mathf.FloorToInt((VolcanoCenter.x - radius + half) / spacing), 0, canvas.QuadResolution);
            int maxGridX = Mathf.Clamp(Mathf.CeilToInt((VolcanoCenter.x + radius + half) / spacing), 0, canvas.QuadResolution);
            int minGridZ = Mathf.Clamp(Mathf.FloorToInt((VolcanoCenter.y - radius + half) / spacing), 0, canvas.QuadResolution);
            int maxGridZ = Mathf.Clamp(Mathf.CeilToInt((VolcanoCenter.y + radius + half) / spacing), 0, canvas.QuadResolution);
            int xSegments = maxGridX - minGridX;
            int zSegments = maxGridZ - minGridZ;
            int columns = xSegments + 1;
            Vector3[] vertices = new Vector3[columns * (zSegments + 1)];
            Color[] colors = new Color[vertices.Length];
            bool[] valid = new bool[vertices.Length];
            float[] distances = new float[vertices.Length];
            float[] angles = new float[vertices.Length];

            for (int z = 0; z <= zSegments; z++)
            {
                for (int x = 0; x <= xSegments; x++)
                {
                    float worldX = -half + (minGridX + x) * spacing;
                    float worldZ = -half + (minGridZ + z) * spacing;
                    Vector2 offset = new Vector2(worldX, worldZ) - VolcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float normalizedDistance = offset.magnitude / radius;
                    float irregularOuter = 0.985f
                        + Mathf.Sin(angle * 3f + 0.6f) * 0.028f
                        + Mathf.Cos(angle * 2f - 0.7f) * 0.018f;
                    int index = z * columns + x;
                    vertices[index] = new Vector3(
                        worldX,
                        canvas.SampleHeightWorld(new Vector3(worldX, 0f, worldZ)) + 0.48f,
                        worldZ);
                    colors[index] = Color.white;
                    distances[index] = normalizedDistance;
                    angles[index] = angle;
                    // Keep the four-band cone surface continuous beneath the crater patch.
                    // This prevents any terrain/sand layer from leaking through the summit seam.
                    valid[index] = normalizedDistance <= irregularOuter;
                }
            }

            List<int>[] subTriangles =
            {
                new List<int>(),
                new List<int>(),
                new List<int>(),
                new List<int>()
            };
            float[] colorRidgeAngles = { -158f, -101f, -39f, 24f, 92f, 161f };
            float[] colorRidgeWidths = { 27f, 19f, 24f, 18f, 30f, 22f };
            float[] colorRidgeStrengths = { 1f, 0.72f, 1.08f, 0.66f, 0.92f, 0.78f };
            float[] colorValleyAngles = { -130f, -70f, -7f, 58f, 127f };
            float[] colorValleyWidths = { 25f, 21f, 23f, 24f, 27f };
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int a = z * columns + x;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    if (!valid[a] || !valid[b] || !valid[c] || !valid[d]) continue;
                    float distance = (distances[a] + distances[b] + distances[c] + distances[d]) * 0.25f;
                    float angle = Mathf.Atan2(
                        Mathf.Sin(angles[a]) + Mathf.Sin(angles[b]) + Mathf.Sin(angles[c]) + Mathf.Sin(angles[d]),
                        Mathf.Cos(angles[a]) + Mathf.Cos(angles[b]) + Mathf.Cos(angles[c]) + Mathf.Cos(angles[d]));
                    float angleDegrees = angle * Mathf.Rad2Deg;
                    float ridgeField = 0f;
                    for (int ridge = 0; ridge < colorRidgeAngles.Length; ridge++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, colorRidgeAngles[ridge]));
                        ridgeField = Mathf.Max(
                            ridgeField,
                            Mathf.Exp(-Mathf.Pow(angularDelta / colorRidgeWidths[ridge], 2f))
                                * colorRidgeStrengths[ridge]);
                    }

                    float valleyField = 0f;
                    for (int valley = 0; valley < colorValleyAngles.Length; valley++)
                    {
                        float angularDelta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, colorValleyAngles[valley]));
                        valleyField = Mathf.Max(
                            valleyField,
                            Mathf.Exp(-Mathf.Pow(angularDelta / colorValleyWidths[valley], 2f)));
                    }

                    // Hero ridges pull darker materials downward in broad tongues while valleys
                    // push the boundaries upward. No texture noise or pixel-scale masks are used.
                    float summitBoundary = 0.165f
                        + ridgeField * 0.082f
                        - valleyField * 0.018f
                        + Mathf.Sin(angle * 2f + 0.25f) * 0.012f;
                    float upperBoundary = 0.365f
                        + ridgeField * 0.092f
                        - valleyField * 0.025f
                        + Mathf.Cos(angle * 3f - 0.45f) * 0.018f;
                    float middleBoundary = 0.655f
                        + ridgeField * 0.078f
                        - valleyField * 0.030f
                        + Mathf.Sin(angle * 2f + 0.8f) * 0.022f;
                    int layer = distance <= summitBoundary ? 3
                        : distance <= upperBoundary ? 2
                        : distance <= middleBoundary ? 1
                        : 0;
                    subTriangles[layer].Add(a);
                    subTriangles[layer].Add(c);
                    subTriangles[layer].Add(b);
                    subTriangles[layer].Add(b);
                    subTriangles[layer].Add(c);
                    subTriangles[layer].Add(d);
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Stylized Cone Volcano - Four Clean Color Bands",
                vertices = vertices,
                colors = colors,
                subMeshCount = 4
            };
            for (int i = 0; i < subTriangles.Length; i++) mesh.SetTriangles(subTriangles[i], i);
            mesh.RecalculateNormals();
            Vector3 fakeSunDirection = new Vector3(-0.38f, 0.82f, -0.43f).normalized;
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < colors.Length; i++)
            {
                float light = Mathf.Clamp01((Vector3.Dot(normals[i], fakeSunDirection) + 0.12f) / 1.02f);
                float brightness = Mathf.Lerp(0.82f, 1.10f, Smooth01(light));
                colors[i] = new Color(brightness, brightness, brightness, 1f);
            }
            mesh.colors = colors;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            renderer.sharedMaterials = GetConeSurfaceMaterials();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        internal static void RebuildFinalConeSurfaceOverlay(EditableIslandMeshCanvas canvas)
        {
            GameObject surface = FindSceneObject(VolcanoSurfaceObject);
            Require(surface != null, "The stylized volcano surface overlay is missing.");
            MeshFilter filter = surface.GetComponent<MeshFilter>();
            MeshRenderer renderer = surface.GetComponent<MeshRenderer>();
            Require(filter != null && renderer != null, "The volcano surface renderer is incomplete.");

            const float radius = 350f;
            float half = canvas.Size * 0.5f;
            float spacing = canvas.Size / canvas.QuadResolution;
            int minGridX = Mathf.Clamp(Mathf.FloorToInt((VolcanoCenter.x - radius + half) / spacing), 0, canvas.QuadResolution);
            int maxGridX = Mathf.Clamp(Mathf.CeilToInt((VolcanoCenter.x + radius + half) / spacing), 0, canvas.QuadResolution);
            int minGridZ = Mathf.Clamp(Mathf.FloorToInt((VolcanoCenter.y - radius + half) / spacing), 0, canvas.QuadResolution);
            int maxGridZ = Mathf.Clamp(Mathf.CeilToInt((VolcanoCenter.y + radius + half) / spacing), 0, canvas.QuadResolution);
            int xSegments = maxGridX - minGridX;
            int zSegments = maxGridZ - minGridZ;
            int columns = xSegments + 1;
            Vector3[] vertices = new Vector3[columns * (zSegments + 1)];
            Color[] colors = new Color[vertices.Length];
            Vector2[] uvs = new Vector2[vertices.Length];
            bool[] valid = new bool[vertices.Length];
            List<int> triangles = new List<int>(xSegments * zSegments * 6);

            float[] ridgeAngles = { -151f, -79f, -12f, 61f, 139f };
            float[] ridgeWidths = { 19f, 14f, 18f, 15f, 21f };
            float[] ridgeStrengths = { 1f, 0.68f, 1.08f, 0.76f, 0.90f };
            float[] valleyAngles = { -118f, -47f, 27f, 102f, 177f };
            float[] valleyWidths = { 18f, 15f, 19f, 16f, 20f };

            for (int z = 0; z <= zSegments; z++)
            {
                for (int x = 0; x <= xSegments; x++)
                {
                    float worldX = -half + (minGridX + x) * spacing;
                    float worldZ = -half + (minGridZ + z) * spacing;
                    Vector2 offset = new Vector2(worldX, worldZ) - VolcanoCenter;
                    float angle = Mathf.Atan2(offset.y, offset.x);
                    float angleDegrees = angle * Mathf.Rad2Deg;
                    float distance = offset.magnitude / radius;
                    int index = z * columns + x;
                    vertices[index] = new Vector3(
                        worldX,
                        canvas.SampleHeightWorld(new Vector3(worldX, 0f, worldZ)) + 0.50f,
                        worldZ);

                    float irregularOuter = 0.985f
                        + Mathf.Sin(angle * 3f + 0.6f) * 0.028f
                        + Mathf.Cos(angle * 2f - 0.7f) * 0.018f;
                    valid[index] = distance <= irregularOuter;

                    float ridgeField = 0f;
                    for (int ridge = 0; ridge < ridgeAngles.Length; ridge++)
                    {
                        float delta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, ridgeAngles[ridge]));
                        ridgeField = Mathf.Max(
                            ridgeField,
                            Mathf.Exp(-Mathf.Pow(delta / ridgeWidths[ridge], 2f)) * ridgeStrengths[ridge]);
                    }

                    float valleyField = 0f;
                    for (int valley = 0; valley < valleyAngles.Length; valley++)
                    {
                        float delta = Mathf.Abs(Mathf.DeltaAngle(angleDegrees, valleyAngles[valley]));
                        valleyField = Mathf.Max(
                            valleyField,
                            Mathf.Exp(-Mathf.Pow(delta / valleyWidths[valley], 2f)));
                    }

                    float broadRidgeShade = Mathf.Clamp01(0.50f + ridgeField * 0.50f - valleyField * 0.42f);
                    uvs[index] = new Vector2(broadRidgeShade, 0f);

                    // Broad ridge-following fields replace triangle-by-triangle material cuts.
                    // Vertex interpolation gives clean transitions without textures or normal maps.
                    float summitBoundary = 0.145f
                        + ridgeField * 0.155f
                        - valleyField * 0.024f
                        + Mathf.Sin(angle * 2f + 0.25f) * 0.014f;
                    float upperBoundary = 0.345f
                        + ridgeField * 0.140f
                        - valleyField * 0.032f
                        + Mathf.Cos(angle * 3f - 0.45f) * 0.020f;
                    float middleBoundary = 0.645f
                        + ridgeField * 0.105f
                        - valleyField * 0.034f
                        + Mathf.Sin(angle * 2f + 0.8f) * 0.024f;
                    float summitBlend = Smooth01(Mathf.InverseLerp(summitBoundary + 0.032f, summitBoundary - 0.032f, distance));
                    float upperBlend = Smooth01(Mathf.InverseLerp(upperBoundary + 0.040f, upperBoundary - 0.040f, distance));
                    float middleBlend = Smooth01(Mathf.InverseLerp(middleBoundary + 0.045f, middleBoundary - 0.045f, distance));
                    float summitWeight = summitBlend;
                    float upperWeight = Mathf.Max(0f, upperBlend - summitBlend);
                    float middleWeight = Mathf.Max(0f, middleBlend - upperBlend);
                    float baseWeight = Mathf.Max(0f, 1f - middleBlend);
                    float sum = Mathf.Max(0.0001f, baseWeight + middleWeight + upperWeight + summitWeight);
                    colors[index] = new Color(
                        baseWeight / sum,
                        middleWeight / sum,
                        upperWeight / sum,
                        summitWeight / sum);
                }
            }

            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int a = z * columns + x;
                    int b = a + 1;
                    int c = a + columns;
                    int d = c + 1;
                    if (!valid[a] || !valid[b] || !valid[c] || !valid[d]) continue;
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Final Stylized Volcano - Smooth Four Color Ridge Fields",
                vertices = vertices,
                colors = colors,
                uv = uvs,
                triangles = triangles.ToArray()
            };
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            renderer.sharedMaterial = GetFinalSurfaceBlendMaterial();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }

        private static int ReplaceNonFreeRockAssets(
            EditableIslandMeshCanvas canvas,
            Material material,
            out int selectedAnchorCount,
            out int freePackInstanceCount)
        {
            HashSet<GameObject> sourceRoots = new HashSet<GameObject>();
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (Transform transform in transforms)
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);
                if (root == null || sourceRoots.Contains(root))
                {
                    continue;
                }

                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if (IsNonFreeRockPath(path))
                {
                    sourceRoots.Add(root);
                }
            }

            List<RockAnchor> anchors = new List<RockAnchor>(sourceRoots.Count);
            foreach (GameObject root in sourceRoots)
            {
                if (TryCalculateBounds(root, out Bounds bounds))
                {
                    anchors.Add(new RockAnchor(bounds.center, bounds.size, root.transform.rotation));
                }

                // These assets are intentionally replaced, not merely hidden. Destroying the
                // prefab roots also prevents stale/missing prefab references in the final scene.
                Object.DestroyImmediate(root);
            }

            anchors.Sort((a, b) =>
                Mathf.Max(b.Size.x, b.Size.z).CompareTo(Mathf.Max(a.Size.x, a.Size.z)));
            List<RockAnchor> selectedAnchors = new List<RockAnchor>();
            foreach (RockAnchor candidate in anchors)
            {
                Vector2 candidatePoint = new Vector2(candidate.Center.x, candidate.Center.z);
                if (Vector2.Distance(candidatePoint, CraterCenter) < 128f)
                {
                    continue;
                }

                float candidateFootprint = Mathf.Max(candidate.Size.x, candidate.Size.z);
                float spacing = candidateFootprint >= 28f ? 46f : 34f;
                bool overlaps = false;
                foreach (RockAnchor accepted in selectedAnchors)
                {
                    Vector2 acceptedPoint = new Vector2(accepted.Center.x, accepted.Center.z);
                    if (Vector2.Distance(candidatePoint, acceptedPoint) < spacing)
                    {
                        overlaps = true;
                        break;
                    }
                }

                if (!overlaps)
                {
                    selectedAnchors.Add(candidate);
                    if (selectedAnchors.Count >= 64)
                    {
                        break;
                    }
                }
            }

            selectedAnchorCount = selectedAnchors.Count;
            GameObject replacementRoot = new GameObject("Free Pack - Rocks Stylized - Unified Replacements");
            freePackInstanceCount = 0;
            for (int anchorIndex = 0; anchorIndex < selectedAnchors.Count; anchorIndex++)
            {
                RockAnchor anchor = selectedAnchors[anchorIndex];
                float footprint = Mathf.Max(anchor.Size.x, anchor.Size.z);
                int pieces = footprint >= 28f ? 2 : 1;
                float targetHeight = Mathf.Clamp(anchor.Size.y * 0.72f, 3.4f, 21f);
                for (int piece = 0; piece < pieces; piece++)
                {
                    float t = pieces == 1 ? 0f : piece / (float)(pieces - 1) - 0.5f;
                    Vector3 right = anchor.Rotation * Vector3.right;
                    Vector3 forward = anchor.Rotation * Vector3.forward;
                    Vector3 position = anchor.Center
                        + right * (t * Mathf.Min(footprint * 0.42f, 13f))
                        + forward * ((piece % 2 == 0 ? -1f : 1f) * Mathf.Min(footprint * 0.08f, 3.5f));
                    position.y = canvas.SampleHeightWorld(position);
                    string prefabPath = SelectFreePackPrefab(anchor, anchorIndex, piece);
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    Require(prefab != null, "Missing Free Pack rock prefab: " + prefabPath);
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    Require(instance != null, "Could not instantiate Free Pack rock: " + prefabPath);
                    instance.transform.SetParent(replacementRoot.transform, true);
                    instance.name = $"{prefab.name} - Unified Replacement {anchorIndex + 1:000}-{piece + 1}";
                    instance.transform.position = position;
                    instance.transform.rotation = Quaternion.Euler(
                        0f,
                        anchor.Rotation.eulerAngles.y + piece * 37f + (anchorIndex % 5) * 11f,
                        0f);
                    float pieceHeight = targetHeight * (piece == 0 ? 1f : 0.76f);
                    ScaleAndBury(instance, pieceHeight, position.y, piece == 0 ? 0.22f : 0.27f);
                    ApplySharedMaterial(instance, material);
                    freePackInstanceCount++;
                }
            }

            return sourceRoots.Count;
        }

        private static string SelectFreePackPrefab(RockAnchor anchor, int anchorIndex, int piece)
        {
            float footprint = Mathf.Max(anchor.Size.x, anchor.Size.z);
            if (footprint > anchor.Size.y * 1.28f || footprint >= 22f)
            {
                return WideRocks[(anchorIndex + piece) % WideRocks.Length];
            }

            return MediumRocks[(anchorIndex + piece) % MediumRocks.Length];
        }

        private static bool IsNonFreeRockPath(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("PolyOne/Rocks Stylized"))
            {
                return false;
            }

            return path.Contains("Blocky Cliff Formations")
                || path.Contains("RockFREE")
                || (path.Contains("A_piece_of_nature")
                    && Path.GetFileNameWithoutExtension(path).StartsWith("Rock", StringComparison.OrdinalIgnoreCase));
        }

        private static Material[] GetConeSurfaceMaterials()
        {
            return new[]
            {
                GetConeFlatMaterial("Cone Volcano 01 Base Warm Brown", new Color(0.52f, 0.41f, 0.31f, 1f)),
                GetConeFlatMaterial("Cone Volcano 02 Mid Gray Brown", new Color(0.46f, 0.42f, 0.36f, 1f)),
                GetConeFlatMaterial("Cone Volcano 03 Upper Ash Gray", new Color(0.39f, 0.40f, 0.38f, 1f)),
                GetConeFlatMaterial("Cone Volcano 04 Summit Charcoal", new Color(0.32f, 0.335f, 0.35f, 1f))
            };
        }

        private static Material GetFinalSurfaceBlendMaterial()
        {
            EnsureMaterialFolder();
            const string name = "Final Volcano Smooth Four Color Blend";
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(FinalSurfaceShaderName);
            Require(shader != null, "The final four-color volcano blend shader is missing.");
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

            material.SetColor("_BaseColor", new Color(0.52f, 0.41f, 0.31f, 1f));
            material.SetColor("_MiddleColor", new Color(0.46f, 0.42f, 0.36f, 1f));
            material.SetColor("_UpperColor", new Color(0.39f, 0.40f, 0.38f, 1f));
            material.SetColor("_SummitColor", new Color(0.32f, 0.335f, 0.35f, 1f));
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetConeFlatMaterial(string name, Color color)
        {
            EnsureMaterialFolder();
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("This Will Float/HQ Volcano Vertex Shade")
                ?? Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "No compatible shader was found for cone volcano color blocking.");
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

            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.02f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.02f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetCraterMaterial()
        {
            EnsureMaterialFolder();
            const string name = "VRST Crater Only Natural Readability";
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(CraterShaderName);
            Require(shader != null, "The crater-only shader is missing.");
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

            material.SetColor("_FloorColor", new Color(0.22f, 0.19f, 0.17f, 1f));
            material.SetColor("_WallColor", new Color(0.36f, 0.34f, 0.31f, 1f));
            material.SetColor("_RimColor", new Color(0.30f, 0.32f, 0.34f, 1f));
            material.SetFloat("_Smoothness", 0.02f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetUnifiedFreePackRockMaterial()
        {
            EnsureMaterialFolder();
            const string name = "VRST Free Pack Rocks Unified Low Poly";
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
            Require(shader != null, "No compatible shader was found for the Free Pack rocks.");
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

            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(RockTexturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
            Color tint = new Color(0.60f, 0.565f, 0.515f, 1f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.035f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.035f);
            material.DisableKeyword("_NORMALMAP");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static TerrainEditStats ValidateLocalTerrainEdit(
            EditableIslandMeshCanvas canvas,
            float[] beforeHeights,
            float[] afterHeights,
            int[] beforePaint,
            int[] afterPaint)
        {
            Require(beforeHeights.Length == afterHeights.Length, "Terrain vertex count changed.");
            Require(beforePaint.Length == afterPaint.Length, "Terrain paint count changed.");
            int resolution = Mathf.RoundToInt(Mathf.Sqrt(afterHeights.Length));
            float spacing = canvas.Size / (resolution - 1);
            float half = canvas.Size * 0.5f;
            int changed = 0;
            float maxRaise = 0f;
            float maxAbsoluteChange = 0f;
            float beforeMax = float.NegativeInfinity;
            float afterMax = float.NegativeInfinity;
            for (int i = 0; i < afterHeights.Length; i++)
            {
                float before = beforeHeights[i];
                float after = afterHeights[i];
                beforeMax = Mathf.Max(beforeMax, before);
                afterMax = Mathf.Max(afterMax, after);
                float delta = after - before;
                if (Mathf.Abs(delta) <= 0.0001f)
                {
                    continue;
                }

                int x = i % resolution;
                int z = i / resolution;
                Vector2 point = new Vector2(-half + x * spacing, -half + z * spacing);
                Require(Vector2.Distance(point, VolcanoCenter) <= 322f,
                    $"Terrain changed outside the volcano redesign radius at vertex {i}.");
                changed++;
                maxRaise = Mathf.Max(maxRaise, delta);
                maxAbsoluteChange = Mathf.Max(maxAbsoluteChange, Mathf.Abs(delta));
            }

            for (int i = 0; i < beforePaint.Length; i++)
            {
                Require(beforePaint[i] == afterPaint[i], $"Terrain paint changed at quad {i}.");
            }

            Require(changed > 3000, "Too few terrain vertices changed for the final volcano polish.");
            Require(maxRaise >= 10f && maxRaise <= 16.3f,
                $"The summit/ridge raise is outside the intended range: {maxRaise:F2}.");
            Require(maxAbsoluteChange <= 16.3f,
                $"A volcano vertex changed too aggressively: {maxAbsoluteChange:F2}.");
            float heightRatio = afterMax / Mathf.Max(0.001f, beforeMax);
            Require(heightRatio >= 1.050f && heightRatio <= 1.081f,
                $"The localized summit height ratio is outside the requested 5-8% range: {heightRatio:F3}.");
            return new TerrainEditStats(changed, maxRaise, beforeMax, afterMax);
        }

        private static void ValidateRockReplacement(
            int sourceRootCount,
            int selectedAnchorCount,
            int freePackInstanceCount)
        {
            Require(sourceRootCount > 0, "No non-Free-Pack rock assets were found to replace.");
            Require(selectedAnchorCount > 0 && selectedAnchorCount <= 64,
                "The curated Free Pack rock anchor count is invalid.");
            Require(freePackInstanceCount >= selectedAnchorCount,
                "Not every selected rock anchor received a Free Pack replacement.");
            Transform[] transforms = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (Transform transform in transforms)
            {
                GameObject root = PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject);
                if (root == null)
                {
                    continue;
                }

                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                Require(!IsNonFreeRockPath(path), "An active non-Free-Pack rock remains: " + path);
            }
        }

        private static void ScaleAndBury(GameObject instance, float targetHeight, float ground, float burial)
        {
            Require(TryCalculateBounds(instance, out Bounds bounds) && bounds.size.y > 0.001f,
                "The Free Pack rock has no renderer bounds: " + instance.name);
            instance.transform.localScale *= targetHeight / bounds.size.y;
            Require(TryCalculateBounds(instance, out bounds), "The scaled Free Pack rock lost its bounds.");
            float targetBottom = ground - bounds.size.y * burial;
            instance.transform.position += Vector3.up * (targetBottom - bounds.min.y);
        }

        private static bool TryCalculateBounds(GameObject instance, out Bounds bounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        private static void ApplySharedMaterial(GameObject instance, Material material)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] replacements = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < replacements.Length; i++) replacements[i] = material;
                renderer.sharedMaterials = replacements;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void EnsureMaterialFolder()
        {
            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project", "GeneratedMaterials");
            }
        }

        private static void CaptureFront(EditableIslandMeshCanvas canvas, string outputPath)
        {
            float angle = -90f * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                VolcanoCenter.x + Mathf.Cos(angle) * 620f,
                canvas.WaterLevel + 35f,
                VolcanoCenter.y + Mathf.Sin(angle) * 620f);
            Vector3 target = new Vector3(VolcanoCenter.x, canvas.WaterLevel + 75f, VolcanoCenter.y);
            CapturePerspective(outputPath, position, target, 44f);
        }

        private static void CaptureCraterHigh(string outputPath)
        {
            CapturePerspective(
                outputPath,
                new Vector3(VolcanoCenter.x - 390f, 500f, VolcanoCenter.y - 430f),
                new Vector3(CraterCenter.x, 136f, CraterCenter.y),
                40f);
        }

        private static void CaptureSide(EditableIslandMeshCanvas canvas, string outputPath)
        {
            float angle = 180f * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                VolcanoCenter.x + Mathf.Cos(angle) * 720f,
                canvas.WaterLevel + 85f,
                VolcanoCenter.y + Mathf.Sin(angle) * 720f);
            Vector3 target = new Vector3(VolcanoCenter.x, canvas.WaterLevel + 100f, VolcanoCenter.y);
            CapturePerspective(outputPath, position, target, 44f);
        }

        private static void CaptureFar(EditableIslandMeshCanvas canvas, string outputPath)
        {
            float angle = -101f * Mathf.Deg2Rad;
            Vector3 position = new Vector3(
                VolcanoCenter.x + Mathf.Cos(angle) * 930f,
                canvas.WaterLevel + 48f,
                VolcanoCenter.y + Mathf.Sin(angle) * 930f);
            Vector3 target = new Vector3(VolcanoCenter.x, canvas.WaterLevel + 104f, VolcanoCenter.y);
            CapturePerspective(outputPath, position, target, 49f);
        }

        private static void CapturePerspective(
            string outputPath,
            Vector3 position,
            Vector3 target,
            float fieldOfView)
        {
            GameObject cameraObject = new GameObject("Temporary Crater Tightening Capture Camera");
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
                Debug.LogError("CONE VOLCANO REDESIGN SCENE NOT FOUND: " + TestScenePath);
                return;
            }

            EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
            GameObject crater = FindSceneObject(CraterObjectRefined);
            Selection.activeGameObject = crater;
            if (frame && crater != null && SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.FrameSelected();
            }
        }

        private static GameObject FindSceneObject(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name == name) return candidate.gameObject;
                }
            }

            return null;
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
