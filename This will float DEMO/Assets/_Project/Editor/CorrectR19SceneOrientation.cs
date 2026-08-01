using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CorrectR19SceneOrientation
{
    private const string ReportPath = "/tmp/correct_r19_scene_orientation_report.json";

    [Serializable]
    private class Report
    {
        public string scenePath;
        public string objectPath;
        public Vector3 rotation;
        public float uniformScale;
        public Vector3 oldBoundsSize;
        public Vector3 correctedBoundsCenter;
        public Vector3 correctedBoundsSize;
        public Vector3 correctedPosition;
    }

    [MenuItem("Tools/This Will Float/Correct R19 Scene Orientation And Scale")]
    public static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        var instance = GameObject.Find("Volcano_Hero_R19_Scene");
        var old = FindInactiveByName("Ultimate Stamp Volcano - OLD DISABLED R19 BACKUP");
        if (instance == null || old == null)
        {
            Debug.LogError($"VOLCANO_R19_ORIENTATION_FAILED: instance={(instance != null)} oldBackup={(old != null)}");
            return;
        }

        Bounds oldBounds = CombinedBounds(old);
        Undo.RecordObject(instance.transform, "Correct Blender R19 axis and scale");

        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
        instance.transform.localScale = Vector3.one;

        Bounds nativeBounds = CombinedBounds(instance);
        float scaleX = oldBounds.size.x / Mathf.Max(nativeBounds.size.x, 0.0001f);
        float scaleZ = oldBounds.size.z / Mathf.Max(nativeBounds.size.z, 0.0001f);
        float uniformScale = Mathf.Max(scaleX, scaleZ);
        instance.transform.localScale = Vector3.one * uniformScale;

        Bounds scaledBounds = CombinedBounds(instance);
        instance.transform.position += new Vector3(
            oldBounds.center.x - scaledBounds.center.x,
            oldBounds.min.y - scaledBounds.min.y,
            oldBounds.center.z - scaledBounds.center.z);

        Bounds corrected = CombinedBounds(instance);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        var report = new Report
        {
            scenePath = scene.path,
            objectPath = FullPath(instance.transform),
            rotation = instance.transform.eulerAngles,
            uniformScale = uniformScale,
            oldBoundsSize = oldBounds.size,
            correctedBoundsCenter = corrected.center,
            correctedBoundsSize = corrected.size,
            correctedPosition = instance.transform.position
        };
        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Selection.activeGameObject = instance;
        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
        Debug.Log($"VOLCANO_R19_ORIENTATION_COMPLETE: {ReportPath}");
    }

    private static GameObject FindInactiveByName(string name)
    {
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    private static Bounds CombinedBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) throw new InvalidOperationException($"No renderers under {go.name}");
        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    private static string FullPath(Transform t)
    {
        var names = new Stack<string>();
        while (t != null) { names.Push(t.name); t = t.parent; }
        return string.Join("/", names);
    }
}
