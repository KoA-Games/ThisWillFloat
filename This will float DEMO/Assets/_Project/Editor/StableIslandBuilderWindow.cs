using System.IO;
using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class StableIslandBuilderUtility
    {
        private const string ScenePath = "Assets/_Project/Scenes/Editable_Island_Workspace.unity";
        private const string MaterialFolder = "Assets/_Project/GeneratedMaterials";
        private const string CanvasName = "EDIT ME - Mesh Island Canvas";
        private const string OceanName = "Ocean - waves, visual only";
        private const string PlacedRootName = "Builder Placed Assets";
        private const string PlayerName = "Demo First Person Player";

        [MenuItem("This Will Float DEMO/9. Old Tools/Open Blank Saved Editable Workspace")]
        public static void OpenSavedEditableIslandWorkspace()
        {
            if (!File.Exists(ScenePath))
            {
                EditorUtility.DisplayDialog("This Will Float DEMO", "Saved editable island scene was not found.", "OK");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditableIslandMeshCanvas canvas = GetCanvas();
                if (canvas != null)
                {
                    Selection.activeGameObject = canvas.gameObject;
                    FrameCanvas(canvas);
                }

                StableIslandBuilderWindow.ShowWindow();
            }
        }

        [MenuItem("This Will Float DEMO/4. Open Stable Mesh Island Builder")]
        public static void OpenStableBuilder()
        {
            StableIslandBuilderWindow.ShowWindow();
        }

        [InitializeOnLoadMethod]
        private static void RestoreSceneView3DOnCompile()
        {
            EditorApplication.delayCall += ForceSceneView3D;
        }

        [MenuItem("This Will Float DEMO/4. Force Scene View Back To 3D")]
        public static void ForceSceneView3D()
        {
            foreach (SceneView view in SceneView.sceneViews)
            {
                if (view == null)
                {
                    continue;
                }

                view.in2DMode = false;
                view.LookAt(view.pivot, Quaternion.Euler(45f, -35f, 0f), Mathf.Max(12f, view.size));
                view.Repaint();
            }
        }

        [MenuItem("This Will Float DEMO/5. Spawn Or Reset Test Player")]
        public static void SpawnOrResetPlayerMenu()
        {
            GameObject player = SpawnOrResetPlayer();
            FramePlayer(player);
        }

        [MenuItem("This Will Float DEMO/6. Polish Current Volcano Landmark")]
        public static void PolishCurrentVolcanoLandmarkMenu()
        {
            PolishCurrentVolcanoLandmark();
        }

        [MenuItem("This Will Float DEMO/7. Fix Terrain Shine / Reapply Matte Textures")]
        public static void FixTerrainShineMenu()
        {
            EditableIslandMeshCanvas canvas = GetCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("This Will Float DEMO", "No editable island canvas found.", "OK");
                return;
            }

            FixImportedAssetMaterials();
            canvas.RebuildMesh();
            ApplyImportedTerrainTextures(canvas);
            ApplyMatteToSceneVegetation();
            ApplyOceanWaterMaterial();
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        [MenuItem("This Will Float DEMO/8. Try NVJOB Water / Fix Pink Ocean")]
        public static void TryNvjobWaterMenu()
        {
            ApplyOceanWaterMaterial(true);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        internal static void BuildFreshWorkspace()
        {
            EnsureFolder("Assets/_Project");
            EnsureFolder("Assets/_Project/Scenes");
            EnsureFolder(MaterialFolder);
            FixImportedAssetMaterials();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureLighting();
            ResetOceanCanvas();
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), ScenePath);
            AssetDatabase.SaveAssets();
            StableIslandBuilderWindow.ShowWindow();
        }

        internal static EditableIslandMeshCanvas ResetOceanCanvas()
        {
            return ResetOceanCanvas(980f, 385);
        }

        internal static EditableIslandMeshCanvas ResetOceanCanvas(float canvasSize, int resolution)
        {
            EnsureFolder(MaterialFolder);
            DeleteSceneObject(CanvasName);
            DeleteSceneObject(OceanName);
            DeleteSceneObject("Ocean - visual only, no collider");
            DeleteSceneObject("EDIT ME - Island Terrain");
            DeleteSceneObject("Builder Land Patches");
            DeleteSceneObject(PlacedRootName);
            DeleteSceneObject(PlayerName);

            CreateOcean();
            EditableIslandMeshCanvas canvas = CreateCanvas(canvasSize, resolution);
            Selection.activeGameObject = canvas.gameObject;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            return canvas;
        }

        internal static void ExpandCurrentCanvas(float targetSize, int targetResolution)
        {
            EditableIslandMeshCanvas canvas = GetCanvas();
            if (canvas == null)
            {
                canvas = ResetOceanCanvas(targetSize, targetResolution);
            }
            else
            {
                Undo.RegisterCompleteObjectUndo(canvas, "Expand Island Edit Area");
                canvas.ResizePreserveWorld(targetSize, targetResolution);
                EditorUtility.SetDirty(canvas);
                Selection.activeGameObject = canvas.gameObject;
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }

            FrameCanvas(canvas);
        }

        internal static void RefineCurrentCanvasDetail(int targetResolution)
        {
            EditableIslandMeshCanvas canvas = GetCanvas();
            if (canvas == null)
            {
                return;
            }

            targetResolution = Mathf.Clamp(targetResolution, canvas.VertexResolution, 513);
            if (targetResolution <= canvas.VertexResolution)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(canvas, "Refine Island Mesh Detail");
            canvas.ResizePreserveWorld(canvas.Size, targetResolution);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvas.gameObject;
            SceneView.RepaintAll();
        }

        internal static GameObject SpawnOrResetPlayer()
        {
            EditableIslandMeshCanvas canvas = GetCanvas();
            if (canvas == null)
            {
                canvas = CreateStarterIsland();
            }

            DeleteSceneObject(PlayerName);
            Vector3 spawn = FindSafeSpawnPoint(canvas, out bool foundSafeGround);
            if (!foundSafeGround)
            {
                Undo.RegisterCompleteObjectUndo(canvas, "Create Starter Island For Player");
                canvas.MakeStarterIsland();
                EditorUtility.SetDirty(canvas);
                spawn = FindSafeSpawnPoint(canvas, out _);
            }

            Vector3 lookDirection = new Vector3(-spawn.x, 0f, -spawn.z);
            if (lookDirection.sqrMagnitude < 0.001f)
            {
                lookDirection = Vector3.forward;
            }

            Quaternion rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(player, "Spawn Demo Player");
            player.name = PlayerName;
            player.transform.SetPositionAndRotation(spawn, rotation);
            Renderer renderer = player.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            Collider capsuleCollider = player.GetComponent<Collider>();
            if (capsuleCollider != null)
            {
                Object.DestroyImmediate(capsuleCollider);
            }

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.9f, 0f);

            GameObject cameraObject = new GameObject("Demo First Person Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.03f;

            if (FindAnySceneAudioListener() == null)
            {
                cameraObject.AddComponent<AudioListener>();
            }

            DemoFirstPersonPlayer controllerScript = player.AddComponent<DemoFirstPersonPlayer>();
            SerializedObject serialized = new SerializedObject(controllerScript);
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("groundMask").intValue = ~0;
            serialized.FindProperty("startPosition").vector3Value = spawn;
            serialized.FindProperty("startRotation").quaternionValue = rotation;
            serialized.FindProperty("resetIfBelowY").floatValue = -20f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Selection.activeGameObject = player;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return player;
        }

        internal static void PolishCurrentVolcanoLandmark()
        {
            EditableIslandMeshCanvas canvas = GetCanvas();
            if (canvas == null)
            {
                EditorUtility.DisplayDialog("This Will Float DEMO", "No editable island canvas found. Open/create the island workspace first.", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(canvas, "Polish Volcano Landmark");
            DeleteSceneObject("Volcano Transition Rocks");
            DeleteSceneObjectsWithPrefix("Volcano Rock ");
            DeleteSceneObject("Readable Volcano Mesh");
            DeleteSceneObject("Central volcano landmark");
            DeleteSceneObject("Dark crater");
            DeleteSceneObjectsWithPrefix("Recovered Volcano");
            if (canvas.VertexResolution < 513)
            {
                canvas.ResizePreserveWorld(canvas.Size, 513);
            }

            canvas.CopyState(out float[] beforeHeights, out int[] beforePaint);
            canvas.PolishVolcanoLandmark(LoadPreferredVolcanoStamp());
            canvas.RepaintReadableIslandBiomes();
            RefreshTerrainMaterialPalette(canvas);
            ScatterNaturalVegetationZone(canvas);
            ApplyMatteToSceneVegetation();

            if (!canvas.HasValidVisibleLand())
            {
                canvas.RestoreState(beforeHeights, beforePaint);
                RefreshTerrainMaterialPalette(canvas);
                ApplyMatteToSceneVegetation();
                EditorUtility.DisplayDialog(
                    "This Will Float DEMO",
                    "Volcano polish tried to leave the island in an invalid/blank state, so I restored the previous terrain instead.",
                    "OK");
            }

            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = canvas.gameObject;
            FrameCanvas(canvas);
            SceneView.RepaintAll();
        }

        internal static Texture2D LoadPreferredVolcanoStamp()
        {
            string[] paths =
            {
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 07.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 04.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 01.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 10.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 08.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 09.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 06.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 05.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 03.exr",
                "Assets/Gaia User Data/Stamps/2K Volcanos/Volcano 02.exr"
            };

            foreach (string path in paths)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (texture == null)
                {
                    continue;
                }

                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null && !importer.isReadable)
                {
                    importer.isReadable = true;
                    importer.sRGBTexture = false;
                    importer.SaveAndReimport();
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }

                return texture;
            }

            return null;
        }

        internal static void FramePlayer(GameObject player)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || player == null)
            {
                return;
            }

            view.in2DMode = false;
            view.LookAt(player.transform.position + Vector3.up * 1.2f, Quaternion.Euler(20f, player.transform.eulerAngles.y, 0f), 8f);
            SceneView.RepaintAll();
        }

        internal static EditableIslandMeshCanvas CreateStarterIsland()
        {
            EditableIslandMeshCanvas canvas = ResetOceanCanvas();
            Undo.RegisterCompleteObjectUndo(canvas, "Create Starter Island");
            canvas.MakeStarterIsland();
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return canvas;
        }

        internal static EditableIslandMeshCanvas GetCanvas()
        {
            if (Selection.activeGameObject != null)
            {
                EditableIslandMeshCanvas selected = Selection.activeGameObject.GetComponent<EditableIslandMeshCanvas>();
                if (selected != null)
                {
                    return selected;
                }
            }

            GameObject named = FindSceneObject(CanvasName);
            if (named != null)
            {
                return named.GetComponent<EditableIslandMeshCanvas>();
            }

            foreach (EditableIslandMeshCanvas canvas in Resources.FindObjectsOfTypeAll<EditableIslandMeshCanvas>())
            {
                if (canvas != null && canvas.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(canvas))
                {
                    return canvas;
                }
            }

            return null;
        }

        internal static void SetWaterVisible(bool visible)
        {
            GameObject ocean = FindSceneObject(OceanName);
            if (ocean == null && visible)
            {
                ocean = CreateOcean();
            }

            if (ocean != null)
            {
                ocean.SetActive(visible);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        internal static bool IsWaterVisible()
        {
            GameObject ocean = FindSceneObject(OceanName);
            return ocean != null && ocean.activeSelf;
        }

        internal static void FrameCanvas(EditableIslandMeshCanvas canvas)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || canvas == null)
            {
                return;
            }

            view.in2DMode = false;
            Vector3 center = canvas.transform.position;
            view.LookAt(center + Vector3.up * 8f, Quaternion.Euler(62f, 0f, 0f), canvas.Size * 0.62f);
            SceneView.RepaintAll();
        }

        private static void RefreshTerrainMaterialPalette(EditableIslandMeshCanvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            MeshRenderer renderer = canvas.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length < 4)
            {
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            SetMaterialColor(materials[0], new Color(0.69f, 0.54f, 0.34f));
            SetMaterialColor(materials[1], new Color(0.36f, 0.31f, 0.26f));
            SetMaterialColor(materials[2], new Color(0.74f, 0.72f, 0.68f));
            SetMaterialColor(materials[3], new Color(0.33f, 0.52f, 0.25f));

            foreach (Material material in materials)
            {
                if (material != null)
                {
                    SetFloat(material, 0.24f, "_Smoothness", "_Glossiness");
                    EditorUtility.SetDirty(material);
                }
            }

            ApplyImportedTerrainTextures(canvas);
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return;
            }

            SetColor(material, color, "_BaseColor", "_Color");
        }

        internal static GameObject PlacePrefab(IslandEditorTool tool, Vector3 position)
        {
            return PlacePrefab(tool, position, 1f);
        }

        internal static GameObject PlacePrefab(IslandEditorTool tool, Vector3 position, float scaleMultiplier)
        {
            string path = tool switch
            {
                IslandEditorTool.Palm => "Assets/PalmTreePack/Prefabs/1/Palm_1_1.prefab",
                IslandEditorTool.Rock => "Assets/RockFREE/LODGroups/rock1_LOD0.prefab",
                IslandEditorTool.Crate => "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigCrate_color01.prefab",
                IslandEditorTool.Barrel => "Assets/Crates And Barrels Pack Volume 1 - Free Version/Prefabs/Prefab_BigBarrel_color01.prefab",
                IslandEditorTool.Log => "Assets/AwesomeFreeScans/WoodLogs/Prefabs/LowEndPC/SM_AFS_Log01_LowEndPC.prefab",
                _ => ""
            };

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning($"Island Builder: Prefab missing at {path}");
                return null;
            }

            GameObject root = FindSceneObject(PlacedRootName);
            if (root == null)
            {
                root = new GameObject(PlacedRootName);
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Island Prop");
            instance.transform.SetParent(root.transform);
            instance.transform.position = position;
            instance.transform.rotation = GetPlacementRotation(tool);
            instance.transform.localScale = GetPlacementScale(tool) * Mathf.Clamp(scaleMultiplier, 0.1f, 3f);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return instance;
        }

        internal static void ApplyImportedTerrainTextures(EditableIslandMeshCanvas canvas)
        {
            if (canvas == null)
            {
                canvas = GetCanvas();
            }

            if (canvas == null)
            {
                return;
            }

            MeshRenderer renderer = canvas.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length < 4)
            {
                return;
            }

            Material[] materials = renderer.sharedMaterials;
            ConfigureTerrainMaterial(
                materials[0],
                new Color(1.0f, 0.9f, 0.67f),
                new[]
                {
                    "Assets/PalmTreePack/Textures/Ground/Ground_Sand_albedo.tif",
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Diffuse.tga"
                },
                new[]
                {
                    "Assets/PalmTreePack/Textures/Ground/Ground_Sand_normal.tif",
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Normal.tga"
                },
                0.07f);
            ConfigureTerrainMaterial(
                materials[1],
                new Color(0.74f, 0.66f, 0.55f),
                new[]
                {
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Diffuse.tga",
                    "Assets/PalmTreePack/Textures/Ground/Ground_Sand_albedo.tif"
                },
                new[]
                {
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Normal.tga",
                    "Assets/PalmTreePack/Textures/Ground/Ground_Sand_normal.tif"
                },
                0.08f);
            ConfigureTerrainMaterial(
                materials[2],
                new Color(0.74f, 0.72f, 0.68f),
                new[]
                {
                    "Assets/Blocky Cliff Formations/Assets/Cliff 3/Textures 4K/Gray Rock/Cliff_3_LP_BaseMap.tif",
                    "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Cliff_Rock_Diffuse.psd",
                    "Assets/Blocky Cliff Formations/Assets/Cliff 5/Textures 4K/Dark Rock/Cliff_5_LP_BaseMap.tif",
                    "Assets/A_piece_of_nature/Textures/Rocks_3_4_texture.tif"
                },
                new[]
                {
                    "Assets/Blocky Cliff Formations/Assets/Cliff 3/Textures 4K/Gray Rock/Cliff_3_LP_Normal.tif",
                    "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Cliff_Rock_Normal.psd",
                    "Assets/Blocky Cliff Formations/Assets/Cliff 5/Textures 4K/Dark Rock/Cliff_5_LP_Normal.tif",
                    "Assets/A_piece_of_nature/Textures/Rocks_3_4_normal.tif"
                },
                0.08f);
            ConfigureTerrainMaterial(
                materials[3],
                new Color(0.58f, 0.76f, 0.42f),
                new[]
                {
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Grass_04/Grass_04_Diffuse.tga",
                    "Assets/A_piece_of_nature/Textures/Grass_texture_1.tif",
                    "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Grass_Diffuse.psd"
                },
                new[]
                {
                    "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Grass_04/Grass_04_Normal.tga",
                    "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Grass_Normal.psd"
                },
                0.06f);

            EditorUtility.SetDirty(renderer);
            SceneView.RepaintAll();
        }

        internal static void ApplyReferenceVolcanoPresentation(EditableIslandMeshCanvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            DeleteSceneObject("Natural Vegetation Zone Props");
            DeleteSceneObject("Volcano Transition Rocks");
            DeleteSceneObjectsWithPrefix("Volcano Rock ");
            DeleteSceneObjectsWithPrefix("Recovered Volcano");
            DeleteSceneObject("Readable Volcano Mesh");
            DeleteSceneObject("Central volcano landmark");
            DeleteSceneObject("Dark crater");

            MeshRenderer terrainRenderer = canvas.GetComponent<MeshRenderer>();
            if (terrainRenderer != null)
            {
                terrainRenderer.sharedMaterials = new[]
                {
                    GetMaterial("Reference Pale Ash Shore", new Color(0.90f, 0.89f, 0.85f), 0.08f, false),
                    GetMaterial("Reference Wet Basalt", new Color(0.27f, 0.31f, 0.36f), 0.16f, false),
                    GetMaterial("Reference Chalk Volcanic Rock", new Color(1f, 1f, 1f), 0.07f, false),
                    GetMaterial("Reference Cool Weathered Ash", new Color(0.82f, 0.85f, 0.88f), 0.06f, false)
                };

                ConfigureTerrainMaterial(
                    terrainRenderer.sharedMaterials[0],
                    new Color(0.90f, 0.89f, 0.85f),
                    new[]
                    {
                        "Assets/Blocky Cliff Formations/Assets/Cliff 3/Textures 4K/Gray Rock/Cliff_3_LP_BaseMap.tif",
                        "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Diffuse.tga"
                    },
                    new[]
                    {
                        "Assets/Blocky Cliff Formations/Assets/Cliff 3/Textures 4K/Gray Rock/Cliff_3_LP_Normal.tif",
                        "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Normal.tga"
                    },
                    0.08f);
                ConfigureTerrainMaterial(
                    terrainRenderer.sharedMaterials[1],
                    new Color(0.27f, 0.31f, 0.36f),
                    new[]
                    {
                        "Assets/Blocky Cliff Formations/Assets/Cliff 5/Textures 4K/Dark Rock/Cliff_5_LP_BaseMap.tif",
                        "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Cliff_Rock_Diffuse.psd"
                    },
                    new[]
                    {
                        "Assets/Blocky Cliff Formations/Assets/Cliff 5/Textures 4K/Dark Rock/Cliff_5_LP_Normal.tif",
                        "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Cliff_Rock_Normal.psd"
                    },
                    0.16f);
                ConfigureTerrainMaterial(
                    terrainRenderer.sharedMaterials[2],
                    new Color(1f, 1f, 1f),
                    new[]
                    {
                        "Assets/Blocky Cliff Formations/Assets/Cliff 3/Textures 4K/Gray Rock/Cliff_3_LP_BaseMap.tif",
                        "Assets/A_piece_of_nature/Textures/Rocks_3_4_texture.tif"
                    },
                    new[]
                    {
                        "Assets/Blocky Cliff Formations/Assets/Cliff 3/Textures 4K/Gray Rock/Cliff_3_LP_Normal.tif",
                        "Assets/A_piece_of_nature/Textures/Rocks_3_4_normal.tif"
                    },
                    0.07f);
                ConfigureTerrainMaterial(
                    terrainRenderer.sharedMaterials[3],
                    new Color(0.82f, 0.85f, 0.88f),
                    new[]
                    {
                        "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Cliff_Rock_Diffuse.psd",
                        "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Diffuse.tga"
                    },
                    new[]
                    {
                        "Assets/RockFREE/Textures/Terrain_Textures/Ground_Textures/Terr_Cliff_Rock_Normal.psd",
                        "Assets/PBR Ground Materials #1 [Dirt& Grass]/Textures/Dirt_04/Dirt_04_Normal.tga"
                    },
                    0.06f);

                SetTexture(terrainRenderer.sharedMaterials[0], null, "_BaseMap", "_MainTex");
                SetTexture(terrainRenderer.sharedMaterials[2], null, "_BaseMap", "_MainTex");
                SetTexture(terrainRenderer.sharedMaterials[3], null, "_BaseMap", "_MainTex");
                SetColor(terrainRenderer.sharedMaterials[0], new Color(0.90f, 0.89f, 0.85f), "_BaseColor", "_Color");
                SetColor(terrainRenderer.sharedMaterials[2], Color.white, "_BaseColor", "_Color");
                SetColor(terrainRenderer.sharedMaterials[3], new Color(0.82f, 0.85f, 0.88f), "_BaseColor", "_Color");

                terrainRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                terrainRenderer.receiveShadows = true;
                EditorUtility.SetDirty(terrainRenderer);
            }

            ApplyReferenceOceanLook();
            ConfigureReferenceVolcanoLighting();

            foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (camera == null || !camera.gameObject.scene.IsValid() || EditorUtility.IsPersistent(camera))
                {
                    continue;
                }

                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.008f, 0.035f, 0.09f, 1f);
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 3000f);
                EditorUtility.SetDirty(camera);
            }

            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            FrameReferenceVolcano(canvas);
            SceneView.RepaintAll();
        }

        internal static void FrameReferenceVolcano(EditableIslandMeshCanvas canvas)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || canvas == null)
            {
                return;
            }

            view.in2DMode = false;
            Vector3 focus = canvas.transform.position + new Vector3(-12f, 52f, 18f);
            view.LookAt(focus, Quaternion.Euler(52f, -42f, 0f), canvas.Size * 0.53f);
            view.Repaint();
        }

        internal static void RepaintCleanIslandBiomes()
        {
            EditableIslandMeshCanvas canvas = GetCanvas();
            if (canvas == null)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(canvas, "Repaint Clean Island Biomes");
            DeleteSceneObject("Volcano Transition Rocks");
            DeleteSceneObjectsWithPrefix("Volcano Rock ");
            DeleteSceneObject("Readable Volcano Mesh");
            DeleteSceneObject("Central volcano landmark");
            DeleteSceneObject("Dark crater");
            DeleteSceneObjectsWithPrefix("Recovered Volcano");
            if (canvas.VertexResolution < 513)
            {
                canvas.ResizePreserveWorld(canvas.Size, 513);
            }

            canvas.RepaintReadableIslandBiomes();
            ApplyImportedTerrainTextures(canvas);
            ApplyOceanWaterMaterial(false);
            ScatterNaturalVegetationZone(canvas);
            EditorUtility.SetDirty(canvas);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private static void ScatterNaturalVegetationZone(EditableIslandMeshCanvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            DeleteSceneObject("Natural Vegetation Zone Props");
            GameObject root = new GameObject("Natural Vegetation Zone Props");
            Undo.RegisterCreatedObjectUndo(root, "Create Natural Vegetation Zone");

            Vector3 center = canvas.EstimateVolcanoCenterWorld();
            float baseRadius = Mathf.Clamp(canvas.Size * 0.046f, 38f, 58f);
            Material grassMaterial = GetLowpolyNatureGrassMaterial();

            int placedGrass = 0;
            for (int i = 0; i < 260 && placedGrass < 135; i++)
            {
                float angle = i * 2.3999632f + Hash01(i * 9.13f) * 1.2f;
                float radius = Mathf.Lerp(baseRadius * 1.35f, baseRadius * 2.75f, Hash01(i * 17.41f));
                Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!canvas.ContainsWorldPoint(position))
                {
                    continue;
                }

                float height = canvas.SampleHeightWorld(position);
                if (height < canvas.WaterLevel + 3.0f || height > canvas.WaterLevel + 8.2f)
                {
                    continue;
                }

                if (canvas.SamplePaintLayerWorld(position) != 3)
                {
                    continue;
                }

                position.y = height + 0.03f;
                CreateNatureGrassPatch(root.transform, position, grassMaterial, i);
                placedGrass++;
            }

            int placedPalms = 0;
            for (int i = 0; i < 80 && placedPalms < 6; i++)
            {
                float angle = i * 2.3999632f + Hash01(i * 5.13f) * 1.4f;
                float radius = Mathf.Lerp(baseRadius * 1.75f, baseRadius * 2.95f, Hash01(i * 8.41f));
                Vector3 position = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!canvas.ContainsWorldPoint(position))
                {
                    continue;
                }

                float height = canvas.SampleHeightWorld(position);
                if (height < canvas.WaterLevel + 3.0f || height > canvas.WaterLevel + 7.2f || canvas.SamplePaintLayerWorld(position) != 3)
                {
                    continue;
                }

                position.y = height + 0.03f;
                PlaceVegetationPalm(root.transform, position, i);
                placedPalms++;
            }
        }

        private static Material GetLowpolyNatureGrassMaterial()
        {
            string path = $"{MaterialFolder}/Builder Lowpoly Nature Grass.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = FindUnlitShader() ?? FindLitShader();
            if (material == null)
            {
                material = new Material(shader) { name = "Builder Lowpoly Nature Grass" };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            SetTexture(material, null, "_BaseMap", "_MainTex");
            SetColor(material, new Color(0.34f, 0.56f, 0.27f, 1f), "_BaseColor", "_Color");
            SetFloat(material, 0.015f, "_Smoothness", "_Glossiness");
            SetFloat(material, 0f, "_Metallic");
            SetFloat(material, 0f, "_SpecularHighlights");
            SetFloat(material, 0f, "_EnvironmentReflections");
            SetFloat(material, 0f, "_AlphaClip");
            SetFloat(material, 0f, "_Cutoff");
            ApplyMatteSurface(material);
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateNatureGrassPatch(Transform root, Vector3 position, Material grassMaterial, int seed)
        {
            GameObject patch = new GameObject($"Lowpoly Nature Grass Patch {seed:000}");
            Undo.RegisterCreatedObjectUndo(patch, "Create Lowpoly Nature Grass");
            patch.transform.SetParent(root);
            patch.transform.position = position;
            patch.transform.rotation = Quaternion.Euler(0f, Hash01(seed * 3.91f) * 360f, 0f);

            int tuftCount = 2 + Mathf.FloorToInt(Hash01(seed * 1.73f) * 3f);
            for (int i = 0; i < tuftCount; i++)
            {
                GameObject tuft = new GameObject("Grass Tuft");
                tuft.transform.SetParent(patch.transform, false);
                float angle = (i / (float)tuftCount) * Mathf.PI * 2f + Hash01(seed * 9.1f + i) * 0.8f;
                float offset = Mathf.Lerp(0.08f, 0.42f, Hash01(seed * 2.2f + i));
                tuft.transform.localPosition = new Vector3(Mathf.Cos(angle) * offset, 0f, Mathf.Sin(angle) * offset);
                tuft.transform.localRotation = Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
                float width = Mathf.Lerp(0.11f, 0.2f, Hash01(seed * 4.7f + i));
                float height = Mathf.Lerp(0.42f, 0.82f, Hash01(seed * 6.3f + i));

                MeshFilter filter = tuft.AddComponent<MeshFilter>();
                MeshRenderer renderer = tuft.AddComponent<MeshRenderer>();
                filter.sharedMesh = CreateGrassBladeMesh(width, height);
                renderer.sharedMaterial = grassMaterial;
            }
        }

        private static Mesh CreateGrassBladeMesh(float width, float height)
        {
            Mesh mesh = new Mesh { name = "Lowpoly Grass Blade Mesh" };
            Vector3[] vertices =
            {
                new Vector3(-width, 0f, 0f),
                new Vector3(width, 0f, 0f),
                new Vector3(width * 0.18f, height, 0f),
                new Vector3(0f, 0f, -width),
                new Vector3(0f, 0f, width),
                new Vector3(0f, height * 0.92f, width * 0.12f)
            };
            int[] triangles =
            {
                0, 2, 1,
                1, 2, 0,
                3, 5, 4,
                4, 5, 3
            };
            Vector2[] uvs =
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f),
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 1f)
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void PlaceVegetationPalm(Transform root, Vector3 position, int seed)
        {
            string path = Hash01(seed * 11.91f) > 0.5f
                ? "Assets/PalmTreePack/Prefabs/1/Palm_1_1.prefab"
                : "Assets/PalmTreePack/Prefabs/2/Palm_2_1.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place Vegetation Palm");
            instance.name = $"Small Vegetation Palm {seed:00}";
            instance.transform.SetParent(root);
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.Euler(0f, Hash01(seed * 6.31f) * 360f, 0f);
            instance.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.32f, Hash01(seed * 3.27f));
        }

        private static void CreateGrassClump(Transform root, Vector3 position, Material grassMaterial, int seed)
        {
            GameObject clump = new GameObject($"Low Vegetation Clump {seed:00}");
            Undo.RegisterCreatedObjectUndo(clump, "Create Vegetation Clump");
            clump.transform.SetParent(root);
            clump.transform.position = position;
            int bladeCount = 4 + Mathf.FloorToInt(Hash01(seed * 2.17f) * 4f);
            for (int i = 0; i < bladeCount; i++)
            {
                GameObject blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blade.name = "Blade";
                blade.transform.SetParent(clump.transform);
                float angle = (i / (float)bladeCount) * Mathf.PI * 2f + Hash01(seed * 5.71f + i) * 0.7f;
                float distance = Mathf.Lerp(0.12f, 0.42f, Hash01(seed * 8.91f + i));
                float height = Mathf.Lerp(0.28f, 0.72f, Hash01(seed * 12.31f + i));
                blade.transform.localPosition = new Vector3(Mathf.Cos(angle) * distance, height * 0.5f, Mathf.Sin(angle) * distance);
                blade.transform.localRotation = Quaternion.Euler(
                    Mathf.Lerp(-8f, 18f, Hash01(seed + i * 2.3f)),
                    angle * Mathf.Rad2Deg,
                    Mathf.Lerp(-16f, 16f, Hash01(seed + i * 4.1f)));
                blade.transform.localScale = new Vector3(0.055f, height, 0.055f);
                Renderer renderer = blade.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = grassMaterial;
                }

                Collider collider = blade.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }
        }

        internal static void NormalizeExistingPlacedAssetSizes()
        {
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (obj == null || !obj.scene.IsValid() || EditorUtility.IsPersistent(obj))
                {
                    continue;
                }

                string lowerName = obj.name.ToLowerInvariant();
                Vector3? scale = GetNormalizedSceneAssetScale(lowerName);
                if (!scale.HasValue)
                {
                    continue;
                }

                Undo.RecordObject(obj.transform, "Normalize Island Prop Size");
                obj.transform.localScale = scale.Value;
                EditorUtility.SetDirty(obj.transform);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            SceneView.RepaintAll();
        }

        private static Vector3? GetNormalizedSceneAssetScale(string lowerName)
        {
            if (lowerName.Contains("palm"))
            {
                return Vector3.one * 0.42f;
            }

            if (lowerName.Contains("rock"))
            {
                return Vector3.one * 0.75f;
            }

            if (lowerName.Contains("crate"))
            {
                return Vector3.one * 0.62f;
            }

            if (lowerName.Contains("barrel"))
            {
                return Vector3.one * 0.55f;
            }

            if (lowerName.Contains("log"))
            {
                return Vector3.one * 0.7f;
            }

            return null;
        }

        private static void ConfigureTerrainMaterial(Material material, Color tint, string[] albedoPaths, string[] normalPaths, float smoothness)
        {
            if (material == null)
            {
                return;
            }

            SetColor(material, tint, "_BaseColor", "_Color");
            SetFloat(material, smoothness, "_Smoothness", "_Glossiness");
            ApplyMatteSurface(material);
            ForceOpaqueSurface(material);

            Texture albedo = LoadFirstTexture(albedoPaths);
            Texture normal = LoadFirstTexture(normalPaths);
            SetTexture(material, albedo, "_BaseMap", "_MainTex");
            SetTexture(material, normal, "_BumpMap");
            SetTextureScale(material, new Vector2(12f, 12f), "_BaseMap", "_MainTex", "_BumpMap");
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
        }

        private static void ForceOpaqueSurface(Material material)
        {
            if (material == null)
            {
                return;
            }

            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
            color.a = 1f;
            SetColor(material, color, "_BaseColor", "_Color");
            SetFloat(material, 0f, "_Surface");
            SetFloat(material, 1f, "_ZWrite");
            SetFloat(material, 0f, "_AlphaClip");
            SetFloat(material, 0f, "_Cutoff");
            SetFloat(material, (float)UnityEngine.Rendering.BlendMode.One, "_SrcBlend");
            SetFloat(material, (float)UnityEngine.Rendering.BlendMode.Zero, "_DstBlend");
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_EMISSION");
            material.renderQueue = -1;
        }

        private static Texture LoadFirstTexture(params string[][] pathGroups)
        {
            foreach (string[] paths in pathGroups)
            {
                if (paths == null)
                {
                    continue;
                }

                foreach (string path in paths)
                {
                    Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static void ScatterVolcanoTransitionRocks(EditableIslandMeshCanvas canvas, Vector3 centerWorld)
        {
            string rockPath = "Assets/RockFREE/LODGroups/rock1_LOD0.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(rockPath);
            if (prefab == null)
            {
                return;
            }

            DeleteSceneObject("Volcano Transition Rocks");
            GameObject root = new GameObject("Volcano Transition Rocks");
            Undo.RegisterCreatedObjectUndo(root, "Create Volcano Transition Rocks");

            float baseRadius = Mathf.Clamp(canvas.Size * 0.062f, 54f, 88f);
            for (int i = 0; i < 11; i++)
            {
                float angle = i * 2.3999632f + 0.4f;
                float radius = Mathf.Lerp(baseRadius * 0.9f, baseRadius * 1.85f, Hash01(i * 19.17f));
                Vector3 position = centerWorld + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                if (!canvas.ContainsWorldPoint(position))
                {
                    continue;
                }

                float height = canvas.SampleHeightWorld(position);
                if (height < canvas.WaterLevel + 0.45f)
                {
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                Undo.RegisterCreatedObjectUndo(instance, "Place Volcano Rock");
                instance.name = $"Volcano Rock {i + 1:00}";
                instance.transform.SetParent(root.transform);
                instance.transform.position = new Vector3(position.x, height + 0.08f, position.z);
                instance.transform.rotation = Quaternion.Euler(0f, Hash01(i * 5.71f) * 360f, 0f);
                instance.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.9f, Hash01(i * 7.93f));
            }
        }

        private static float Hash01(float value)
        {
            return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
        }

        internal static void FixImportedAssetMaterials()
        {
            ConvertMaterialsInFolder("Assets/Crates And Barrels Pack Volume 1 - Free Version");
            ConvertMaterialsInFolder("Assets/PalmTreePack");
            ConvertMaterialsInFolder("Assets/RockFREE");
            ConvertMaterialsInFolder("Assets/A_piece_of_nature");
            ConvertMaterialsInFolder("Assets/PBR Ground Materials #1 [Dirt& Grass]");
            ConvertMaterialsInFolder("Assets/AwesomeFreeScans/WoodLogs");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static EditableIslandMeshCanvas CreateCanvas(float canvasSize, int resolution)
        {
            GameObject canvasObject = new GameObject(CanvasName);
            EditableIslandMeshCanvas canvas = canvasObject.AddComponent<EditableIslandMeshCanvas>();
            MeshRenderer renderer = canvasObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                GetMaterial("Builder Dry Sand", new Color(0.68f, 0.52f, 0.31f), 0.2f, false),
                GetMaterial("Builder Wet Sand", new Color(0.34f, 0.29f, 0.24f), 0.42f, false),
                GetMaterial("Builder Volcanic Soil", new Color(0.74f, 0.72f, 0.68f), 0.12f, false),
                GetMaterial("Builder Muted Grass", new Color(0.16f, 0.32f, 0.17f), 0.22f, false)
            };
            canvas.Initialize(resolution, canvasSize, -2.6f, 0f);
            ApplyImportedTerrainTextures(canvas);
            return canvas;
        }

        private static GameObject CreateOcean()
        {
            GameObject ocean = new GameObject(OceanName);
            ocean.transform.position = new Vector3(0f, -0.12f, 0f);
            MeshFilter filter = ocean.AddComponent<MeshFilter>();
            MeshRenderer renderer = ocean.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetOceanWaterMaterial(false);
            filter.sharedMesh = CreateOceanMesh(2200f, 136);
            ocean.AddComponent<SimpleWaveWater>();
            return ocean;
        }

        private static Material GetOceanWaterMaterial(bool preferNvjob)
        {
            if (preferNvjob)
            {
                Material nvjob = GetNvjobOceanWaterMaterial();
                if (IsUsableMaterial(nvjob))
                {
                    return nvjob;
                }

                Material aquas = AssetDatabase.LoadAssetAtPath<Material>("Assets/AQUAS-Lite/Materials/AQUAS_Lite_Water.mat")
                    ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/AQUAS-Lite/Models/Materials/WaterSimpleDaylight.mat");
                if (IsUsableMaterial(aquas))
                {
                    return aquas;
                }
            }

            return GetFallbackOceanWaterMaterial();
        }

        private static Material GetNvjobOceanWaterMaterial()
        {
            Shader shader = Shader.Find("#NVJOB/Water Shaders V2/Water Surface");
            if (shader == null || !shader.isSupported || shader.name == "Hidden/InternalErrorShader")
            {
                return null;
            }

            EnsureFolder(MaterialFolder);
            string path = $"{MaterialFolder}/NVJOB Ocean Water.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "NVJOB Ocean Water" };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture albedo0 = AssetDatabase.LoadAssetAtPath<Texture>("Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water A 0.png");
            Texture albedo1 = AssetDatabase.LoadAssetAtPath<Texture>("Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water A 1.png");
            Texture normal0 = AssetDatabase.LoadAssetAtPath<Texture>("Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water N 0.png");
            Texture normal1 = AssetDatabase.LoadAssetAtPath<Texture>("Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water N 1.png");

            SetColor(material, new Color(0.12f, 0.70f, 0.88f, 0.66f), "_AlbedoColor", "_BaseColor", "_Color");
            SetColor(material, new Color(0.78f, 0.95f, 1f, 0.86f), "_FoamColor");
            SetFloat(material, 0.82f, "_AlbedoIntensity");
            SetFloat(material, 0.28f, "_MicrowaveStrength");
            SetFloat(material, 0.42f, "_FoamGain");
            SetFloat(material, 0.68f, "_Glossiness", "_Smoothness");
            SetTexture(material, albedo0, "_AlbedoTex1", "_MainTex");
            SetTexture(material, albedo1, "_AlbedoTex2");
            SetTexture(material, normal0, "_NormalMap1", "_BumpMap");
            SetTexture(material, normal1, "_NormalMap2");
            SetTextureScale(material, new Vector2(42f, 42f), "_AlbedoTex1", "_AlbedoTex2", "_NormalMap1", "_NormalMap2", "_MainTex", "_BumpMap");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material GetFallbackOceanWaterMaterial()
        {
            Material material = GetMaterial("Builder Ocean Water", new Color(0.12f, 0.67f, 0.88f, 0.42f), 0.46f, true);
            Texture albedo = AssetDatabase.LoadAssetAtPath<Texture>("Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water A 0.png");
            Texture normal = AssetDatabase.LoadAssetAtPath<Texture>("Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water N 0.png");
            SetTexture(material, albedo, "_BaseMap", "_MainTex");
            SetTexture(material, normal, "_BumpMap");
            SetTextureScale(material, new Vector2(30f, 30f), "_BaseMap", "_MainTex", "_BumpMap");
            SetFloat(material, 0.25f, "_EnvironmentReflections");
            SetFloat(material, 0.35f, "_SpecularHighlights");
            SetFloat(material, 0.46f, "_Smoothness", "_Glossiness");
            SetFloat(material, 1f, "_Surface");
            SetFloat(material, 0f, "_ZWrite");
            SetFloat(material, (float)UnityEngine.Rendering.BlendMode.SrcAlpha, "_SrcBlend");
            SetFloat(material, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "_DstBlend");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 80;
            if (normal != null)
            {
                material.EnableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyOceanWaterMaterial(bool preferNvjob = false)
        {
            GameObject ocean = FindSceneObject(OceanName);
            if (ocean == null)
            {
                ocean = FindSceneObject("Ocean - visual only, no collider");
            }

            if (ocean == null)
            {
                ocean = CreateOcean();
            }

            ocean.SetActive(true);
            ocean.transform.position = new Vector3(0f, -0.12f, 0f);

            MeshRenderer renderer = ocean.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                return;
            }

            renderer.sharedMaterial = GetOceanWaterMaterial(preferNvjob);
            SimpleWaveWater waves = ocean.GetComponent<SimpleWaveWater>();
            if (waves == null)
            {
                waves = ocean.AddComponent<SimpleWaveWater>();
            }

            waves.Configure(0.55f, 0.045f, 1.1f, 0.24f);
            EditorUtility.SetDirty(waves);
            EditorUtility.SetDirty(renderer);
        }

        private static void ApplyReferenceOceanLook()
        {
            GameObject ocean = FindSceneObject(OceanName);
            if (ocean == null)
            {
                ocean = FindSceneObject("Ocean - visual only, no collider");
            }

            if (ocean == null)
            {
                ocean = CreateOcean();
            }

            ocean.name = OceanName;
            ocean.SetActive(true);
            ocean.transform.position = new Vector3(0f, -0.2f, 0f);

            MeshRenderer renderer = ocean.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                Material material = GetMaterial(
                    "Reference Deep Navy Ocean",
                    new Color(0.012f, 0.055f, 0.13f, 1f),
                    0.72f,
                    false);
                Texture normal = AssetDatabase.LoadAssetAtPath<Texture>(
                    "Assets/#NVJOB Water Shaders V2/Examples Water/Materials/Textures/Water N 0.png");
                SetTexture(material, null, "_BaseMap", "_MainTex");
                SetTexture(material, normal, "_BumpMap");
                SetTextureScale(material, new Vector2(34f, 34f), "_BumpMap");
                SetColor(material, new Color(0.012f, 0.055f, 0.13f, 1f), "_BaseColor", "_Color");
                SetFloat(material, 0.72f, "_Smoothness", "_Glossiness");
                SetFloat(material, 1f, "_SpecularHighlights");
                SetFloat(material, 1f, "_EnvironmentReflections");
                material.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
                material.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
                if (normal != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                }

                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                EditorUtility.SetDirty(material);
                EditorUtility.SetDirty(renderer);
            }

            SimpleWaveWater waves = ocean.GetComponent<SimpleWaveWater>();
            if (waves == null)
            {
                waves = ocean.AddComponent<SimpleWaveWater>();
            }

            waves.Configure(0.34f, 0.032f, 0.68f, 0.13f);
            EditorUtility.SetDirty(waves);
        }

        private static Mesh CreateOceanMesh(float size, int divisions)
        {
            int vertexCount = (divisions + 1) * (divisions + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[divisions * divisions * 6];
            float half = size * 0.5f;

            for (int z = 0; z <= divisions; z++)
            {
                for (int x = 0; x <= divisions; x++)
                {
                    int index = z * (divisions + 1) + x;
                    vertices[index] = new Vector3(Mathf.Lerp(-half, half, x / (float)divisions), 0f, Mathf.Lerp(-half, half, z / (float)divisions));
                    uvs[index] = new Vector2(x / (float)divisions, z / (float)divisions);
                }
            }

            int t = 0;
            for (int z = 0; z < divisions; z++)
            {
                for (int x = 0; x < divisions; x++)
                {
                    int a = z * (divisions + 1) + x;
                    int b = a + 1;
                    int c = a + divisions + 1;
                    int d = c + 1;
                    triangles[t++] = a;
                    triangles[t++] = c;
                    triangles[t++] = b;
                    triangles[t++] = b;
                    triangles[t++] = c;
                    triangles[t++] = d;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "Builder Ocean Mesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32,
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material GetMaterial(string name, Color color, float smoothness, bool transparent)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = FindLitShader();
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }

            SetColor(material, color, "_BaseColor", "_Color");
            SetFloat(material, smoothness, "_Smoothness", "_Glossiness");
            ApplyMatteSurface(material);
            if (transparent)
            {
                SetFloat(material, 1f, "_Surface");
                SetFloat(material, 0f, "_ZWrite");
                SetFloat(material, (float)UnityEngine.Rendering.BlendMode.SrcAlpha, "_SrcBlend");
                SetFloat(material, (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha, "_DstBlend");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                ForceOpaqueSurface(material);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static bool IsUsableMaterial(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported || material.shader.name == "Hidden/InternalErrorShader")
            {
                return false;
            }

            bool usingScriptableRenderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (usingScriptableRenderPipeline && !material.shader.name.StartsWith("Universal Render Pipeline/"))
            {
                return false;
            }

            return true;
        }

        private static Shader FindLitShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard")
                ?? Shader.Find("Unlit/Color");
        }

        private static Shader FindUnlitShader()
        {
            return Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color");
        }

        private static void ConvertMaterialsInFolder(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            Shader shader = FindLitShader();
            if (shader == null)
            {
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    continue;
                }

                Texture mainTexture = GetTexture(material, "_BaseMap", "_MainTex", "_AlbedoMap");
                Texture normalTexture = GetTexture(material, "_BumpMap", "_NormalMap");
                Color color = GetColor(material, "_BaseColor", "_Color");
                material.shader = shader;
                SetColor(material, color, "_BaseColor", "_Color");
                SetTexture(material, mainTexture, "_BaseMap", "_MainTex");
                SetTexture(material, normalTexture, "_BumpMap");
                if (normalTexture != null)
                {
                    material.EnableKeyword("_NORMALMAP");
                }

                SetFloat(material, 0.36f, "_Smoothness", "_Glossiness");
                ApplyMatteSurface(material);
                EditorUtility.SetDirty(material);
            }
        }

        private static void ApplyMatteToSceneVegetation()
        {
            Material safeGrass = GetLowpolyNatureGrassMaterial();
            foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid() || EditorUtility.IsPersistent(renderer))
                {
                    continue;
                }

                string lowerName = renderer.gameObject.name.ToLowerInvariant();
                MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    lowerName += " " + meshFilter.sharedMesh.name.ToLowerInvariant();
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material == null)
                    {
                        continue;
                    }

                    lowerName += " " + material.name.ToLowerInvariant();
                    if (material.shader != null)
                    {
                        lowerName += " " + material.shader.name.ToLowerInvariant();
                    }
                }

                Transform parent = renderer.transform.parent;
                while (parent != null
                    && !lowerName.Contains("grass")
                    && !lowerName.Contains("tuft")
                    && !lowerName.Contains("leaf")
                    && !lowerName.Contains("palm")
                    && !lowerName.Contains("vegetation"))
                {
                    lowerName += " " + parent.name.ToLowerInvariant();
                    parent = parent.parent;
                }

                bool isGrassLike = lowerName.Contains("grass") || lowerName.Contains("tuft") || lowerName.Contains("leaf");
                bool isVegetation = isGrassLike || lowerName.Contains("palm") || lowerName.Contains("vegetation");
                if (!isVegetation)
                {
                    continue;
                }

                if (isGrassLike)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter != null)
                    {
                        filter.sharedMesh = CreateGrassBladeMesh(0.16f, 0.68f);
                        EditorUtility.SetDirty(filter);
                    }
                }

                if (isGrassLike && safeGrass != null)
                {
                    Material[] safeMaterials = renderer.sharedMaterials;
                    for (int i = 0; i < safeMaterials.Length; i++)
                    {
                        safeMaterials[i] = safeGrass;
                    }

                    renderer.sharedMaterials = safeMaterials;
                    EditorUtility.SetDirty(renderer);
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    ApplyMatteSurface(material);
                    if (material != null)
                    {
                        SetFloat(material, 0.015f, "_Smoothness", "_Glossiness");
                        EditorUtility.SetDirty(material);
                    }
                }

                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                renderer.allowOcclusionWhenDynamic = false;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static Texture GetTexture(Material material, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    Texture texture = material.GetTexture(name);
                    if (texture != null)
                    {
                        return texture;
                    }
                }
            }

            return null;
        }

        private static Color GetColor(Material material, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    return material.GetColor(name);
                }
            }

            return Color.white;
        }

        private static void SetTexture(Material material, Texture texture, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetTexture(name, texture);
                }
            }
        }

        private static void SetTextureScale(Material material, Vector2 scale, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetTextureScale(name, scale);
                }
            }
        }

        private static void SetColor(Material material, Color color, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetColor(name, color);
                }
            }
        }

        private static void SetFloat(Material material, float value, params string[] names)
        {
            foreach (string name in names)
            {
                if (material.HasProperty(name))
                {
                    material.SetFloat(name, value);
                }
            }
        }

        private static void ApplyMatteSurface(Material material)
        {
            if (material == null)
            {
                return;
            }

            SetFloat(material, 0f, "_Metallic");
            SetFloat(material, 0f, "_SpecularHighlights");
            SetFloat(material, 0f, "_EnvironmentReflections");
            SetFloat(material, 0f, "_Emission");
            SetColor(material, Color.black, "_SpecColor");
            SetColor(material, Color.black, "_EmissionColor");
            SetTexture(material, null, "_EmissionMap");
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.DisableKeyword("_EMISSION");
        }

        private static Quaternion GetPlacementRotation(IslandEditorTool tool)
        {
            float yaw = Random.Range(0f, 360f);
            if (tool == IslandEditorTool.Barrel || tool == IslandEditorTool.Log)
            {
                return Quaternion.Euler(0f, yaw, 90f);
            }

            return Quaternion.Euler(0f, yaw, 0f);
        }

        private static Vector3 GetPlacementScale(IslandEditorTool tool)
        {
            return tool switch
            {
                IslandEditorTool.Palm => Vector3.one * Random.Range(0.36f, 0.52f),
                IslandEditorTool.Rock => Vector3.one * Random.Range(0.55f, 1.05f),
                IslandEditorTool.Crate => Vector3.one * 0.62f,
                IslandEditorTool.Barrel => Vector3.one * 0.55f,
                IslandEditorTool.Log => Vector3.one * 0.7f,
                _ => Vector3.one
            };
        }

        private static Vector3 FindSafeSpawnPoint(EditableIslandMeshCanvas canvas, out bool foundSafeGround)
        {
            Vector3 best = new Vector3(0f, 0f, 110f);
            float bestScore = float.NegativeInfinity;
            foundSafeGround = false;

            for (float radius = 70f; radius <= 175f; radius += 12f)
            {
                for (int i = 0; i < 48; i++)
                {
                    float angle = i / 48f * Mathf.PI * 2f;
                    Vector3 candidate = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                    if (!canvas.ContainsWorldPoint(candidate))
                    {
                        continue;
                    }

                    float height = canvas.SampleHeightWorld(candidate);
                    if (height < canvas.WaterLevel + 0.5f || height > 7.5f)
                    {
                        continue;
                    }

                    float forwardPreference = Mathf.InverseLerp(-1f, 1f, candidate.z / Mathf.Max(1f, radius));
                    float beachPreference = 1f - Mathf.Abs(height - 2.2f) / 5.3f;
                    float score = forwardPreference * 2f + beachPreference;
                    if (score > bestScore)
                    {
                        foundSafeGround = true;
                        bestScore = score;
                        best = candidate;
                    }
                }
            }

            best.y = canvas.SampleHeightWorld(best) + 0.2f;
            if (best.y < canvas.WaterLevel + 0.6f)
            {
                best = new Vector3(0f, canvas.WaterLevel + 2.0f, 120f);
            }

            return best;
        }

        private static AudioListener FindAnySceneAudioListener()
        {
            foreach (AudioListener listener in Resources.FindObjectsOfTypeAll<AudioListener>())
            {
                if (listener != null && listener.gameObject.scene.IsValid() && !EditorUtility.IsPersistent(listener))
                {
                    return listener;
                }
            }

            return null;
        }

        private static void EnsureLighting()
        {
            if (FindSceneObject("Sun") == null)
            {
                GameObject sun = new GameObject("Sun");
                Light light = sun.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.25f;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }

            RenderSettings.ambientLight = new Color(0.58f, 0.62f, 0.66f);
        }

        private static void ConfigureReferenceVolcanoLighting()
        {
            GameObject sun = FindSceneObject("Reference Volcano Sun")
                ?? FindSceneObject("Recovery Sun")
                ?? FindSceneObject("Sun");
            if (sun == null)
            {
                sun = new GameObject("Reference Volcano Sun");
                sun.AddComponent<Light>();
            }

            sun.name = "Reference Volcano Sun";
            Light light = sun.GetComponent<Light>();
            if (light == null)
            {
                light = sun.AddComponent<Light>();
            }

            light.type = LightType.Directional;
            light.color = new Color(0.92f, 0.95f, 1f);
            light.intensity = 2.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.92f;
            sun.transform.rotation = Quaternion.Euler(47f, -58f, 0f);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.56f, 0.64f);
            RenderSettings.reflectionIntensity = 0.38f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.008f, 0.035f, 0.09f);
            RenderSettings.fogStartDistance = 1050f;
            RenderSettings.fogEndDistance = 2350f;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 900f);

            EditorUtility.SetDirty(light);
            EditorUtility.SetDirty(sun.transform);
        }

        private static void DeleteSceneObject(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                GameObject obj = objects[i];
                if (obj != null && obj.scene.IsValid() && !EditorUtility.IsPersistent(obj) && obj.name == objectName)
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        private static void DeleteSceneObjectsWithPrefix(string objectNamePrefix)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                GameObject obj = objects[i];
                if (obj != null && obj.scene.IsValid() && !EditorUtility.IsPersistent(obj) && obj.name.StartsWith(objectNamePrefix))
                {
                    Object.DestroyImmediate(obj);
                }
            }
        }

        private static GameObject FindSceneObject(string objectName)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = objects.Length - 1; i >= 0; i--)
            {
                GameObject obj = objects[i];
                if (obj != null && obj.scene.IsValid() && !EditorUtility.IsPersistent(obj) && obj.name == objectName)
                {
                    return obj;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folder = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    public sealed class StableIslandBuilderWindow : EditorWindow
    {
        private IslandEditorTool activeTool = IslandEditorTool.None;
        private float brushRadius = 18f;
        private float brushStrength = 0.75f;
        private float flattenHeight = 1.25f;
        private float placedAssetScale = 1f;
        private float canvasSize = 980f;
        private int canvasResolution = 385;
        private Vector2 scroll;

        public static void ShowWindow()
        {
            StableIslandBuilderWindow window = GetWindow<StableIslandBuilderWindow>("Island Builder");
            window.minSize = new Vector2(430f, 650f);
        }

        private void OnEnable()
        {
            SceneView.duringSceneGui += DuringSceneGui;
            Undo.undoRedoPerformed += RebuildCanvasAfterUndoRedo;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DuringSceneGui;
            Undo.undoRedoPerformed -= RebuildCanvasAfterUndoRedo;
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("This Will Float Island Builder", EditorStyles.boldLabel);
            DrawStatus();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("UNDO  Cmd+Z", GUILayout.Height(34f)))
            {
                Undo.PerformUndo();
                RebuildCanvasAfterUndoRedo();
                ShowNotification(new GUIContent("Undo"));
            }

            if (GUILayout.Button("REDO  Shift+Cmd+Z", GUILayout.Height(34f)))
            {
                Undo.PerformRedo();
                RebuildCanvasAfterUndoRedo();
                ShowNotification(new GUIContent("Redo"));
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("1. Start", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Medium 980m", GUILayout.Height(28f)))
            {
                canvasSize = 980f;
                canvasResolution = 385;
            }

            if (GUILayout.Button("Large 1400m", GUILayout.Height(28f)))
            {
                canvasSize = 1400f;
                canvasResolution = 385;
            }

            if (GUILayout.Button("Huge 1800m", GUILayout.Height(28f)))
            {
                canvasSize = 1800f;
                canvasResolution = 513;
            }
            EditorGUILayout.EndHorizontal();

            canvasSize = EditorGUILayout.Slider("Editable Area Size", canvasSize, 560f, 1800f);
            canvasResolution = EditorGUILayout.IntSlider("Terrain Detail", canvasResolution, 177, 513);

            if (GUILayout.Button("RESET OCEAN CANVAS", GUILayout.Height(42f)))
            {
                EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.ResetOceanCanvas(canvasSize, canvasResolution);
                StableIslandBuilderUtility.FrameCanvas(canvas);
                activeTool = IslandEditorTool.DrawLand;
                ShowNotification(new GUIContent("Canvas ready. Draw land on the ocean."));
            }

            if (GUILayout.Button("CREATE BIG EDITABLE ISLAND", GUILayout.Height(42f)))
            {
                EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.ResetOceanCanvas(canvasSize, canvasResolution);
                Undo.RegisterCompleteObjectUndo(canvas, "Create Big Editable Island");
                canvas.MakeStarterIsland();
                EditorUtility.SetDirty(canvas);
                StableIslandBuilderUtility.FrameCanvas(canvas);
                activeTool = IslandEditorTool.Raise;
                ShowNotification(new GUIContent("Starter island created. Sculpt freely."));
            }

            if (GUILayout.Button("EXPAND CURRENT EDIT AREA", GUILayout.Height(36f)))
            {
                StableIslandBuilderUtility.ExpandCurrentCanvas(canvasSize, canvasResolution);
                ShowNotification(new GUIContent("Edit area expanded without stretching the island."));
            }

            if (GUILayout.Button("REFINE CURRENT ISLAND DETAIL", GUILayout.Height(34f)))
            {
                StableIslandBuilderUtility.RefineCurrentCanvasDetail(Mathf.Max(canvasResolution, 385));
                ShowNotification(new GUIContent("Island mesh detail refined."));
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Show Water", GUILayout.Height(30f)))
            {
                StableIslandBuilderUtility.SetWaterVisible(true);
            }

            if (GUILayout.Button("Hide Water", GUILayout.Height(30f)))
            {
                StableIslandBuilderUtility.SetWaterVisible(false);
            }

            if (GUILayout.Button("Frame", GUILayout.Height(30f)))
            {
                StableIslandBuilderUtility.FrameCanvas(StableIslandBuilderUtility.GetCanvas());
            }
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("SPAWN / RESET TEST PLAYER", GUILayout.Height(38f)))
            {
                GameObject player = StableIslandBuilderUtility.SpawnOrResetPlayer();
                StableIslandBuilderUtility.FramePlayer(player);
                ShowNotification(new GUIContent("Player ready. Press Play, then click Game view to lock mouse."));
            }

            if (GUILayout.Button("POLISH CURRENT VOLCANO LANDMARK", GUILayout.Height(38f)))
            {
                StableIslandBuilderUtility.PolishCurrentVolcanoLandmark();
                ShowNotification(new GUIContent("Volcano landmark polished."));
            }

            if (GUILayout.Button("APPLY TEXTURED TERRAIN MATERIALS", GUILayout.Height(34f)))
            {
                StableIslandBuilderUtility.ApplyImportedTerrainTextures(StableIslandBuilderUtility.GetCanvas());
                ShowNotification(new GUIContent("Terrain textures applied."));
            }

            if (GUILayout.Button("REPAINT CLEAN BEACH / GRASS / VOLCANO", GUILayout.Height(34f)))
            {
                StableIslandBuilderUtility.RepaintCleanIslandBiomes();
                ShowNotification(new GUIContent("Clean biome paint applied."));
            }

            if (GUILayout.Button("NORMALIZE EXISTING PROP SIZES", GUILayout.Height(34f)))
            {
                StableIslandBuilderUtility.NormalizeExistingPlacedAssetSizes();
                ShowNotification(new GUIContent("Existing props resized."));
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("2. Brush", EditorStyles.boldLabel);
            brushRadius = EditorGUILayout.Slider("Brush Size", brushRadius, 3f, 140f);
            brushStrength = EditorGUILayout.Slider("Brush Strength", brushStrength, 0.05f, 1f);
            flattenHeight = EditorGUILayout.Slider("Beach Height", flattenHeight, 0.3f, 4.5f);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("3. Island Shape", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Draw Land", IslandEditorTool.DrawLand);
            ToolButton("Erase", IslandEditorTool.Erase);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("4. Terrain", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Raise", IslandEditorTool.Raise);
            ToolButton("Lower", IslandEditorTool.Lower);
            ToolButton("Smooth", IslandEditorTool.Smooth);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            ToolButton("Flatten Beach", IslandEditorTool.Flatten);
            ToolButton("Stop", IslandEditorTool.None);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("5. Paint", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Dry Sand", IslandEditorTool.DrySand);
            ToolButton("Wet Sand", IslandEditorTool.WetSand);
            ToolButton("Volcanic", IslandEditorTool.Volcanic);
            ToolButton("Grass", IslandEditorTool.Grass);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("6. Place Assets", EditorStyles.boldLabel);
            placedAssetScale = EditorGUILayout.Slider("Placed Asset Size", placedAssetScale, 0.25f, 2.2f);
            EditorGUILayout.BeginHorizontal();
            ToolButton("Palm", IslandEditorTool.Palm);
            ToolButton("Rock", IslandEditorTool.Rock);
            ToolButton("Crate", IslandEditorTool.Crate);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            ToolButton("Barrel", IslandEditorTool.Barrel);
            ToolButton("Log", IslandEditorTool.Log);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("Scene 뷰에서 왼클릭/드래그하면 바로 편집됩니다. Option/Alt + 드래그는 화면 회전입니다. 이 빌더는 Unity Terrain이 아니라 직접 제어하는 mesh라서 물 위에 섬을 그리는 방식입니다.", MessageType.Info);
            EditorGUILayout.EndScrollView();
        }

        private static void RebuildCanvasAfterUndoRedo()
        {
            EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.GetCanvas();
            if (canvas != null)
            {
                canvas.RebuildMesh();
                EditorUtility.SetDirty(canvas);
                SceneView.RepaintAll();
            }
        }

        private void DrawStatus()
        {
            EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.GetCanvas();
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = canvas == null ? new Color(0.65f, 0.22f, 0.18f) : new Color(0.18f, 0.45f, 0.75f);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField($"Active Tool: {GetToolLabel(activeTool)}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(canvas == null ? "No canvas. Press RESET OCEAN CANVAS." : $"Canvas {canvas.Size:0}m / Water: {(StableIslandBuilderUtility.IsWaterVisible() ? "shown" : "hidden")}");
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = old;
        }

        private void ToolButton(string label, IslandEditorTool tool)
        {
            Color old = GUI.backgroundColor;
            if (activeTool == tool)
            {
                GUI.backgroundColor = new Color(0.35f, 0.64f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Height(30f)))
            {
                activeTool = tool;
                Tools.current = Tool.None;
                SceneView.RepaintAll();
            }

            GUI.backgroundColor = old;
        }

        private void DuringSceneGui(SceneView sceneView)
        {
            if (activeTool == IslandEditorTool.None)
            {
                return;
            }

            EditableIslandMeshCanvas canvas = StableIslandBuilderUtility.GetCanvas();
            if (canvas == null)
            {
                return;
            }

            Event current = Event.current;
            int controlId = GUIUtility.GetControlID(FocusType.Passive);
            if (current.type == EventType.Layout)
            {
                HandleUtility.AddDefaultControl(controlId);
            }

            if (!TryGetBrushPoint(current.mousePosition, canvas, out Vector3 point))
            {
                return;
            }

            DrawBrushPreview(point, canvas);

            if ((current.type != EventType.MouseDown && current.type != EventType.MouseDrag) || current.button != 0 || current.alt)
            {
                return;
            }

            if (IsPlaceTool(activeTool) && current.type == EventType.MouseDrag)
            {
                return;
            }

            Undo.RegisterCompleteObjectUndo(canvas, "Island Builder Edit");
            if (IsPlaceTool(activeTool))
            {
                Vector3 place = point;
                place.y = canvas.SampleHeightWorld(place) + 0.05f;
                StableIslandBuilderUtility.PlacePrefab(activeTool, place, placedAssetScale);
            }
            else
            {
                canvas.Sculpt(point, brushRadius, brushStrength, GetSculptMode(activeTool), flattenHeight, GetPaintLayer(activeTool));
                EditorUtility.SetDirty(canvas);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            current.Use();
        }

        private bool TryGetBrushPoint(Vector2 mousePosition, EditableIslandMeshCanvas canvas, out Vector3 point)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
            Plane waterPlane = new Plane(Vector3.up, new Vector3(0f, canvas.WaterLevel, 0f));
            if (waterPlane.Raycast(ray, out float enter))
            {
                point = canvas.ClampWorldPoint(ray.GetPoint(enter));
                return canvas.ContainsWorldPoint(point);
            }

            if (Physics.Raycast(ray, out RaycastHit hit, 10000f, ~0, QueryTriggerInteraction.Ignore))
            {
                point = canvas.ClampWorldPoint(hit.point);
                return canvas.ContainsWorldPoint(point);
            }

            point = Vector3.zero;
            return false;
        }

        private void DrawBrushPreview(Vector3 point, EditableIslandMeshCanvas canvas)
        {
            float previewY = Mathf.Max(canvas.WaterLevel + 0.12f, canvas.SampleHeightWorld(point) + 0.12f);
            Color color = GetToolColor(activeTool);
            Handles.color = color;
            float radius = IsPlaceTool(activeTool) ? 2.2f : brushRadius;
            Handles.DrawWireDisc(new Vector3(point.x, previewY, point.z), Vector3.up, radius);
            Handles.color = new Color(color.r, color.g, color.b, 0.08f);
            Handles.DrawSolidDisc(new Vector3(point.x, previewY, point.z), Vector3.up, radius);
        }

        private static bool IsPlaceTool(IslandEditorTool tool)
        {
            return tool == IslandEditorTool.Palm || tool == IslandEditorTool.Rock || tool == IslandEditorTool.Crate || tool == IslandEditorTool.Barrel || tool == IslandEditorTool.Log;
        }

        private static IslandSculptMode GetSculptMode(IslandEditorTool tool)
        {
            return tool switch
            {
                IslandEditorTool.DrawLand => IslandSculptMode.DrawLand,
                IslandEditorTool.Erase => IslandSculptMode.Erase,
                IslandEditorTool.Raise => IslandSculptMode.Raise,
                IslandEditorTool.Lower => IslandSculptMode.Lower,
                IslandEditorTool.Smooth => IslandSculptMode.Smooth,
                IslandEditorTool.Flatten => IslandSculptMode.Flatten,
                _ => IslandSculptMode.Paint
            };
        }

        private static int GetPaintLayer(IslandEditorTool tool)
        {
            return tool switch
            {
                IslandEditorTool.WetSand => 1,
                IslandEditorTool.Volcanic => 2,
                IslandEditorTool.Grass => 3,
                _ => 0
            };
        }

        private static Color GetToolColor(IslandEditorTool tool)
        {
            if (tool == IslandEditorTool.DrySand || tool == IslandEditorTool.WetSand || tool == IslandEditorTool.Volcanic || tool == IslandEditorTool.Grass)
            {
                return new Color(0.35f, 1f, 0.45f, 0.95f);
            }

            if (IsPlaceTool(tool))
            {
                return new Color(1f, 0.85f, 0.2f, 0.95f);
            }

            return tool switch
            {
                IslandEditorTool.Erase => new Color(1f, 0.2f, 0.16f, 0.95f),
                IslandEditorTool.Lower => new Color(0.25f, 0.55f, 1f, 0.95f),
                IslandEditorTool.Smooth => new Color(0.75f, 0.75f, 1f, 0.95f),
                IslandEditorTool.Flatten => new Color(0.95f, 0.95f, 0.55f, 0.95f),
                _ => new Color(1f, 0.72f, 0.25f, 0.95f)
            };
        }

        private static string GetToolLabel(IslandEditorTool tool)
        {
            return tool switch
            {
                IslandEditorTool.DrawLand => "Draw Land",
                IslandEditorTool.Erase => "Erase To Water",
                IslandEditorTool.Raise => "Raise",
                IslandEditorTool.Lower => "Lower",
                IslandEditorTool.Smooth => "Smooth",
                IslandEditorTool.Flatten => "Flatten Beach",
                IslandEditorTool.DrySand => "Paint Dry Sand",
                IslandEditorTool.WetSand => "Paint Wet Sand",
                IslandEditorTool.Volcanic => "Paint Volcanic",
                IslandEditorTool.Grass => "Paint Grass",
                IslandEditorTool.Palm => "Place Palm",
                IslandEditorTool.Rock => "Place Rock",
                IslandEditorTool.Crate => "Place Crate",
                IslandEditorTool.Barrel => "Place Barrel",
                IslandEditorTool.Log => "Place Log",
                _ => "None"
            };
        }
    }

    public enum IslandEditorTool
    {
        None,
        DrawLand,
        Erase,
        Raise,
        Lower,
        Smooth,
        Flatten,
        DrySand,
        WetSand,
        Volcanic,
        Grass,
        Palm,
        Rock,
        Crate,
        Barrel,
        Log
    }
}
