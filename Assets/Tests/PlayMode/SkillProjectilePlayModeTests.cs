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
    public sealed class SkillProjectilePlayModeTests
    {
        string _directory;
        SkillProjectDocument _project;
        SkillLabRunner _runner;
        SkillNodeDocument Node(string skill,string kind)=>_project.Skills.First(s=>s.Id==skill).Nodes.First(n=>n.Kind==kind);
        void Publish(string first="chain-shot",string second="split-shot")
        {
            foreach(var pair in new[]{new[]{"f6",first},new[]{"f7",second}})
                File.WriteAllText(Path.Combine(_directory,pair[0]+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,pair[1])));
        }
        void Targets(params (float x,float z)[] positions)=>_project.Preview.Targets=positions.Select((v,i)=>new PreviewActorDocument{Id="target-"+(i+1),X=v.x,Z=v.z,Hp=1000}).ToArray();
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Projectiles-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            _project.Preview.Caster.X=0;_project.Preview.Caster.Z=0;_project.Preview.Recovery=0;Targets((2,0),(4,0),(6,0));
            foreach(string skill in new[]{"chain-shot","split-shot","spread-shot","fan-channel"})
            {
                Node(skill,"cast").Parameters["delay"]=0;
                Node(skill,"projectile").Parameters["speed"]=10;Node(skill,"projectile").Parameters["radius"]=.1f;
            }
            Node("chain-shot","chain").Parameters["count"]=2;
            var split=Node("split-shot","split").Parameters;split["count"]=2;split["spread"]=0;split["generations"]=2;
            Publish();
            yield return SkillLabTestScene.Load();
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;
            _runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}

        [UnityTest]
        public IEnumerator PublishedChainAndSplitExecuteRealDamageAndCleanTheirBodies()
        {
            Assert.IsTrue(_runner.CastSlot("f6",10,0).Accepted);_runner.StepLogicForTests(120);
            Assert.AreEqual(240,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(2,_runner.Snapshot.Statistics.Chains);Assert.AreEqual(1,_runner.Snapshot.Statistics.Projectiles);
            Assert.IsFalse(_runner.Snapshot.Actors.Any(a=>a.Kind=="projectile"));
            _runner.ReloadPublished();_runner.CastSlot("f7",10,0);for(int i=0;i<20&&_runner.Snapshot.Statistics.Splits==0;i++)_runner.StepLogicForTests(1);_runner.StepLogicForTests(1);yield return null;
            var children=_runner.Snapshot.Actors.Where(a=>a.Kind=="projectile").ToArray();Assert.AreEqual(2,children.Length);Assert.IsTrue(children.All(a=>a.Generation==1&&a.CastId>0));
            _runner.StepLogicForTests(120);Assert.AreEqual(240,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(7,_runner.Snapshot.Statistics.Projectiles);
            Assert.AreEqual(3,_runner.Snapshot.Statistics.Splits);Assert.IsFalse(_runner.Snapshot.Actors.Any(a=>a.Kind=="projectile"));
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PublishedSpreadAndHeldFanUseRealDirectionsAndReleaseStopsEmission()
        {
            Targets((4,0),(3.4641016f,2),(3.4641016f,-2));
            Node("spread-shot","projectile").Parameters["count"]=3;Node("spread-shot","projectile").Parameters["spread"]=60;
            Node("fan-channel","cast").Parameters["energyPerSecond"]=0;Publish("spread-shot","fan-channel");_runner.ReloadPublished();
            _runner.CastSlot("f6",10,0);_runner.StepLogicForTests(80);Assert.AreEqual(240,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(3,_runner.Snapshot.Statistics.Projectiles);
            _runner.ReloadPublished();_runner.CastSlot("f7",10,0);_runner.StepLogicForTests(25);Assert.IsTrue(_runner.Snapshot.Channeling);
            _runner.ReleaseCast();int count=_runner.Snapshot.Statistics.Projectiles;_runner.StepLogicForTests(50);
            Assert.AreEqual(count,_runner.Snapshot.Statistics.Projectiles);Assert.AreEqual(0,count%5);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InvalidSplitPublicationPreservesExistingBodiesAndCastIdentity()
        {
            _runner.CastSlot("f7",10,0);_runner.StepLogicForTests(4);var before=_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile");
            var bundle=SkillPublicationJson.Read(File.ReadAllText(Path.Combine(_directory,"f7.json"))).Document;
            bundle.Project.Skills[0].Nodes.First(n=>n.Kind=="split").Parameters["generations"]=9;
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(bundle));
            Assert.IsFalse(_runner.ReloadPublished());var after=_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile");
            Assert.AreEqual(before.Id,after.Id);Assert.AreEqual(before.CastId,after.CastId);
            _runner.StepLogicForTests(120);Assert.AreEqual(240,_runner.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
