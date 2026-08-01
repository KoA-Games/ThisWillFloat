using UnityEngine;
using UnityEngine.PostProcessing;
using System.IO;

namespace UnityEditor.PostProcessing
{
    public class PostProcessingFactory
    {
        [MenuItem("Assets/Create/Post-Processing Profile", priority = 201)]
        static void MenuCreatePostProcessingProfile()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/New Post-Processing Profile.asset");
            PostProcessingProfile profile = CreatePostProcessingProfileAtPath(path);
            AssetDatabase.SaveAssets();
            ProjectWindowUtil.ShowCreatedAsset(profile);
        }

        internal static PostProcessingProfile CreatePostProcessingProfileAtPath(string path)
        {
            var profile = ScriptableObject.CreateInstance<PostProcessingProfile>();
            profile.name = Path.GetFileName(path);
            profile.fog.enabled = true;
            AssetDatabase.CreateAsset(profile, path);
            return profile;
        }
    }
}
