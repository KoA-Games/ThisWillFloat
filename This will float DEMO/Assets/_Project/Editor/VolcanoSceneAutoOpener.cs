using ThisWillFloat.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ThisWillFloat.Demo.EditorTools
{
    public static class VolcanoSceneAutoOpener
    {
        private const string CurrentVolcanoScenePath =
            "Assets/_Project/Scenes/VolcanoFinalPolish.unity";

        [MenuItem("This Will Float DEMO/0. OPEN CURRENT VOLCANO ISLAND NOW")]
        public static void OpenVolcanoSceneNow()
        {
            OpenAndFrame(CurrentVolcanoScenePath);
        }

        private static void OpenAndFrame(string scenePath)
        {
            SceneAsset scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            if (scene == null)
            {
                Debug.LogError("VOLCANO SCENE NOT FOUND: " + scenePath);
                return;
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EditableIslandMeshCanvas canvas = Object.FindAnyObjectByType<EditableIslandMeshCanvas>();
            if (canvas != null)
            {
                Selection.activeGameObject = canvas.gameObject;
            }

            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.LookAt(
                    new Vector3(35f, 72f, 75f),
                    Quaternion.Euler(43f, -41f, 0f),
                    1080f);
                sceneView.Repaint();
            }

            Debug.Log("VOLCANO SCENE OPENED: " + scenePath);
        }
    }
}
