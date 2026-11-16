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
    public sealed class SkillProjectileMotionPlayModeTests
    {
        string _directory;
        SkillProjectDocument _project;
        SkillLabRunner _runner;
        SkillNodeDocument Node(string skill,string kind)=>_project.Skills.First(s=>s.Id==skill).Nodes.First(n=>n.Kind==kind);
        void Publish(string first="accelerating-shot",string second="bouncing-shot")
        {
            foreach(var pair in new[]{new[]{"f6",first},new[]{"f7",second}})
                File.WriteAllText(Path.Combine(_directory,pair[0]+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,pair[1])));
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Motion-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            _project.Preview.Caster.X=0;_project.Preview.Caster.Z=0;_project.Preview.Recovery=0;_project.Preview.Targets=Array.Empty<PreviewActorDocument>();
            foreach(string id in new[]{"accelerating-shot","bouncing-shot","returning-shot","ricochet-shot"})
            {
                Node(id,"cast").Parameters["delay"]=0;var parameters=Node(id,"projectile").Parameters;
                parameters["range"]=100;parameters["radius"]=.1f;
                var graph=_project.Skills.First(s=>s.Id==id);var pierce=graph.Nodes.FirstOrDefault(n=>n.Kind=="pierce");
                if(pierce!=null)pierce.Parameters["maxHits"]=0;
                else
                {
                    graph.Nodes=graph.Nodes.Concat(new[]{new SkillNodeDocument{Id="pierce",Kind="pierce",Parameters=Newtonsoft.Json.Linq.JObject.FromObject(new{maxHits=0})}}).ToArray();
                    graph.Edges=graph.Edges.Concat(new[]{new SkillEdgeDocument{Id="hit-pierce",Source=Node(id,"projectile").Id,SourcePort="hit",Target="pierce",TargetPort="execute",Order=2}}).ToArray();
                }
            }
            var accel=Node("accelerating-shot","projectile").Parameters;accel["speed"]=2;accel["acceleration"]=4;accel["maxSpeed"]=6;
            var bounce=Node("bouncing-shot","projectile").Parameters;bounce["speed"]=4;bounce["launchHeight"]=0;bounce["verticalSpeed"]=4;bounce["gravity"]=8;bounce["restitution"]=.5f;bounce["groundBounces"]=1;
            Publish();yield return SkillLabTestScene.Load();
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;
            _runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}

        [UnityTest]
        public IEnumerator PublishedAccelerationAndBounceAdvanceCoreCoordinatesAndVisibleHeight()
        {
            _runner.CastSlot("f6",10,0);_runner.StepLogicForTests(50);
            var actor=_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile");
            Assert.AreEqual(4.4f,actor.X,.001f);Assert.AreEqual(6,actor.Speed.Value,.001f);
            _runner.ReloadPublished();_runner.CastSlot("f7",10,0);_runner.StepLogicForTests(25);
            actor=_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile");Assert.AreEqual(1,actor.Y,.001f);
            yield return null;yield return null;
            var visual=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Single(t=>t.name.StartsWith("projectile_"));
            Assert.AreEqual(actor.Y+.45f,visual.position.y,.001f);
            _runner.StepLogicForTests(60);Assert.AreEqual("Ground",_runner.Snapshot.Events.Single(e=>e.Kind=="projectile-end").Result);
            Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="projectile-ground-bounce"));Assert.IsFalse(_runner.Snapshot.Actors.Any(a=>a.Kind=="projectile"));
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PublishedReturnCatchesTheOwnerAndWallReflectionsRespectTheirQuota()
        {
            var returned=Node("returning-shot","projectile").Parameters;returned["speed"]=4;returned["returnAfter"]=.5f;returned["returnSpeed"]=8;returned["catchRadius"]=.1f;
            Publish("accelerating-shot","returning-shot");_runner.ReloadPublished();_runner.CastSlot("f7",10,0);_runner.StepLogicForTests(50);
            Assert.AreEqual("Returned",_runner.Snapshot.Events.Single(e=>e.Kind=="projectile-end").Result);Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="projectile-returning"));
            _project.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="right",X=2.5f,Z=0,Width=1,Depth=8},new PreviewObstacleDocument{Id="left",X=-2.5f,Z=0,Width=1,Depth=8}};
            var ricochet=Node("ricochet-shot","projectile").Parameters;ricochet["speed"]=4;ricochet["wallBounces"]=2;
            Publish("ricochet-shot","returning-shot");_runner.ReloadPublished();_runner.CastSlot("f6",10,0);_runner.StepLogicForTests(50);
            Assert.AreEqual(-.6001f,_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile").X,.002f);
            _runner.StepLogicForTests(100);Assert.AreEqual(2,_runner.Snapshot.Events.Count(e=>e.Kind=="projectile-wall-bounce"));
            Assert.AreEqual("Wall",_runner.Snapshot.Events.Single(e=>e.Kind=="projectile-end").Result);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InvalidMotionPublicationPreservesTheExistingBallAndItsVerticalState()
        {
            _runner.CastSlot("f7",10,0);_runner.StepLogicForTests(25);var before=_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile");
            var bundle=SkillPublicationJson.Read(File.ReadAllText(Path.Combine(_directory,"f7.json"))).Document;
            bundle.Project.Skills[0].Nodes.First(n=>n.Kind=="projectile").Parameters["gravity"]=0;
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(bundle));
            Assert.IsFalse(_runner.ReloadPublished());var after=_runner.Snapshot.Actors.Single(a=>a.Kind=="projectile");
            Assert.AreEqual(before.Id,after.Id);Assert.AreEqual(before.Y,after.Y);Assert.AreEqual(before.VerticalSpeed,after.VerticalSpeed);
            _runner.StepLogicForTests(60);Assert.AreEqual("Ground",_runner.Snapshot.Events.Single(e=>e.Kind=="projectile-end").Result);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
