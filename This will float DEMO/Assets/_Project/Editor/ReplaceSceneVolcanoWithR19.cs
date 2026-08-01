using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class ReplaceSceneVolcanoWithR19
{
    private const string SessionKey = "ThisWillFloat.ReplaceSceneVolcanoWithR19.20260726.v4";
    private const string PrefabPath = "Assets/_Project/Art/Volcano/Hero_R19/Prefabs/Volcano_Hero_R19.prefab";
    private const string OldVolcanoPath = "NATURAL ASSET COMPOSITION - No Buildings Or Bridges/Ultimate Stamp Volcano - Blocky Cliff Rim Details";
    private const string OldRockDressingPath = "LOCKED CRATER - STYLIZED VOLCANO ROCK DRESSING";
    private const string ReportPath = "/tmp/replace_scene_volcano_r19_report.json";

    [Serializable]
    private class ReplacementReport
    {
        public string scenePath;
        public string backupScenePath;
        public string sourcePrefabPath;
        public string oldVolcanoPath;
        public string oldRockDressingPath;
        public bool oldVolcanoDisabled;
        public bool oldRockDressingDisabled;
        public string newObjectPath;
        public float uniformScale;
        public Vector3 oldBoundsCenter;
        public Vector3 oldBoundsSize;
        public Vector3 newBoundsCenter;
        public Vector3 newBoundsSize;
        public Vector3 newWorldPosition;
        public int newVertexCount;
        public string materialName;
    }

    static ReplaceSceneVolcanoWithR19()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/This Will Float/Replace Scene Volcano With R19")]
    public static void RunFromMenu()
    {
        Run();
    }

    private static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("VOLCANO_R19_REPLACE_FAILED: active scene is invalid or unsaved");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var oldVolcano = FindByPath(OldVolcanoPath);
        var oldRockDressing = FindByPath(OldRockDressingPath);
        if (prefab == null || oldVolcano == null)
        {
            Debug.LogError($"VOLCANO_R19_REPLACE_FAILED: prefab={(prefab != null)} oldVolcano={(oldVolcano != null)}");
            return;
        }

        Bounds oldBounds = CombinedBounds(oldVolcano);

        EnsureFolder("Assets/_Project/Scenes/Backups");
        string backupPath = AssetDatabase.GenerateUniqueAssetPath(
            $"Assets/_Project/Scenes/Backups/{scene.name}_BeforeR19Volcano.unity");
        if (!EditorSceneManager.SaveScene(scene, backupPath, true))
        {
            Debug.LogError("VOLCANO_R19_REPLACE_FAILED: could not save backup scene");
            return;
        }

        Undo.SetCurrentGroupName("Replace scene volcano with Blender R19");
        int undoGroup = Undo.GetCurrentGroup();

        var existing = GameObject.Find("Volcano_Hero_R19_Scene");
        if (existing != null) Undo.DestroyObjectImmediate(existing);

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        if (instance == null)
        {
            Debug.LogError("VOLCANO_R19_REPLACE_FAILED: prefab instantiation returned null");
            return;
        }
        Undo.RegisterCreatedObjectUndo(instance, "Create Blender R19 volcano");
        instance.name = "Volcano_Hero_R19_Scene";

        Transform siblingParent = oldVolcano.transform.parent;
        if (siblingParent != null) instance.transform.SetParent(siblingParent, true);
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;

        Bounds nativeBounds = CombinedBounds(instance);
        float scaleX = oldBounds.size.x / Mathf.Max(nativeBounds.size.x, 0.0001f);
        float scaleZ = oldBounds.size.z / Mathf.Max(nativeBounds.size.z, 0.0001f);
        float uniformScale = Mathf.Max(scaleX, scaleZ);
        instance.transform.localScale = Vector3.one * uniformScale;

        Bounds scaledBounds = CombinedBounds(instance);
        Vector3 delta = new Vector3(
            oldBounds.center.x - scaledBounds.center.x,
            oldBounds.min.y - scaledBounds.min.y,
            oldBounds.center.z - scaledBounds.center.z);
        instance.transform.position += delta;

        Undo.RecordObject(oldVolcano, "Disable old scene volcano");
        oldVolcano.SetActive(false);
        oldVolcano.name = "Ultimate Stamp Volcano - OLD DISABLED R19 BACKUP";

        bool rockDressingDisabled = false;
        if (oldRockDressing != null)
        {
            Undo.RecordObject(oldRockDressing, "Disable old crater rock dressing");
            oldRockDressing.SetActive(false);
            oldRockDressing.name = "LOCKED CRATER ROCK DRESSING - OLD DISABLED R19 BACKUP";
            rockDressingDisabled = true;
        }

        Bounds finalBounds = CombinedBounds(instance);
        var mf = instance.GetComponentInChildren<MeshFilter>(true);
        var mr = instance.GetComponentInChildren<MeshRenderer>(true);
        var report = new ReplacementReport
        {
            scenePath = scene.path,
            backupScenePath = backupPath,
            sourcePrefabPath = PrefabPath,
            oldVolcanoPath = OldVolcanoPath,
            oldRockDressingPath = OldRockDressingPath,
            oldVolcanoDisabled = !oldVolcano.activeSelf,
            oldRockDressingDisabled = rockDressingDisabled,
            newObjectPath = FullPath(instance.transform),
            uniformScale = uniformScale,
            oldBoundsCenter = oldBounds.center,
            oldBoundsSize = oldBounds.size,
            newBoundsCenter = finalBounds.center,
            newBoundsSize = finalBounds.size,
            newWorldPosition = instance.transform.position,
            newVertexCount = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0,
            materialName = mr != null && mr.sharedMaterial != null ? mr.sharedMaterial.name : ""
        };

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("VOLCANO_R19_REPLACE_FAILED: replacement made but active scene save failed");
            return;
        }

        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Selection.activeGameObject = instance;
        EditorGUIUtility.PingObject(instance);
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"VOLCANO_R19_SCENE_REPLACE_COMPLETE: {ReportPath}");
    }

    private static Bounds CombinedBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException($"No renderers under {go.name}");
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static GameObject FindByPath(string path)
    {
        string[] parts = path.Split('/');
        GameObject current = SceneManager.GetActiveScene().GetRootGameObjects()
            .FirstOrDefault(go => go.name == parts[0]);
        if (current == null) return null;
        for (int i = 1; i < parts.Length; i++)
        {
            Transform child = current.transform.Find(parts[i]);
            if (child == null) return null;
            current = child.gameObject;
        }
        return current;
    }

    private static string FullPath(Transform t)
    {
        var names = new Stack<string>();
        while (t != null) { names.Push(t.name); t = t.parent; }
        return string.Join("/", names);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
