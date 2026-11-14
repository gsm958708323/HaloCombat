using System.Collections;
using Combat.Core;
using Combat.TrainingCamp;
using Combat.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;

namespace Combat.Tests
{
    public sealed class RuntimeContentPlayModeTests
    {
        [UnityTest]
        public IEnumerator DiskJsonDrivesRealInputTimelineDamageAndActorViews()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/V2Melee.unity",LoadSceneMode.Single);yield return null;
            var runner=Object.FindFirstObjectByType<CombatRunner>();Assert.IsNotNull(runner);runner.SetLessonAuto(false);
            Assert.IsNull(runner.LastLoadError);Assert.IsTrue(runner.TryGetPlayer(out var player));
            float startX=player.GetComp<TransformComp>().Position.X;
            float damage=0;runner.World.Events.Subscribe<EvDamage>(e=>damage+=e.Amount);
            LogAssert.Expect(LogType.Log,new Regex("^DMG 10"));LogAssert.Expect(LogType.Log,new Regex("^DMG 10"));
            runner.InjectInput(InputToken.Attack);runner.StepLogicForTests(40);
            Assert.Greater(damage,0);Assert.IsNotNull(GameObject.Find("fighter_"+player.Id.Index));
            Assert.AreEqual(.6f,player.GetComp<TransformComp>().Position.X-startX,.0001f);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator MissingFileCannotReplaceTheWorkingWorldOrItsPendingTimeline()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/V2Melee.unity",LoadSceneMode.Single);yield return null;
            var runner=Object.FindFirstObjectByType<CombatRunner>();runner.SetLessonAuto(false);var original=runner.World;
            runner.InjectInput(InputToken.Attack);runner.StepLogicForTests(4);
            runner.Configure(CombatRunner.SceneKind.V2,"Combat/missing-runtime-test.json");runner.BuildWorld();
            Assert.AreSame(original,runner.World);StringAssert.Contains("missing-runtime-test",runner.LastLoadError);
            runner.TryGetPlayer(out var player);Assert.IsTrue(player.GetComp<SkillDirectorComp>().IsPlaying);
            LogAssert.Expect(LogType.Log,new Regex("^DMG 10"));LogAssert.Expect(LogType.Log,new Regex("^DMG 10"));
            runner.StepLogicForTests(35);Assert.Greater(runner.World.Time.Frame,30);
            runner.Configure(CombatRunner.SceneKind.V2,UnityRuntimeContent.DefaultFile);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator TrainingCampReadsTheSameJsonAndResetRebuildsFreshActors()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/TrainingCamp.unity",LoadSceneMode.Single);yield return null;
            var runner=Object.FindFirstObjectByType<TrainingCampRunner>();Assert.IsNotNull(runner);var previous=runner.World;
            Assert.IsTrue(previous.TryGetActor(runner.PlayerId,out var before));runner.ResetWorld();
            Assert.AreNotSame(previous,runner.World);Assert.IsFalse(before.IsActive);
            Assert.IsTrue(runner.World.TryGetActor(runner.PlayerId,out var player));Assert.IsNotNull(player.GetComp<RuntimePlayerDriverComp>());
            LogAssert.NoUnexpectedReceived();
        }
    }
}
