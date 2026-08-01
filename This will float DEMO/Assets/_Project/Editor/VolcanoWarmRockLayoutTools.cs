using System;
using System.IO;
using System.Linq;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class VolcanoWarmRockLayoutTools
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string EnvironmentScenePath = "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string SourceLockedPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_LOCKED_BEFORE_WARM_ROCK_LAYOUT.unity";
        private const string EnvironmentLockedPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_LOCKED_BEFORE_WARM_ROCK_LAYOUT.unity";
        private const string SourceClusterCorrectionLockedPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_LOCKED_BEFORE_ROCK_CLUSTER_CORRECTION.unity";
        private const string EnvironmentClusterCorrectionLockedPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_LOCKED_BEFORE_ROCK_CLUSTER_CORRECTION.unity";
        private const string SurfaceObjectName = "Stylized Volcano - Earth Rock And Charcoal Bands";
        private const string CraterObjectName = "Visible Caldera Walls And Floor - Crater Only Refinement";
        private const string RockRootName = "LOCKED CRATER - STYLIZED VOLCANO ROCK DRESSING";
        private const string LegacyUnifiedRootName = "Free Pack - Rocks Stylized - Unified Replacements";
        private const string PrefabFolder = "Assets/PolyOne/Rocks Stylized/Prefabs/";
        private const string ShaderName = "This Will Float/Warm Volcanic Rocks Stylized";
        private const string MaterialPath =
            "Assets/_Project/GeneratedMaterials/Final Rocks Stylized Warm Volcanic Matte.mat";

        private static readonly Vector2 VolcanoCenter = new Vector2(35f, 75f);
        private static readonly Vector2 CraterCenter = new Vector2(28f, 80f);

        private enum RockSize
        {
            Small,
            Medium,
            Large
        }

        private readonly struct Piece
        {
            public readonly string Prefab;
            public readonly Vector2 Offset;
            public readonly float Height;
            public readonly float Yaw;
            public readonly float TiltX;
            public readonly float TiltZ;
            public readonly float Burial;
            public readonly RockSize Size;

            public Piece(
                string prefab,
                RockSize size,
                float x,
                float z,
                float height,
                float yaw,
                float tiltX,
                float tiltZ,
                float burial)
            {
                Prefab = prefab;
                Size = size;
                Offset = new Vector2(x, z);
                Height = height;
                Yaw = yaw;
                TiltX = tiltX;
                TiltZ = tiltZ;
                Burial = burial;
            }
        }

        private readonly struct Cluster
        {
            public readonly string Name;
            public readonly string Zone;
            public readonly Vector2 Center;
            public readonly Piece[] Pieces;

            public Cluster(string name, string zone, Vector2 center, params Piece[] pieces)
            {
                Name = name;
                Zone = zone;
                Center = center;
                Pieces = pieces;
            }
        }

        [MenuItem("This Will Float DEMO/6. BUILD WARM VOLCANIC ROCK LAYOUT")]
        public static void BuildFromMenu() => Build(true);

        [MenuItem("This Will Float DEMO/6. OPEN WARM VOLCANIC ROCK LAYOUT")]
        public static void OpenFromMenu() => EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);

        [MenuItem("This Will Float DEMO/6. CAPTURE WARM VOLCANIC ROCK LAYOUT")]
        public static void CaptureFromMenu() => CaptureBatch();

        [MenuItem("This Will Float DEMO/6. AUDIT FREE PACK ROCK POSITIONS")]
        public static void AuditFromMenu()
        {
            EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            GameObject root = GameObject.Find("Free Pack - Rocks Stylized - Unified Replacements");
            Require(root != null, "The pre-existing unified Free Pack rock root is missing.");
            foreach (Transform child in root.transform.Cast<Transform>())
            {
                if (!TryBounds(child.gameObject, out Bounds bounds)) continue;
                float radial = Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z), VolcanoCenter);
                Debug.Log(string.Format(
                    "VOLCANO_ROCK_AUDIT|name={0}|position={1:F1},{2:F1},{3:F1}|radial={4:F1}|materials={5}",
                    child.name,
                    bounds.center.x,
                    bounds.center.y,
                    bounds.center.z,
                    radial,
                    string.Join(",", child.GetComponentsInChildren<Renderer>(true)
                        .SelectMany(r => r.sharedMaterials)
                        .Where(m => m != null)
                        .Select(m => m.name)
                        .Distinct())));
            }
        }

        public static void BuildBatch() => Build(false);

        public static void CaptureBatch()
        {
            string folder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "VolcanoRockClusterCorrectionCaptures");
            Directory.CreateDirectory(folder);

            Scene beforeScene = EditorSceneManager.OpenScene(
                EnvironmentClusterCorrectionLockedPath, OpenSceneMode.Single);
            EditableIslandMeshCanvas beforeCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(beforeScene.IsValid() && beforeCanvas != null, "The current-rock comparison scene is missing.");
            CapturePlayer(beforeCanvas, Path.Combine(folder, "01_before_player.png"));
            CaptureCrater45(beforeCanvas, Path.Combine(folder, "03_before_top45.png"));

            Scene revisedScene = EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(revisedScene.IsValid() && canvas != null, "The revised rock scene is missing.");
            CapturePlayer(canvas, Path.Combine(folder, "02_after_player.png"));
            CaptureCrater45(canvas, Path.Combine(folder, "04_after_top45.png"));
            CaptureCraterClose(canvas, Path.Combine(folder, "05_crater_cluster_close.png"));
            CaptureBase(canvas, Path.Combine(folder, "06_base_cluster_close.png"));
            CaptureEmbeddedCloseup(canvas, Path.Combine(folder, "07_embedded_side_close.png"));
            Debug.Log("VOLCANO_ROCK_CLUSTER_CORRECTION_CAPTURES_COMPLETE: " + folder);
        }

        private static void Build(bool showDialog)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) != null,
                "The approved volcano source scene is missing.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScenePath) != null,
                "The approved environment scene is missing.");

            Scene environment = EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            string environmentSignature = CaptureEnvironmentSignature();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentClusterCorrectionLockedPath) == null)
            {
                Require(EditorSceneManager.SaveScene(environment, EnvironmentClusterCorrectionLockedPath, true),
                    "Could not lock the approved environment before correcting rock clusters.");
            }

            Scene source;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceClusterCorrectionLockedPath) == null)
            {
                source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
                Require(EditorSceneManager.SaveScene(source, SourceClusterCorrectionLockedPath, true),
                    "Could not lock the approved volcano before correcting rock clusters.");
            }
            else
            {
                // Every correction rebuild starts from the approved warm-rock version so the
                // before/after comparison stays stable and test iterations cannot accumulate.
                source = EditorSceneManager.OpenScene(SourceClusterCorrectionLockedPath, OpenSceneMode.Single);
                Require(EditorSceneManager.SaveScene(source, SourceScenePath, true),
                    "Could not restore the approved pre-cluster-correction source snapshot.");
                source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            }

            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            GameObject surface = GameObject.Find(SurfaceObjectName);
            GameObject crater = GameObject.Find(CraterObjectName);
            GameObject oldRockRoot = GameObject.Find(RockRootName);
            GameObject legacyUnifiedRoot = GameObject.Find(LegacyUnifiedRootName);
            Require(canvas != null && surface != null && crater != null && oldRockRoot != null,
                "The approved volcano, crater, surface or current rock dressing is incomplete.");
            Require(legacyUnifiedRoot != null, "The pre-existing unified Free Pack rock root is missing.");

            MeshFilter surfaceFilter = surface.GetComponent<MeshFilter>();
            MeshRenderer surfaceRenderer = surface.GetComponent<MeshRenderer>();
            MeshFilter craterFilter = crater.GetComponent<MeshFilter>();
            Require(surfaceFilter != null && surfaceFilter.sharedMesh != null && surfaceRenderer != null,
                "The approved volcano surface is incomplete.");
            Require(craterFilter != null && craterFilter.sharedMesh != null,
                "The approved crater geometry is incomplete.");

            canvas.CopyState(out float[] beforeHeights, out int[] beforePaint);
            Vector3[] beforeSurfaceVertices = surfaceFilter.sharedMesh.vertices.ToArray();
            Vector3[] beforeCraterVertices = craterFilter.sharedMesh.vertices.ToArray();
            Material approvedVolcanoMaterial = surfaceRenderer.sharedMaterial;
            string approvedVolcanoMaterialPath = AssetDatabase.GetAssetPath(approvedVolcanoMaterial);
            int replacedDressingCount = CountRocks(oldRockRoot);

            Object.DestroyImmediate(oldRockRoot);
            int removedLegacyVolcanoRocks = RemoveLegacyRocksInsideVolcanoZone(legacyUnifiedRoot);
            Material warmRockMaterial = GetOrCreateRockMaterial();
            using SurfaceProbe surfaceProbe = new SurfaceProbe(surfaceFilter, craterFilter, canvas);
            BuildStats stats = BuildClusters(surfaceProbe, warmRockMaterial);
            GameObject revisedRoot = GameObject.Find(RockRootName);
            Require(revisedRoot != null, "The revised rock root was not created.");
            string revisedRockSignature = CaptureRockSignature(revisedRoot);

            canvas.CopyState(out float[] afterHeights, out int[] afterPaint);
            Require(beforeHeights.SequenceEqual(afterHeights), "Terrain heights changed during the rock-only edit.");
            Require(beforePaint.SequenceEqual(afterPaint), "Terrain paint changed during the rock-only edit.");
            Require(beforeSurfaceVertices.SequenceEqual(surfaceFilter.sharedMesh.vertices),
                "The approved volcano mesh changed during the rock-only edit.");
            Require(beforeCraterVertices.SequenceEqual(craterFilter.sharedMesh.vertices),
                "The approved crater changed during the rock-only edit.");
            Require(surfaceRenderer.sharedMaterial == approvedVolcanoMaterial &&
                    AssetDatabase.GetAssetPath(surfaceRenderer.sharedMaterial) == approvedVolcanoMaterialPath,
                "The approved volcano surface material changed.");

            EditorSceneManager.MarkSceneDirty(source);
            Require(EditorSceneManager.SaveScene(source, SourceScenePath), "Could not save the revised rock source scene.");
            AssetDatabase.SaveAssets();
            EnvironmentMoodTestTools.BuildTestSceneBatch();

            EditableIslandMeshCanvas finalCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            MeshFilter finalSurface = GameObject.Find(SurfaceObjectName).GetComponent<MeshFilter>();
            MeshRenderer finalSurfaceRenderer = GameObject.Find(SurfaceObjectName).GetComponent<MeshRenderer>();
            MeshFilter finalCrater = GameObject.Find(CraterObjectName).GetComponent<MeshFilter>();
            GameObject finalRockRoot = GameObject.Find(RockRootName);
            Require(finalCanvas != null && finalRockRoot != null, "The rebuilt environment lost the revised rocks.");
            finalCanvas.CopyState(out float[] finalHeights, out int[] finalPaint);
            Require(beforeHeights.SequenceEqual(finalHeights) && beforePaint.SequenceEqual(finalPaint),
                "Environment rebuild changed island terrain data.");
            Require(beforeSurfaceVertices.SequenceEqual(finalSurface.sharedMesh.vertices),
                "Environment rebuild changed the volcano mesh.");
            Require(beforeCraterVertices.SequenceEqual(finalCrater.sharedMesh.vertices),
                "Environment rebuild changed the crater mesh.");
            Require(finalSurfaceRenderer.sharedMaterial == approvedVolcanoMaterial,
                "Environment rebuild changed the approved volcano material.");
            Require(revisedRockSignature == CaptureRockSignature(finalRockRoot),
                "Environment rebuild changed the revised rock transforms or materials.");
            Require(environmentSignature == CaptureEnvironmentSignature(),
                "Sky, water, lighting or ambient settings changed during the rock-only edit.");

            Debug.Log(
                "VOLCANO_ROCK_CLUSTER_CORRECTION_COMPLETE" +
                $"|removed={replacedDressingCount}" +
                $"|replacedDressing={replacedDressingCount}|removedLegacyVolcanoZone={removedLegacyVolcanoRocks}" +
                $"|added={stats.Total}|finalTotal={stats.Total}|recolored={stats.Total}" +
                $"|small={stats.Small}|medium={stats.Medium}|large={stats.Large}" +
                "|clusters=12|craterClusters=3|upperOutcrops=3|baseClusters=6" +
                "|burial=35-55pct|floating=0|dottedLines=0|isolatedTrailMarkers=0|middleSlope=open" +
                "|material=Final Rocks Stylized Warm Volcanic Matte" +
                "|base=#514942|shadow=#35322F|highlight=#746A60|metallic=0.00|smoothness=0.02" +
                "|prefabs=SM_Rocks_01-11|pack=PolyOne Free Pack Rocks Stylized" +
                "|geometryChange=none|craterChange=none|volcanoMaterialChange=none" +
                "|skyChange=none|waterChange=none|islandChange=none|treeChange=none|lightingChange=none|gameplayChange=none");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The approved volcano and environment remain locked. Evenly spaced markers were replaced by three crater outcrops, three short irregular upper outcrops and six dense base debris clusters.",
                    "OK");
            }
        }

        private static Cluster[] CreateClusters()
        {
            return new[]
            {
                new Cluster("Crater 01 - Broken South West Rim", "Crater", new Vector2(-2f, 53f),
                    P("SM_Rocks_03", RockSize.Large, 0f, 0f, 16.5f, 18f, 5f, -4f, 0.43f),
                    P("SM_Rocks_07", RockSize.Medium, -3.8f, 1.3f, 10.8f, 79f, -5f, 3f, 0.45f),
                    P("SM_Rocks_05", RockSize.Medium, 2.7f, -2.4f, 9.6f, 143f, 4f, 5f, 0.48f),
                    P("SM_Rocks_02", RockSize.Medium, 4.1f, 2.8f, 8.8f, 224f, 5f, -4f, 0.50f),
                    P("SM_Rocks_11", RockSize.Small, -2.0f, -3.5f, 5.8f, 291f, -4f, -3f, 0.53f),
                    P("SM_Rocks_09", RockSize.Small, 1.6f, 3.7f, 6.2f, 337f, 3f, 5f, 0.47f),
                    P("SM_Rocks_06", RockSize.Small, -5.0f, -1.5f, 5.1f, 104f, 4f, -5f, 0.55f),
                    P("SM_Rocks_10", RockSize.Small, 5.4f, -0.5f, 4.8f, 257f, -5f, 3f, 0.52f)),
                new Cluster("Crater 02 - Raised North East Rim", "Crater", new Vector2(56f, 108f),
                    P("SM_Rocks_01", RockSize.Large, 0f, 0f, 15.8f, 43f, -4f, 5f, 0.42f),
                    P("SM_Rocks_08", RockSize.Medium, 3.6f, 0.9f, 10.4f, 116f, 5f, -3f, 0.46f),
                    P("SM_Rocks_04", RockSize.Medium, -2.9f, 2.1f, 9.9f, 181f, -5f, -4f, 0.49f),
                    P("SM_Rocks_07", RockSize.Medium, 1.2f, -3.0f, 8.7f, 252f, 4f, 5f, 0.51f),
                    P("SM_Rocks_06", RockSize.Small, -4.8f, -0.8f, 5.9f, 315f, -3f, 4f, 0.54f),
                    P("SM_Rocks_10", RockSize.Small, 4.9f, -2.0f, 5.2f, 25f, 5f, -5f, 0.50f),
                    P("SM_Rocks_09", RockSize.Small, -1.0f, -4.5f, 6.1f, 149f, -4f, 3f, 0.55f),
                    P("SM_Rocks_11", RockSize.Small, 1.8f, 3.9f, 4.9f, 283f, 3f, -4f, 0.53f)),
                new Cluster("Crater 03 - East Rim Fragment", "Crater", new Vector2(63f, 63f),
                    P("SM_Rocks_03", RockSize.Large, 0f, 0f, 15.2f, 61f, 4f, 5f, 0.44f),
                    P("SM_Rocks_02", RockSize.Medium, 3.2f, -1.0f, 9.8f, 128f, -4f, 3f, 0.47f),
                    P("SM_Rocks_05", RockSize.Medium, -2.7f, 1.8f, 9.0f, 204f, 5f, -3f, 0.50f),
                    P("SM_Rocks_08", RockSize.Medium, 1.1f, 3.3f, 8.5f, 276f, -3f, -5f, 0.52f),
                    P("SM_Rocks_11", RockSize.Small, -4.8f, -1.1f, 5.4f, 336f, 3f, 4f, 0.54f),
                    P("SM_Rocks_06", RockSize.Small, 4.7f, 1.9f, 5.8f, 32f, -5f, 3f, 0.48f),
                    P("SM_Rocks_10", RockSize.Small, -1.1f, -3.8f, 4.9f, 157f, 4f, -4f, 0.55f),
                    P("SM_Rocks_09", RockSize.Small, 3.0f, 3.9f, 5.6f, 245f, -4f, 5f, 0.51f)),

                new Cluster("Upper 01 - South West Broken Outcrop", "Upper", new Vector2(-36f, -8f),
                    P("SM_Rocks_04", RockSize.Medium, 0f, 0f, 12.6f, 21f, 5f, -3f, 0.43f),
                    P("SM_Rocks_09", RockSize.Small, 3.0f, -1.8f, 6.4f, 96f, -4f, 4f, 0.52f),
                    P("SM_Rocks_07", RockSize.Medium, -3.5f, -3.2f, 10.8f, 166f, 4f, 5f, 0.47f),
                    P("SM_Rocks_11", RockSize.Small, 2.0f, -5.0f, 5.7f, 247f, 5f, -4f, 0.55f),
                    P("SM_Rocks_06", RockSize.Small, -1.5f, -6.5f, 5.2f, 319f, -4f, -5f, 0.53f)),
                new Cluster("Upper 02 - South East Broken Outcrop", "Upper", new Vector2(100f, 15f),
                    P("SM_Rocks_05", RockSize.Medium, 0f, 0f, 12.2f, 38f, -4f, 5f, 0.44f),
                    P("SM_Rocks_10", RockSize.Small, -3.1f, -1.5f, 5.9f, 111f, 4f, -5f, 0.51f),
                    P("SM_Rocks_08", RockSize.Medium, 3.2f, -2.0f, 10.6f, 183f, 5f, 3f, 0.48f),
                    P("SM_Rocks_09", RockSize.Small, -1.0f, -4.8f, 6.2f, 259f, -5f, 4f, 0.54f),
                    P("SM_Rocks_11", RockSize.Small, 4.8f, -4.5f, 5.3f, 332f, 3f, -4f, 0.55f)),
                new Cluster("Upper 03 - North East Broken Outcrop", "Upper", new Vector2(104f, 140f),
                    P("SM_Rocks_02", RockSize.Medium, 0f, 0f, 11.8f, 29f, 4f, -5f, 0.45f),
                    P("SM_Rocks_06", RockSize.Small, 2.3f, 2.3f, 6.1f, 103f, -5f, 4f, 0.52f),
                    P("SM_Rocks_07", RockSize.Medium, 4.2f, -1.0f, 10.4f, 176f, 5f, 3f, 0.49f),
                    P("SM_Rocks_10", RockSize.Small, 1.0f, -3.5f, 5.5f, 251f, -4f, -3f, 0.55f),
                    P("SM_Rocks_09", RockSize.Small, 6.5f, 2.5f, 6.0f, 326f, 3f, 5f, 0.53f)),

                new Cluster("Base 01 - South West Debris", "Base", new Vector2(-190f, -112f),
                    P("SM_Rocks_01", RockSize.Large, 0f, 0f, 22.0f, 19f, 4f, -3f, 0.42f),
                    P("SM_Rocks_03", RockSize.Large, -5.4f, -1.8f, 18.8f, 151f, 5f, 3f, 0.45f),
                    P("SM_Rocks_04", RockSize.Medium, 5.5f, 1.5f, 12.5f, 82f, -5f, 5f, 0.47f),
                    P("SM_Rocks_08", RockSize.Medium, 2.5f, -4.6f, 10.7f, 219f, -4f, -5f, 0.50f),
                    P("SM_Rocks_05", RockSize.Medium, -2.8f, 4.5f, 9.8f, 276f, 4f, 3f, 0.52f),
                    P("SM_Rocks_11", RockSize.Small, -4.1f, 3.2f, 6.0f, 304f, 3f, 4f, 0.54f),
                    P("SM_Rocks_06", RockSize.Small, 6.7f, -3.3f, 5.5f, 341f, -5f, -3f, 0.55f),
                    P("SM_Rocks_09", RockSize.Small, 1.1f, 5.6f, 6.4f, 119f, 4f, -5f, 0.51f),
                    P("SM_Rocks_10", RockSize.Small, -7.0f, -3.8f, 5.1f, 237f, -4f, 5f, 0.55f)),
                new Cluster("Base 02 - South Beach Transition", "Base", new Vector2(10f, -225f),
                    P("SM_Rocks_03", RockSize.Large, 0f, 0f, 18.5f, 42f, -4f, 5f, 0.43f),
                    P("SM_Rocks_02", RockSize.Medium, 5.2f, 1.1f, 12.0f, 103f, 5f, -3f, 0.48f),
                    P("SM_Rocks_05", RockSize.Medium, -4.8f, -1.6f, 10.8f, 169f, -5f, -4f, 0.51f),
                    P("SM_Rocks_07", RockSize.Medium, 2.1f, -4.7f, 9.6f, 236f, 4f, 5f, 0.52f),
                    P("SM_Rocks_10", RockSize.Small, -3.6f, 3.8f, 5.2f, 299f, -3f, 4f, 0.55f),
                    P("SM_Rocks_09", RockSize.Small, 6.1f, -3.1f, 5.8f, 352f, 5f, -5f, 0.53f),
                    P("SM_Rocks_11", RockSize.Small, -6.3f, 2.1f, 4.9f, 127f, -4f, 3f, 0.55f)),
                new Cluster("Base 03 - South East Debris", "Base", new Vector2(218f, -115f),
                    P("SM_Rocks_01", RockSize.Large, 0f, 0f, 22.5f, 31f, 5f, 3f, 0.42f),
                    P("SM_Rocks_03", RockSize.Large, 5.3f, -1.7f, 18.7f, 162f, 4f, -4f, 0.46f),
                    P("SM_Rocks_04", RockSize.Medium, -5.7f, 1.4f, 12.1f, 95f, -4f, 5f, 0.49f),
                    P("SM_Rocks_08", RockSize.Medium, -2.2f, -4.8f, 10.2f, 230f, 5f, 4f, 0.52f),
                    P("SM_Rocks_06", RockSize.Small, 3.8f, 3.6f, 5.8f, 294f, -5f, 3f, 0.55f),
                    P("SM_Rocks_11", RockSize.Small, -6.0f, -3.0f, 5.3f, 342f, 3f, -5f, 0.53f),
                    P("SM_Rocks_09", RockSize.Small, 6.7f, 2.0f, 6.2f, 114f, -4f, 4f, 0.51f),
                    P("SM_Rocks_10", RockSize.Small, 1.2f, -5.9f, 4.9f, 263f, 5f, -3f, 0.55f)),
                new Cluster("Base 04 - East Grass Transition", "Base", new Vector2(325f, 95f),
                    P("SM_Rocks_03", RockSize.Large, 0f, 0f, 20.8f, 14f, -5f, 4f, 0.44f),
                    P("SM_Rocks_05", RockSize.Medium, 5.2f, 1.4f, 11.5f, 77f, 4f, -3f, 0.48f),
                    P("SM_Rocks_07", RockSize.Medium, 1.9f, -4.4f, 9.8f, 214f, -4f, -5f, 0.52f),
                    P("SM_Rocks_02", RockSize.Medium, -4.6f, -1.7f, 10.5f, 147f, 5f, 5f, 0.50f),
                    P("SM_Rocks_09", RockSize.Small, -3.9f, 3.6f, 5.9f, 279f, 3f, 4f, 0.54f),
                    P("SM_Rocks_10", RockSize.Small, 6.1f, -2.9f, 5.0f, 337f, -5f, 3f, 0.55f),
                    P("SM_Rocks_11", RockSize.Small, 2.5f, 4.5f, 5.3f, 112f, 4f, -4f, 0.52f)),
                new Cluster("Base 05 - North West Grass Transition", "Base", new Vector2(-235f, 165f),
                    P("SM_Rocks_01", RockSize.Large, 0f, 0f, 21.5f, 48f, 4f, -5f, 0.43f),
                    P("SM_Rocks_03", RockSize.Large, 5.1f, -1.5f, 18.1f, 182f, 5f, 4f, 0.47f),
                    P("SM_Rocks_02", RockSize.Medium, -5.3f, 1.6f, 11.7f, 114f, -5f, 3f, 0.49f),
                    P("SM_Rocks_08", RockSize.Medium, -2.1f, -4.5f, 10.1f, 247f, -4f, -3f, 0.52f),
                    P("SM_Rocks_11", RockSize.Small, 3.9f, 3.8f, 5.6f, 309f, 3f, 5f, 0.54f),
                    P("SM_Rocks_06", RockSize.Small, -6.0f, -2.9f, 5.8f, 355f, -5f, -4f, 0.55f),
                    P("SM_Rocks_09", RockSize.Small, 6.4f, 1.9f, 6.1f, 127f, 4f, 3f, 0.51f),
                    P("SM_Rocks_10", RockSize.Small, 1.1f, -5.5f, 4.8f, 268f, -4f, 5f, 0.55f)),
                new Cluster("Base 06 - North East Low Point", "Base", new Vector2(245f, 270f),
                    P("SM_Rocks_03", RockSize.Large, 0f, 0f, 20.2f, 27f, -4f, 5f, 0.44f),
                    P("SM_Rocks_04", RockSize.Medium, -4.9f, 1.4f, 11.2f, 99f, 5f, -3f, 0.49f),
                    P("SM_Rocks_07", RockSize.Medium, 3.7f, -2.8f, 9.7f, 221f, -5f, -4f, 0.52f),
                    P("SM_Rocks_06", RockSize.Small, 4.8f, 2.7f, 5.7f, 286f, 4f, 5f, 0.54f),
                    P("SM_Rocks_11", RockSize.Small, -5.8f, -2.2f, 5.1f, 343f, -4f, 3f, 0.55f),
                    P("SM_Rocks_09", RockSize.Small, 0.9f, 4.8f, 6.0f, 136f, 3f, -5f, 0.52f))
            };
        }

        private static Piece P(
            string prefab,
            RockSize size,
            float x,
            float z,
            float height,
            float yaw,
            float tiltX,
            float tiltZ,
            float burial)
        {
            return new Piece(prefab, size, x, z, height, yaw, tiltX, tiltZ, burial);
        }

        private static BuildStats BuildClusters(SurfaceProbe surface, Material material)
        {
            Cluster[] clusters = CreateClusters();
            Require(clusters.Count(c => c.Zone == "Crater") == 3, "Crater cluster count must be three.");
            Require(clusters.Count(c => c.Zone == "Upper") == 3, "Upper outcrop line count must be three.");
            Require(clusters.Count(c => c.Zone == "Base") == 6, "Base cluster count must be six.");

            GameObject root = new GameObject(RockRootName);
            int small = 0;
            int medium = 0;
            int large = 0;
            foreach (Cluster cluster in clusters)
            {
                GameObject clusterRoot = new GameObject(cluster.Name);
                clusterRoot.transform.SetParent(root.transform, false);
                foreach (Piece piece in cluster.Pieces)
                {
                    string path = PrefabFolder + piece.Prefab + ".prefab";
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Require(prefab != null, "Missing real Free Pack - Rocks Stylized prefab: " + path);
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    Require(instance != null, "Could not instantiate Free Pack rock: " + path);
                    instance.name = piece.Prefab + " - " + cluster.Name;
                    instance.transform.SetParent(clusterRoot.transform, true);

                    float spacing = cluster.Zone == "Base" ? 2.8f : 1f;
                    Vector3 point = new Vector3(
                        cluster.Center.x + piece.Offset.x * spacing,
                        0f,
                        cluster.Center.y + piece.Offset.y * spacing);
                    float ground = surface.SampleHeight(point);
                    Vector3 normal = surface.SampleNormal(point, piece.Size == RockSize.Large ? 5f : 3f);
                    instance.transform.position = new Vector3(point.x, ground, point.z);
                    instance.transform.rotation = Quaternion.AngleAxis(piece.Yaw, normal) *
                                                  Quaternion.FromToRotation(Vector3.up, normal);
                    instance.transform.Rotate(Vector3.right, piece.TiltX, Space.Self);
                    instance.transform.Rotate(Vector3.forward, piece.TiltZ, Space.Self);
                    ApplyMaterial(instance, material);
                    ScaleAndBury(instance, surface, piece.Height, piece.Burial);
                    ValidatePrefabAndGrounding(instance, path, surface, material, piece.Burial);

                    if (piece.Size == RockSize.Small) small++;
                    else if (piece.Size == RockSize.Medium) medium++;
                    else large++;
                }
            }

            BuildStats stats = new BuildStats(small, medium, large);
            Require(stats.Total == 84, "The corrected clustered rock count must be 84.");
            Require(small == 42 && medium == 30 && large == 12,
                "The required 50/35/15 small-medium-large balance was not preserved.");
            return stats;
        }

        private static Material GetOrCreateRockMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            Require(shader != null, "The warm stylized volcanic-rock shader is missing or failed to compile.");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "Final Rocks Stylized Warm Volcanic Matte" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", new Color(0.318f, 0.286f, 0.259f, 1f));
            material.SetColor("_ShadowColor", new Color(0.208f, 0.196f, 0.184f, 1f));
            material.SetColor("_HighlightColor", new Color(0.455f, 0.416f, 0.376f, 1f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.02f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyMaterial(GameObject instance, Material material)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
        }

        private static void ScaleAndBury(
            GameObject instance,
            SurfaceProbe surface,
            float targetHeight,
            float burial)
        {
            Require(burial >= 0.35f && burial <= 0.55f, "Rock burial is outside the approved range.");
            Require(TryBounds(instance, out Bounds initial) && initial.size.y > 0.001f,
                "Rock renderer bounds are missing.");
            instance.transform.localScale *= targetHeight / initial.size.y;
            Require(TryBounds(instance, out Bounds scaled), "Scaled rock bounds are missing.");
            float ground = surface.SampleHeight(new Vector3(scaled.center.x, 0f, scaled.center.z));
            float desiredBottom = ground - scaled.size.y * burial;
            instance.transform.position += Vector3.up * (desiredBottom - scaled.min.y);
        }

        private static void ValidatePrefabAndGrounding(
            GameObject instance,
            string expectedPrefabPath,
            SurfaceProbe surface,
            Material material,
            float expectedBurial)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            Require(source != null && AssetDatabase.GetAssetPath(source) == expectedPrefabPath,
                "A rock is not an instance of the required Free Pack prefab.");
            Require(TryBounds(instance, out Bounds bounds), "Final rock bounds are missing.");
            float ground = surface.SampleHeight(new Vector3(bounds.center.x, 0f, bounds.center.z));
            float actualBurial = (ground - bounds.min.y) / Mathf.Max(0.001f, bounds.size.y);
            Require(actualBurial >= 0.345f && actualBurial <= 0.555f,
                $"Rock burial is invalid: {instance.name} ({actualBurial:P0}).");
            Require(Mathf.Abs(actualBurial - expectedBurial) < 0.015f,
                "A rock did not retain its authored burial depth.");
            Require(instance.GetComponentsInChildren<Renderer>(true)
                    .All(r => r.sharedMaterials.All(m => m == material)),
                "A rock did not receive the shared warm volcanic material.");
        }

        private static int RemoveLegacyRocksInsideVolcanoZone(GameObject root)
        {
            int removed = 0;
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
            {
                if (!TryBounds(child.gameObject, out Bounds bounds)) continue;
                float radial = Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z), VolcanoCenter);
                if (radial > 340f) continue;
                Object.DestroyImmediate(child.gameObject);
                removed++;
            }

            Require(root.transform.Cast<Transform>().All(child =>
            {
                if (!TryBounds(child.gameObject, out Bounds bounds)) return true;
                return Vector2.Distance(new Vector2(bounds.center.x, bounds.center.z), VolcanoCenter) > 340f;
            }), "A randomly scattered legacy rock remained inside the volcano layout zone.");
            return removed;
        }

        private static bool TryBounds(GameObject instance, out Bounds bounds)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        private static int CountRocks(GameObject root)
        {
            return root.transform.Cast<Transform>().Sum(cluster => cluster.childCount);
        }

        private static string CaptureRockSignature(GameObject root)
        {
            return string.Join("|", root.GetComponentsInChildren<Transform>(true)
                .OrderBy(t => GetPath(t))
                .Select(t =>
                {
                    Renderer renderer = t.GetComponent<Renderer>();
                    string materials = renderer == null
                        ? "-"
                        : string.Join(",", renderer.sharedMaterials.Select(AssetDatabase.GetAssetPath));
                    return string.Format(
                        "{0}:{1:F4},{2:F4},{3:F4}:{4:F4},{5:F4},{6:F4},{7:F4}:{8:F4},{9:F4},{10:F4}:{11}",
                        GetPath(t),
                        t.localPosition.x, t.localPosition.y, t.localPosition.z,
                        t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w,
                        t.localScale.x, t.localScale.y, t.localScale.z,
                        materials);
                }));
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

        private static void CaptureCrater45(EditableIslandMeshCanvas canvas, string path)
        {
            Capture(
                path,
                new Vector3(CraterCenter.x - 190f, canvas.WaterLevel + 365f, CraterCenter.y - 235f),
                new Vector3(CraterCenter.x, canvas.WaterLevel + 132f, CraterCenter.y),
                48f);
        }

        private static void CaptureCraterClose(EditableIslandMeshCanvas canvas, string path)
        {
            Capture(
                path,
                new Vector3(CraterCenter.x - 122f, canvas.WaterLevel + 320f, CraterCenter.y - 155f),
                new Vector3(CraterCenter.x + 5f, canvas.WaterLevel + 148f, CraterCenter.y + 3f),
                44f);
        }

        private static void CaptureBase(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 target = new Vector3(10f, 0f, -225f);
            target.y = SampleVisibleHeight(canvas, target) + 5f;
            Vector3 position = new Vector3(82f, target.y + 58f, -318f);
            Capture(path, position, target, 52f);
        }

        private static void CaptureEmbeddedCloseup(EditableIslandMeshCanvas canvas, string path)
        {
            Vector3 target = new Vector3(10f, 0f, -225f);
            target.y = SampleVisibleHeight(canvas, target) + 4f;
            Vector3 position = new Vector3(42f, 0f, -271f);
            position.y = SampleVisibleHeight(canvas, position) + 9f;
            Capture(path, position, target, 42f);
        }

        private static float SampleVisibleHeight(EditableIslandMeshCanvas canvas, Vector3 point)
        {
            MeshFilter surface = GameObject.Find(SurfaceObjectName)?.GetComponent<MeshFilter>();
            MeshFilter crater = GameObject.Find(CraterObjectName)?.GetComponent<MeshFilter>();
            Require(surface != null && crater != null, "Visible volcano surfaces are missing during capture.");
            using SurfaceProbe probe = new SurfaceProbe(surface, crater, canvas);
            return probe.SampleHeight(point);
        }

        private static void Capture(string path, Vector3 position, Vector3 target, float fov)
        {
            GameObject cameraObject = new GameObject("Temporary Warm Volcanic Rock Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = fov;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 3500f;
            camera.transform.position = position;
            camera.transform.LookAt(target);
            RenderTexture texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 4
            };
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

        private readonly struct BuildStats
        {
            public readonly int Small;
            public readonly int Medium;
            public readonly int Large;
            public int Total => Small + Medium + Large;

            public BuildStats(int small, int medium, int large)
            {
                Small = small;
                Medium = medium;
                Large = large;
            }
        }

        private sealed class SurfaceProbe : IDisposable
        {
            private readonly EditableIslandMeshCanvas canvas;
            private readonly MeshCollider volcanoCollider;
            private readonly MeshCollider craterCollider;
            private readonly bool ownsVolcanoCollider;
            private readonly bool ownsCraterCollider;

            public SurfaceProbe(
                MeshFilter volcanoSurface,
                MeshFilter craterSurface,
                EditableIslandMeshCanvas sourceCanvas)
            {
                canvas = sourceCanvas;
                volcanoCollider = GetOrCreateProbeCollider(volcanoSurface, out bool volcanoCreated);
                craterCollider = GetOrCreateProbeCollider(craterSurface, out bool craterCreated);
                ownsVolcanoCollider = volcanoCreated;
                ownsCraterCollider = craterCreated;
                Physics.SyncTransforms();
            }

            public float SampleHeight(Vector3 point)
            {
                float height = canvas.SampleHeightWorld(point);
                Ray ray = new Ray(new Vector3(point.x, canvas.WaterLevel + 1800f, point.z), Vector3.down);
                if (volcanoCollider != null && volcanoCollider.Raycast(ray, out RaycastHit volcanoHit, 3600f))
                {
                    height = Mathf.Max(height, volcanoHit.point.y);
                }
                if (craterCollider != null && craterCollider.Raycast(ray, out RaycastHit craterHit, 3600f))
                {
                    height = Mathf.Max(height, craterHit.point.y);
                }
                return height;
            }

            public Vector3 SampleNormal(Vector3 point, float sample)
            {
                float left = SampleHeight(point + Vector3.left * sample);
                float right = SampleHeight(point + Vector3.right * sample);
                float back = SampleHeight(point + Vector3.back * sample);
                float forward = SampleHeight(point + Vector3.forward * sample);
                return new Vector3(left - right, sample * 2f, back - forward).normalized;
            }

            public void Dispose()
            {
                if (ownsVolcanoCollider && volcanoCollider != null) Object.DestroyImmediate(volcanoCollider);
                if (ownsCraterCollider && craterCollider != null) Object.DestroyImmediate(craterCollider);
            }

            private static MeshCollider GetOrCreateProbeCollider(MeshFilter filter, out bool created)
            {
                MeshCollider collider = filter.GetComponent<MeshCollider>();
                created = collider == null;
                if (created)
                {
                    collider = filter.gameObject.AddComponent<MeshCollider>();
                    collider.hideFlags = HideFlags.HideAndDontSave;
                }
                collider.sharedMesh = filter.sharedMesh;
                return collider;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
