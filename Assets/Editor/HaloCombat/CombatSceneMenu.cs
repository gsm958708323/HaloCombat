using Combat.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Combat.EditorTools
{
    [InitializeOnLoad]
    internal static class CombatSceneMenu
    {
        static CombatSceneMenu()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) SessionState.EraseBool(ArenaBootstrap.EditorModeKey);
            };
        }

        [MenuItem("Combat/打开场景/Arena 正式", false, 10)]
        static void OpenArena() => Open(ArenaSceneBuilder.Path);

        [MenuItem("Combat/打开场景/Arena 木桩", false, 11)]
        static void PlayTraining() => Open(ArenaSceneBuilder.Path, training: true);

        [MenuItem("Combat/打开场景/SkillLab", false, 13)]
        static void OpenSkillLab() => Open("Assets/Scenes/SkillLab.unity");

        static void Open(string path, bool training = false)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SessionState.EraseBool(ArenaBootstrap.EditorModeKey);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (!training) return;
            SessionState.SetBool(ArenaBootstrap.EditorModeKey, true);
            EditorApplication.isPlaying = true;
        }
    }
}
