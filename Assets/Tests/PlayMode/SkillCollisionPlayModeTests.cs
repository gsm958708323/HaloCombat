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
    public sealed class SkillCollisionPlayModeTests
    {
        string _directory;
        SkillProjectDocument _project;
        SkillLabRunner _runner;
        void Publish(string skill)
        {
            foreach(var pair in new[]{new[]{"f6","basic-shot"},new[]{"f7",skill}})
                File.WriteAllText(Path.Combine(_directory,pair[0]+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,pair[1])));
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Collision-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            _project.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=-2,Z=0,Width=.5f,Depth=4}};
            Publish("piercing-ray");
            yield return SkillLabTestScene.Load();
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);
            _runner.AutoAdvance=false;_runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;
            if(Directory.Exists(_directory))Directory.Delete(_directory,true);
        }

        [UnityTest]
        public IEnumerator RayAndObstacleVisualsFollowTheActualStoppedRayThenPierceAfterReload()
        {
            _runner.CastSlot("f7",0,0);_runner.StepLogicForTests(10);
            Assert.AreEqual(0,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(-2.4f,_runner.Snapshot.Rays.Single().ToX,.001f);
            Assert.AreEqual(1,_runner.Snapshot.Obstacles.Length);
            yield return null;
            Assert.IsTrue(_runner.GetComponentsInChildren<Transform>().Any(t=>t.name=="Obstacle_wall"));
            var line=_runner.GetComponentsInChildren<LineRenderer>().Single(l=>l.name.StartsWith("Ray_"));
            Assert.AreEqual(-2.4f,line.GetPosition(1).x,.001f);
            _project.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();
            _project.Preview.Targets=_project.Preview.Targets.Take(3).ToArray();
            for(int i=0;i<3;i++){_project.Preview.Targets[i].X=i*4;_project.Preview.Targets[i].Z=0;}
            Publish("piercing-ray");Assert.IsTrue(_runner.ReloadPublished());_runner.CastSlot("f7",0,0);_runner.StepLogicForTests(10);
            Assert.AreEqual(240,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(3,_runner.Snapshot.Statistics.Hits);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PublishedBulletHitsTheTargetEdgeAndShootingKeepsPlayerMovement()
        {
            _project.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();
            _project.Preview.Targets=_project.Preview.Targets.Take(1).ToArray();_project.Preview.Targets[0].Z=.4f;
            var shot=_project.Skills.First(s=>s.Id=="basic-shot");shot.Nodes.First(n=>n.Kind=="damage").Parameters["critChance"]=0;
            shot.Nodes.First(n=>n.Kind=="projectile").Parameters["speed"]=1000;
            Publish("piercing-ray");Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
            Assert.IsTrue(_runner.CastSlot("f6",10,0).Accepted);_runner.StepLogicForTests(30);Assert.AreEqual(1,_runner.Snapshot.Statistics.Hits);
            Assert.IsTrue(_runner.ReloadPublished());_runner.SetMovement(0,1);_runner.CastSlot("f6",10,0);_runner.StepLogicForTests(4);
            Assert.Greater(_runner.Snapshot.Actors.First(a=>a.Kind=="caster").Z,.3f);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MovementDashAndBlinkShareTheSameObstacleClearance()
        {
            _runner.SetMovement(1,0);_runner.StepLogicForTests(100);
            Assert.AreEqual(-2.6501f,_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X,.002f);
            Publish("dash-strike");_runner.ReloadPublished();_runner.CastSlot("f7",0,0);_runner.StepLogicForTests(100);
            Assert.AreEqual(-2.6501f,_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X,.002f);
            Assert.AreEqual(0,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(0,_runner.Snapshot.Volumes.Length);
            Publish("blink-strike");_runner.ReloadPublished();Assert.IsFalse(_runner.CastSlot("f7",-2,0).Accepted);
            Assert.AreEqual(100,_runner.Snapshot.Energy);Assert.IsTrue(_runner.CastSlot("f7",0,0).Accepted);_runner.StepLogicForTests(50);
            Assert.AreEqual(0,_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X,.0001f);Assert.AreEqual(240,_runner.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DiskPublishedFastProjectilesStopAtThinWallsAndReportOneEnd()
        {
            var projectile=_project.Skills.First(s=>s.Id=="basic-shot").Nodes.First(n=>n.Kind=="projectile");projectile.Parameters["speed"]=1000;
            _project.Preview.Obstacles[0].Width=.02f;Publish("piercing-ray");_runner.ReloadPublished();_runner.CastSlot("f6",0,0);_runner.StepLogicForTests(50);
            Assert.AreEqual(0,_runner.Snapshot.Statistics.Damage);
            Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="projectile-end"&&e.Result=="Wall"));
            _project.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();Publish("piercing-ray");_runner.ReloadPublished();_runner.CastSlot("f6",0,0);_runner.StepLogicForTests(50);
            Assert.AreEqual(80,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(1,_runner.Snapshot.Events.Count(e=>e.Kind=="projectile-end"&&e.Result=="HitLimit"));
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
