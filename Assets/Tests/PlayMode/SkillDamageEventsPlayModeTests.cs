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
    public sealed class SkillDamageEventsPlayModeTests
    {
        string _directory;
        SkillProjectDocument _project;
        SkillLabRunner _runner;
        void Publish()
        {
            foreach(var item in new[]{new[]{"f6","event-shot"},new[]{"f7","shield-self"}})
                File.WriteAllText(Path.Combine(_directory,item[0]+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,item[1])));
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-DamageEvents-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            _project.Preview.Targets=_project.Preview.Targets.Take(1).ToArray();var target=_project.Preview.Targets[0];target.Hp=20;target.Shield=30;target.Def=20;
            Publish();yield return SkillLabTestScene.Load();
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;
            _runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}

        [UnityTest]
        public IEnumerator PublishedCriticalKillShowsExactShieldHealthOverkillAndOnlyOneEventChain()
        {
            _runner.CastSlot("f6",0,0);_runner.StepLogicForTests(100);var snapshot=_runner.Snapshot;
            Assert.AreEqual(50,snapshot.Statistics.Damage);Assert.AreEqual(1,snapshot.Statistics.Hits);
            var damage=snapshot.Events.Single(e=>e.Kind=="critical");Assert.AreEqual(120,damage.Damage.Final);Assert.AreEqual(30,damage.Damage.Absorbed);
            Assert.AreEqual(20,damage.Damage.HpLost);Assert.AreEqual(70,damage.Damage.Overkill);Assert.Greater(damage.HitIndex,0);
            foreach(string kind in new[]{"skill-hit","skill-critical","skill-kill","skill-completed","skill-projectileend"})
                Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind==kind),kind);
            Assert.IsTrue(snapshot.Actors.First(a=>a.Name=="target-1").Dead);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ShieldEffectAndPureShieldDamageUseActualActorResources()
        {
            _runner.CastSlot("f7",0,0);_runner.StepLogicForTests(30);
            Assert.AreEqual(200,_runner.Snapshot.Actors.Single(a=>a.Kind=="caster").Attributes["Shield"]);
            _project.Preview.Targets[0].Shield=1000;Publish();_runner.ReloadPublished();_runner.CastSlot("f6",0,0);_runner.StepLogicForTests(100);
            Assert.AreEqual(120,_runner.Snapshot.Statistics.Damage);Assert.IsFalse(_runner.Snapshot.Events.Any(e=>e.Kind=="skill-hit"||e.Kind=="skill-kill"));
            Assert.AreEqual(0,_runner.Snapshot.Events.Single(e=>e.Kind=="critical").Damage.HpLost);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InvalidEventPublicationKeepsTheInFlightCastAndItsHandlers()
        {
            _runner.CastSlot("f6",0,0);_runner.StepLogicForTests(10);var projectile=_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile");
            var bundle=SkillPublicationJson.Read(File.ReadAllText(Path.Combine(_directory,"f6.json"))).Document;
            bundle.Project.Skills[0].Nodes.First(n=>n.Kind=="damage").Parameters["damageType"]="invalid";
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(bundle));
            Assert.IsFalse(_runner.ReloadPublished());Assert.AreEqual(projectile.CastId,_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile").CastId);
            _runner.StepLogicForTests(100);Assert.AreEqual(50,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="skill-kill"));
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
