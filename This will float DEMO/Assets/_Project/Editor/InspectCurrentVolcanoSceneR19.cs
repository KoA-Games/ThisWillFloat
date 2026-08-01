using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InspectCurrentVolcanoSceneR19
{
    private const string SessionKey = "ThisWillFloat.InspectCurrentVolcanoSceneR19.20260726.v3";
    private const string ReportPath = "/tmp/current_unity_volcano_scene_report.json";

    [Serializable]
    private class Candidate
    {
        public string path;
        public string name;
        public bool activeInHierarchy;
        public string prefabPath;
        public string meshName;
        public int vertexCount;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 lossyScale;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
    }

    [Serializable]
    private class Report
    {
        public string sceneName;
        public string scenePath;
        public bool sceneDirty;
        public List<Candidate> candidates = new List<Candidate>();
    }

    static InspectCurrentVolcanoSceneR19()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Run;
    }

    private static void Run()
    {
        var scene = SceneManager.GetActiveScene();
        var report = new Report
        {
            sceneName = scene.name,
            scenePath = scene.path,
            sceneDirty = scene.isDirty
        };

        foreach (var root in scene.GetRootGameObjects())
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            var go = t.gameObject;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) continue;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

            string lower = go.name.ToLowerInvariant();
            bool named = lower.Contains("volcano") || lower.Contains("mountain") || lower.Contains("crater");
            bool large = bounds.size.x > 40f && bounds.size.z > 40f && bounds.size.y > 15f;
            if (!named && !large) continue;

            var mf = go.GetComponentInChildren<MeshFilter>(true);
            report.candidates.Add(new Candidate
            {
                path = FullPath(t),
                name = go.name,
                activeInHierarchy = go.activeInHierarchy,
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go),
                meshName = mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "",
                vertexCount = mf != null && mf.sharedMesh != null ? mf.sharedMesh.vertexCount : 0,
                position = t.position,
                rotation = t.eulerAngles,
                lossyScale = t.lossyScale,
                boundsCenter = bounds.center,
                boundsSize = bounds.size
            });
        }

        report.candidates = report.candidates
            .OrderByDescending(c => c.activeInHierarchy)
            .ThenByDescending(c => c.boundsSize.x * c.boundsSize.z)
            .Take(80)
            .ToList();

        File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
        Debug.Log($"VOLCANO_SCENE_INSPECTION_COMPLETE: {ReportPath} candidates={report.candidates.Count}");
    }

    private static string FullPath(Transform t)
    {
        var names = new Stack<string>();
        while (t != null)
        {
            names.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", names);
    }
}
