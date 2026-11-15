using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    public sealed class SkillBuffPlayModeTests
    {
        string _directory;
        SkillProjectDocument _project;
        SkillLabRunner _runner;
        void Publish(string second="cleanse-burn")
        {
            foreach(var item in new[]{new[]{"f6","burn-shot"},new[]{"f7",second}})
                File.WriteAllText(Path.Combine(_directory,item[0]+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,item[1])));
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Buff-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));Publish();
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;
            _runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}

        [UnityTest]
        public IEnumerator PublishedLifecycleGraphChangesRealHealthAndRestoresLeasedStateAtExpiry()
        {
            _runner.CastSlot("f6",0,0);_runner.StepLogicForTests(40);var target=_runner.Snapshot.Actors.First(a=>a.Name=="target-1");
            Assert.AreEqual(1,target.Buffs.Length);Assert.AreEqual(2.5f,target.Attributes["MoveSpeed"]);Assert.AreEqual(1,target.Tags[3101]);
            Assert.AreEqual(990,target.Hp);_runner.StepLogicForTests(80);target=_runner.Snapshot.Actors.First(a=>a.Name=="target-1");
            Assert.AreEqual(960,target.Hp);Assert.AreEqual(0,target.Buffs.Length);Assert.AreEqual(5,target.Attributes["MoveSpeed"]);Assert.IsFalse(target.Tags.ContainsKey(3101));
            Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="buff-expired"));Assert.AreEqual(40,_runner.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PublishedDispelAndManualInstanceRemovalStopThePeriodicDamage()
        {
            _runner.CastSlot("f6",0,0);_runner.StepLogicForTests(40);Assert.IsTrue(_runner.CastSlot("f7",0,0).Accepted);_runner.StepLogicForTests(100);
            Assert.AreEqual(10,_runner.Snapshot.Statistics.Damage);Assert.IsFalse(_runner.Snapshot.Events.Any(e=>e.Kind=="buff-expired"));
            _runner.ReloadPublished();_runner.CastSlot("f6",0,0);_runner.StepLogicForTests(40);var target=_runner.Snapshot.Actors.First(a=>a.Name=="target-1");
            Assert.IsTrue(_runner.RemoveBuff(target.Id,target.Buffs[0].InstanceId));_runner.StepLogicForTests(80);Assert.AreEqual(10,_runner.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator AuraExitCleansOnlyThatAreaSourceWhileAnotherInstanceRemains()
        {
            Publish("slow-aura");_runner.ReloadPublished();_runner.CastSlot("f7",0,0);_runner.StepLogicForTests(40);_runner.CastSlot("f7",0,0);_runner.StepLogicForTests(10);
            var target=_runner.Snapshot.Actors.First(a=>a.Name=="target-1");Assert.AreEqual(2,target.Buffs.Length);Assert.AreEqual(1.25f,target.Attributes["MoveSpeed"]);
            _runner.StepLogicForTests(10);target=_runner.Snapshot.Actors.First(a=>a.Name=="target-1");Assert.AreEqual(1,target.Buffs.Length);Assert.AreEqual(2.5f,target.Attributes["MoveSpeed"]);
            _runner.StepLogicForTests(60);target=_runner.Snapshot.Actors.First(a=>a.Name=="target-1");Assert.AreEqual(0,target.Buffs.Length);Assert.AreEqual(5,target.Attributes["MoveSpeed"]);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MissingBuffOrVersionConflictKeepsTheWorkingSnapshotAndPendingPeriods()
        {
            _runner.CastSlot("f6",0,0);_runner.StepLogicForTests(40);string revision=_runner.Revision;
            var document=SkillPublicationJson.Read(File.ReadAllText(Path.Combine(_directory,"f7.json"))).Document;document.Project.Buffs=Array.Empty<BuffDocument>();
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(document));Assert.IsFalse(_runner.ReloadPublished());StringAssert.Contains("buff",_runner.LastLoadError.ToLowerInvariant());Assert.AreEqual(revision,_runner.Revision);
            _runner.StepLogicForTests(80);Assert.AreEqual(40,_runner.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
