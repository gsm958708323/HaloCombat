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
    public sealed class SkillSummonPlayModeTests
    {
        string _directory;
        SkillProjectDocument _project;
        SkillLabRunner _runner;
        void Publish()
        {
            foreach(var item in new[]{new[]{"f6","basic-shot"},new[]{"f7","summon-gunners"}})
                File.WriteAllText(Path.Combine(_directory,item[0]+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,item[1])));
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Summon-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));Publish();
            yield return SkillLabTestScene.Load();
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;
            _runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}

        [UnityTest]
        public IEnumerator PublishedSummonLoadsItsAttackDependencyAndRendersRealBlueprintBodies()
        {
            Assert.IsTrue(_runner.CastSlot("f7",0,0).Accepted);_runner.StepLogicForTests(10);
            var pets=_runner.Snapshot.Actors.Where(a=>a.Kind=="summon").ToArray();Assert.AreEqual(2,pets.Length);
            foreach(var pet in pets){Assert.AreEqual("caster",pet.Owner);Assert.AreEqual("echo-gunner",pet.Blueprint);Assert.Greater(pet.Remaining.Value,0);}
            yield return null;
            Assert.AreEqual(2,_runner.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("summon_")));
            _runner.StepLogicForTests(70);Assert.Greater(_runner.Snapshot.Statistics.Damage,0);Assert.Greater(_runner.Snapshot.Statistics.Casts,1);
            Assert.IsTrue(_runner.Snapshot.Events.Any(e=>(e.Kind=="damage"||e.Kind=="critical")&&e.Source.StartsWith("射击分身#")));
            float damage=_runner.Snapshot.Statistics.Damage;_runner.Interrupt(true);_runner.StepLogicForTests(100);
            Assert.IsFalse(_runner.Snapshot.Actors.Any(a=>a.Kind=="summon"||a.Kind=="projectile"||a.Kind=="aoe"));Assert.AreEqual(damage,_runner.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NaturalExpiryAndManualRemovalHaveDifferentLifecycleEvents()
        {
            _runner.CastSlot("f7",0,0);_runner.StepLogicForTests(10);var id=_runner.Snapshot.Actors.First(a=>a.Kind=="summon").Id;
            _runner.RemoveEntity(id);Assert.AreEqual(1,_runner.Snapshot.Actors.Count(a=>a.Kind=="summon"));
            _runner.StepLogicForTests(120);Assert.AreEqual(0,_runner.Snapshot.Actors.Count(a=>a.Kind=="summon"));
            Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="summon-removed"));Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="summon-expired"));
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MissingAttackDependencyCannotReplaceTheWorkingWorld()
        {
            _runner.CastSlot("f7",0,0);_runner.StepLogicForTests(15);string revision=_runner.Revision;
            var publication=SkillPublicationJson.Read(File.ReadAllText(Path.Combine(_directory,"f7.json"))).Document;
            publication.Project.Skills=publication.Project.Skills.Where(s=>s.Id!="basic-shot").ToArray();
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(publication));
            Assert.IsFalse(_runner.ReloadPublished());StringAssert.Contains("skillId",_runner.LastLoadError);Assert.AreEqual(revision,_runner.Revision);
            _runner.StepLogicForTests(60);Assert.Greater(_runner.Snapshot.Statistics.Damage,0);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EachSpawnPositionChecksTheSameCoreObstacleClearance()
        {
            _project.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="spawn-wall",X=-4.8f,Z=0,Width=.2f,Depth=.2f}};Publish();
            Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);_runner.CastSlot("f7",0,0);_runner.StepLogicForTests(10);
            Assert.AreEqual(1,_runner.Snapshot.Statistics.Summons);Assert.AreEqual(1,_runner.Snapshot.Actors.Count(a=>a.Kind=="summon"));
            Assert.IsTrue(_runner.Snapshot.Events.Any(e=>e.Kind=="summon-rejected"&&e.Result=="BlockedPosition"));
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
