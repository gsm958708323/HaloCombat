using UnityEngine;
using UnityEngine.SceneManagement;

namespace Combat.Tests
{
    internal static class SkillLabTestScene
    {
        // Resolve the existing asset identity so moving a user's scene does not
        // force tests to copy it back or alter their Build Settings.
        public static AsyncOperation Load()
        {
#if UNITY_EDITOR
            string path=UnityEditor.AssetDatabase.GUIDToAssetPath("43ec1b8cec522424a98dc3f6df5e2dc9");
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync("SkillLab",LoadSceneMode.Single);
#endif
        }
    }
}
