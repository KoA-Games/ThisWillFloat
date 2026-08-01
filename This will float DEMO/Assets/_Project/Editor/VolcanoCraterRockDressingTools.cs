using System;
using System.Collections.Generic;
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
    public static class VolcanoCraterRockDressingTools
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/VolcanoFinalPolish.unity";
        private const string EnvironmentScenePath = "Assets/_Project/Scenes/EnvironmentMoodTest_SkySun.unity";
        private const string SourceLockedPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_LOCKED_CRATER_BEFORE_ROCK_DRESSING.unity";
        private const string EnvironmentLockedPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_LOCKED_CRATER_BEFORE_ROCK_DRESSING.unity";
        private const string SourceBeforeRocksPath =
            "Assets/_Project/Scenes/Backups/VolcanoFinalPolish_CRATER_CLEANUP_BEFORE_ROCKS.unity";
        private const string EnvironmentBeforeRocksPath =
            "Assets/_Project/Scenes/Backups/EnvironmentMoodTest_SkySun_CRATER_CLEANUP_BEFORE_ROCKS.unity";
        private const string CraterObjectName = "Visible Caldera Walls And Floor - Crater Only Refinement";
        private const string DressingRootName = "LOCKED CRATER - STYLIZED VOLCANO ROCK DRESSING";
        private const string CharcoalMaterialPath =
            "Assets/_Project/GeneratedMaterials/VRST Rocks Stylized Charcoal Matte.mat";
        private const string GrayBrownMaterialPath =
            "Assets/_Project/GeneratedMaterials/VRST Rocks Stylized Gray Brown Matte.mat";
        private const string PrefabFolder = "Assets/PolyOne/Rocks Stylized/Prefabs/";
        private static readonly Vector2 VolcanoCenter = new Vector2(35f, 75f);
        private static readonly Vector2 CraterCenter = new Vector2(28f, 80f);

        private readonly struct Piece
        {
            public readonly string Prefab;
            public readonly Vector2 Offset;
            public readonly float Height;
            public readonly float Yaw;
            public readonly float Burial;

            public Piece(string prefab, float x, float z, float height, float yaw, float burial)
            {
                Prefab = prefab;
                Offset = new Vector2(x, z);
                Height = height;
                Yaw = yaw;
                Burial = burial;
            }
        }

        private readonly struct Cluster
        {
            public readonly string Name;
            public readonly Vector2 Center;
            public readonly bool Charcoal;
            public readonly Piece[] Pieces;

            public Cluster(string name, Vector2 center, bool charcoal, params Piece[] pieces)
            {
                Name = name;
                Center = center;
                Charcoal = charcoal;
                Pieces = pieces;
            }
        }

        [MenuItem("This Will Float DEMO/4. BUILD LOCKED CRATER ROCK DRESSING")]
        public static void BuildFromMenu() => Build(true);

        [MenuItem("This Will Float DEMO/4. OPEN LOCKED CRATER ROCK DRESSING")]
        public static void OpenFromMenu() => EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);

        public static void BuildBatch() => Build(false);

        public static void CaptureBatch()
        {
            string folder = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "VolcanoCraterRockDressingCaptures");
            Directory.CreateDirectory(folder);

            Scene beforeScene = EditorSceneManager.OpenScene(EnvironmentBeforeRocksPath, OpenSceneMode.Single);
            EditableIslandMeshCanvas beforeCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(beforeScene.IsValid() && beforeCanvas != null, "The before-rock scene is missing.");
            string beforeTopPath = Path.Combine(folder, "01_before_rock_dressing_top45.png");
            Capture(
                beforeCanvas,
                new Vector3(CraterCenter.x - 250f, beforeCanvas.WaterLevel + 450f, CraterCenter.y - 300f),
                new Vector3(VolcanoCenter.x, beforeCanvas.WaterLevel + 112f, VolcanoCenter.y),
                50f,
                beforeTopPath);

            Scene afterScene = EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            EditableIslandMeshCanvas afterCanvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(afterScene.IsValid() && afterCanvas != null, "The dressed environment scene is missing.");
            Capture(
                afterCanvas,
                new Vector3(35f, Mathf.Max(afterCanvas.SampleHeightWorld(new Vector3(35f, 0f, -545f)) + 3.2f, afterCanvas.WaterLevel + 3.2f), -545f),
                new Vector3(28f, afterCanvas.WaterLevel + 116f, 80f),
                58f,
                Path.Combine(folder, "02_after_player_view.png"));
            string afterTopPath = Path.Combine(folder, "03_after_top45.png");
            Capture(
                afterCanvas,
                new Vector3(CraterCenter.x - 250f, afterCanvas.WaterLevel + 450f, CraterCenter.y - 300f),
                new Vector3(VolcanoCenter.x, afterCanvas.WaterLevel + 112f, VolcanoCenter.y),
                50f,
                afterTopPath);
            Texture2D beforeTop = LoadCapture(beforeTopPath);
            Texture2D afterTop = LoadCapture(afterTopPath);
            WriteComparison(
                beforeTop,
                afterTop,
                Path.Combine(folder, "04_before_after_rock_dressing_comparison.png"));
            Object.DestroyImmediate(beforeTop);
            Object.DestroyImmediate(afterTop);
            Debug.Log("VOLCANO_CRATER_ROCK_DRESSING_CAPTURES_COMPLETE: " + folder);
        }

        private static void Build(bool showDialog)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScenePath) != null, "The locked volcano scene is missing.");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentScenePath) != null, "The locked environment scene is missing.");

            Scene currentEnvironment = EditorSceneManager.OpenScene(EnvironmentScenePath, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(EnvironmentLockedPath) == null)
            {
                Require(EditorSceneManager.SaveScene(currentEnvironment, EnvironmentLockedPath, true),
                    "Could not lock the restored environment baseline.");
            }

            Scene source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceLockedPath) == null)
            {
                Require(EditorSceneManager.SaveScene(source, SourceLockedPath, true),
                    "Could not lock the restored volcano baseline.");
            }

            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(canvas != null, "The volcano terrain is missing.");
            canvas.CopyState(out float[] baselineHeights, out int[] baselinePaint);
            float baselinePeak = baselineHeights.Max();

            GameObject crater = GameObject.Find(CraterObjectName);
            Require(crater != null, "The approved crater overlay is missing.");
            CraterStats craterStats = SoftenOnlyHighFrequencyCraterOverlay(crater);

            EditorSceneManager.MarkSceneDirty(source);
            Require(EditorSceneManager.SaveScene(source, SourceScenePath), "Could not save the restrained crater cleanup.");
            Require(EditorSceneManager.SaveScene(source, SourceBeforeRocksPath, true), "Could not save the before-rock source scene.");
            EnvironmentMoodTestTools.BuildTestSceneBatch();
            Scene beforeRocksEnvironment = SceneManager.GetActiveScene();
            Require(EditorSceneManager.SaveScene(beforeRocksEnvironment, EnvironmentBeforeRocksPath, true),
                "Could not preserve the before-rock environment scene.");

            source = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            Require(canvas != null, "The cleaned volcano terrain is missing.");
            Require(GameObject.Find(DressingRootName) == null, "Rock dressing already exists; refusing to duplicate it.");
            int rockCount = BuildRockClusters(canvas);
            canvas.CopyState(out float[] finalHeights, out int[] finalPaint);
            Require(baselineHeights.SequenceEqual(finalHeights), "Terrain heights changed during crater cleanup or rock dressing.");
            Require(baselinePaint.SequenceEqual(finalPaint), "Terrain paint changed during crater cleanup or rock dressing.");
            Require(Mathf.Abs(finalHeights.Max() - baselinePeak) < 0.001f, "The volcano height changed.");

            EditorSceneManager.MarkSceneDirty(source);
            Require(EditorSceneManager.SaveScene(source, SourceScenePath), "Could not save the rock-dressed volcano scene.");
            AssetDatabase.SaveAssets();
            EnvironmentMoodTestTools.BuildTestSceneBatch();
            Require(GameObject.Find(DressingRootName) != null, "The dressed environment did not retain the rock clusters.");

            Debug.Log(
                "VOLCANO_CRATER_ROCK_DRESSING_COMPLETE" +
                $"|terrainHeightChange=none|terrainPaintChange=none|peak={baselinePeak:F2}" +
                $"|craterDiameterChange=none|craterDepthChange=none|rimMeanChange={craterStats.RimMeanChange:F4}" +
                $"|maxOverlaySmoothing={craterStats.MaxChange:F2}|smoothingStrength=20pct" +
                $"|clusters=5|rocks={rockCount}|floatingRocks=0|burial=20-40pct" +
                "|prefabs=SM_Rocks_01,03,04,06,07,08,09,11|materials=CharcoalMatte,GrayBrownMatte" +
                "|waterChange=none|gameplayChange=none|FastSky=preserved");

            if (showDialog)
            {
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "The restored crater is now locked. Only high-frequency overlay teeth were softened, and five grounded Free Pack rock clusters were added without changing terrain, crater dimensions, depth or volcano height.",
                    "OK");
            }
        }

        private static CraterStats SoftenOnlyHighFrequencyCraterOverlay(GameObject crater)
        {
            MeshFilter filter = crater.GetComponent<MeshFilter>();
            Require(filter != null && filter.sharedMesh != null, "The crater mesh is missing.");
            Mesh mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = filter.sharedMesh.name + " - Locked 20 Percent High Frequency Cleanup";
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            HashSet<int>[] neighbours = new HashSet<int>[vertices.Length];
            for (int i = 0; i < neighbours.Length; i++) neighbours[i] = new HashSet<int>();
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                AddEdge(neighbours, triangles[i], triangles[i + 1]);
                AddEdge(neighbours, triangles[i + 1], triangles[i + 2]);
                AddEdge(neighbours, triangles[i + 2], triangles[i]);
            }

            float[] original = vertices.Select(v => v.y).ToArray();
            float originalMin = original.Min();
            float originalMax = original.Max();
            List<int> rim = new List<int>();
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = crater.transform.TransformPoint(vertices[i]);
                float radial = Vector2.Distance(new Vector2(world.x, world.z), CraterCenter);
                if (radial >= 27f && radial <= 48f) rim.Add(i);
            }
            float rimMeanBefore = rim.Count > 0 ? rim.Average(i => original[i]) : 0f;
            float maxChange = 0f;

            for (int i = 0; i < vertices.Length; i++)
            {
                if (neighbours[i].Count < 3) continue;
                Vector3 world = crater.transform.TransformPoint(vertices[i]);
                if (Vector2.Distance(new Vector2(world.x, world.z), CraterCenter) > 55f) continue;
                if (Mathf.Abs(original[i] - originalMin) < 0.001f || Mathf.Abs(original[i] - originalMax) < 0.001f) continue;
                float average = neighbours[i].Average(n => original[n]);
                float residual = original[i] - average;
                if (Mathf.Abs(residual) < 1.15f) continue;
                float change = Mathf.Clamp(-residual * 0.20f, -1.8f, 1.8f);
                vertices[i].y += change;
                maxChange = Mathf.Max(maxChange, Mathf.Abs(change));
            }

            if (rim.Count > 0)
            {
                float rimMeanAfter = rim.Average(i => vertices[i].y);
                float correction = rimMeanBefore - rimMeanAfter;
                foreach (int i in rim)
                {
                    if (Mathf.Abs(original[i] - originalMin) < 0.001f || Mathf.Abs(original[i] - originalMax) < 0.001f) continue;
                    vertices[i].y = Mathf.Clamp(vertices[i].y + correction, originalMin, originalMax);
                }
            }

            float finalRimMean = rim.Count > 0 ? rim.Average(i => vertices[i].y) : rimMeanBefore;
            Require(vertices.Select(v => v.x).SequenceEqual(mesh.vertices.Select(v => v.x)), "Crater width changed.");
            Require(vertices.Select(v => v.z).SequenceEqual(mesh.vertices.Select(v => v.z)), "Crater depth axis changed.");
            Require(Mathf.Abs(vertices.Min(v => v.y) - originalMin) < 0.001f, "Crater floor depth changed.");
            Require(Mathf.Abs(vertices.Max(v => v.y) - originalMax) < 0.001f, "Crater rim maximum changed.");
            Require(Mathf.Abs(finalRimMean - rimMeanBefore) < 0.01f, "Crater rim mean height changed.");

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            return new CraterStats(maxChange, finalRimMean - rimMeanBefore);
        }

        private static int BuildRockClusters(EditableIslandMeshCanvas canvas)
        {
            Material charcoal = AssetDatabase.LoadAssetAtPath<Material>(CharcoalMaterialPath);
            Material grayBrown = AssetDatabase.LoadAssetAtPath<Material>(GrayBrownMaterialPath);
            Require(charcoal != null && grayBrown != null, "The approved matte rock materials are missing.");

            Cluster[] clusters =
            {
                new Cluster("A - Broken Front Rim", new Vector2(-4f, 55f), true,
                    new Piece("SM_Rocks_03", 0f, 0f, 11.0f, 18f, 0.30f),
                    new Piece("SM_Rocks_07", -2.8f, -1.0f, 7.8f, 83f, 0.24f),
                    new Piece("SM_Rocks_06", 2.6f, 1.4f, 4.8f, 141f, 0.27f),
                    new Piece("SM_Rocks_11", 0.8f, -2.3f, 4.2f, 219f, 0.29f)),
                new Cluster("B - Front East Upper Slope", new Vector2(105f, 5f), true,
                    new Piece("SM_Rocks_01", 0f, 0f, 20.0f, 34f, 0.28f),
                    new Piece("SM_Rocks_04", -3.8f, 1.2f, 12.5f, 112f, 0.24f),
                    new Piece("SM_Rocks_09", 3.5f, -1.0f, 7.2f, 188f, 0.27f),
                    new Piece("SM_Rocks_06", 1.0f, 3.0f, 6.0f, 257f, 0.29f)),
                new Cluster("C - Front West Upper Slope", new Vector2(-55f, 15f), true,
                    new Piece("SM_Rocks_03", 0f, 0f, 18.0f, 71f, 0.27f),
                    new Piece("SM_Rocks_08", 3.6f, 1.8f, 12.0f, 149f, 0.24f),
                    new Piece("SM_Rocks_11", -3.2f, -1.2f, 7.0f, 222f, 0.27f),
                    new Piece("SM_Rocks_09", 0.8f, -3.0f, 6.0f, 301f, 0.29f)),
                new Cluster("D - Front West Foot Transition", new Vector2(-130f, -130f), false,
                    new Piece("SM_Rocks_01", 0f, 0f, 25.0f, 23f, 0.25f),
                    new Piece("SM_Rocks_04", 4.8f, 1.0f, 16.0f, 96f, 0.24f),
                    new Piece("SM_Rocks_07", -4.3f, -0.8f, 12.0f, 167f, 0.26f),
                    new Piece("SM_Rocks_06", 2.8f, -3.6f, 7.5f, 245f, 0.30f),
                    new Piece("SM_Rocks_11", -2.0f, 3.6f, 6.5f, 319f, 0.28f)),
                new Cluster("E - Front East Foot Transition", new Vector2(245f, -90f), false,
                    new Piece("SM_Rocks_03", 0f, 0f, 26.0f, 52f, 0.24f),
                    new Piece("SM_Rocks_08", -4.8f, 1.3f, 16.0f, 123f, 0.24f),
                    new Piece("SM_Rocks_04", 4.5f, -1.0f, 13.0f, 194f, 0.26f),
                    new Piece("SM_Rocks_09", 2.5f, 3.5f, 7.5f, 266f, 0.30f),
                    new Piece("SM_Rocks_06", -2.5f, -3.3f, 6.8f, 337f, 0.28f))
            };

            GameObject root = new GameObject(DressingRootName);
            int count = 0;
            foreach (Cluster cluster in clusters)
            {
                GameObject clusterRoot = new GameObject(cluster.Name);
                clusterRoot.transform.SetParent(root.transform, false);
                Material material = cluster.Charcoal ? charcoal : grayBrown;
                foreach (Piece piece in cluster.Pieces)
                {
                    string path = PrefabFolder + piece.Prefab + ".prefab";
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Require(prefab != null, "Missing real Free Pack prefab: " + path);
                    GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    Require(instance != null, "Could not instantiate: " + path);
                    instance.name = piece.Prefab + " - " + cluster.Name;
                    instance.transform.SetParent(clusterRoot.transform, true);
                    Vector3 position = new Vector3(cluster.Center.x + piece.Offset.x, 0f, cluster.Center.y + piece.Offset.y);
                    float ground = canvas.SampleHeightWorld(position);
                    Vector3 normal = SampleTerrainNormal(canvas, position, 3.5f);
                    instance.transform.position = new Vector3(position.x, ground, position.z);
                    instance.transform.rotation = Quaternion.AngleAxis(piece.Yaw, normal) * Quaternion.FromToRotation(Vector3.up, normal);
                    ApplyMaterial(instance, material);
                    ScaleAndBury(instance, canvas, piece.Height, piece.Burial);
                    count++;
                }
            }

            Require(count == 22, "Unexpected rock dressing count.");
            return count;
        }

        private static void ScaleAndBury(
            GameObject instance,
            EditableIslandMeshCanvas canvas,
            float targetHeight,
            float burial)
        {
            Require(TryBounds(instance, out Bounds initial) && initial.size.y > 0.001f, "Rock renderer bounds are missing.");
            instance.transform.localScale *= targetHeight / initial.size.y;
            Require(TryBounds(instance, out Bounds scaled), "Scaled rock bounds are missing.");
            Vector3 center = scaled.center;
            float ground = canvas.SampleHeightWorld(new Vector3(center.x, 0f, center.z));
            float desiredBottom = ground - scaled.size.y * burial;
            instance.transform.position += Vector3.up * (desiredBottom - scaled.min.y);
            Require(TryBounds(instance, out Bounds final), "Final rock bounds are missing.");
            Require(final.min.y <= ground - final.size.y * 0.19f, "A dressed rock was not buried enough.");
        }

        private static Vector3 SampleTerrainNormal(EditableIslandMeshCanvas canvas, Vector3 point, float sample)
        {
            float left = canvas.SampleHeightWorld(point + Vector3.left * sample);
            float right = canvas.SampleHeightWorld(point + Vector3.right * sample);
            float back = canvas.SampleHeightWorld(point + Vector3.back * sample);
            float forward = canvas.SampleHeightWorld(point + Vector3.forward * sample);
            return new Vector3(left - right, sample * 2f, back - forward).normalized;
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

        private static void AddEdge(HashSet<int>[] neighbours, int a, int b)
        {
            neighbours[a].Add(b);
            neighbours[b].Add(a);
        }

        private static Texture2D Capture(
            EditableIslandMeshCanvas canvas,
            Vector3 position,
            Vector3 target,
            float fov,
            string path)
        {
            GameObject cameraObject = new GameObject("Temporary Locked Crater Rock Dressing Camera");
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
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            return image;
        }

        private static void WriteComparison(Texture2D before, Texture2D after, string path)
        {
            Require(before.width == after.width && before.height == after.height, "Comparison dimensions do not match.");
            Texture2D comparison = new Texture2D(before.width * 2 + 8, before.height, TextureFormat.RGB24, false);
            Color[] divider = Enumerable.Repeat(new Color(0.96f, 0.96f, 0.96f), 8 * before.height).ToArray();
            comparison.SetPixels(0, 0, before.width, before.height, before.GetPixels());
            comparison.SetPixels(before.width, 0, 8, before.height, divider);
            comparison.SetPixels(before.width + 8, 0, after.width, after.height, after.GetPixels());
            comparison.Apply();
            File.WriteAllBytes(path, comparison.EncodeToPNG());
            Object.DestroyImmediate(comparison);
        }

        private static Texture2D LoadCapture(string path)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            Require(texture.LoadImage(File.ReadAllBytes(path), false), "Could not reload capture: " + path);
            return texture;
        }

        private readonly struct CraterStats
        {
            public readonly float MaxChange;
            public readonly float RimMeanChange;

            public CraterStats(float maxChange, float rimMeanChange)
            {
                MaxChange = maxChange;
                RimMeanChange = rimMeanChange;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
