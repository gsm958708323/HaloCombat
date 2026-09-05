using System.Collections;
using Combat.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    // Unity is only used as a presentation/scene smoke check. Combat behavior
    // is covered by Combat.Demos SeasonThreeLessonDemo in pure C#.
    public sealed class HaloCombatPlayModeTests
    {
        static readonly string[] ScenePaths =
        {
            "Assets/Scenes/HaloCombat/V1Motor.unity",
            "Assets/Scenes/HaloCombat/V2Melee.unity",
            "Assets/Scenes/HaloCombat/V3ProjectileAoe.unity",
            "Assets/Scenes/HaloCombat/V4AiSummon.unity"
        };

        [UnityTest]
        public IEnumerator SeasonThreeScenes_LoadWithoutMissingScripts()
        {
            for (int i = 0; i < ScenePaths.Length; i++)
            {
                yield return SceneManager.LoadSceneAsync(ScenePaths[i], LoadSceneMode.Single);
                yield return null;
                AssertNoMissingScripts();
                var runner = Object.FindFirstObjectByType<CombatRunner>();
                Assert.That(runner, Is.Not.Null, ScenePaths[i]);
                Assert.That(runner.IsUsingSoDatabase, Is.True, ScenePaths[i]);
                Assert.That(runner.Database, Is.Not.Null, ScenePaths[i]);
                // Assert.That(runner.Lesson, Is.Not.Null, ScenePaths[i]);
                Assert.That(Object.FindFirstObjectByType<CombatLessonDirector>(), Is.Not.Null, ScenePaths[i]);
                Assert.That(Object.FindFirstObjectByType<CombatLessonVfx>(), Is.Not.Null, ScenePaths[i]);
            }
        }

        static void AssertNoMissingScripts()
        {
            var behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
                Assert.That(behaviours[i], Is.Not.Null, "Scene contains a missing MonoBehaviour script");
        }
    }
}

