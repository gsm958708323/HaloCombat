using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Combat.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Combat.Tests
{
    public sealed class AmmoRecastPlayModeTests
    {
        ArenaBootstrap _runner;
        string _directory,_relative;
        SkillProjectDocument _project;
        Actor Player {get{_runner.Session.World.TryGetActor(_runner.Session.PlayerId,out var value);return value;}}
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _project=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));
            _relative="Combat/.ammo-tests-"+Guid.NewGuid().ToString("N");_directory=Path.Combine(Application.streamingAssetsPath,_relative);Directory.CreateDirectory(_directory);
            Save();Publish("f6","arena-ammo-shot");Publish("f7","arena-tracked-shot");
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _runner=Object.FindFirstObjectByType<ArenaBootstrap>();_runner.AutoAdvance=false;_runner.Configure(_relative+"/project.json",true);Assert.IsTrue(_runner.Reload(),_runner.LastLoadError);
        }
        void Save()=>File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(_project));
        void Publish(string slot,string skill)=>File.WriteAllText(Path.Combine(_directory,slot+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,skill)));
        [UnityTearDown]
        public IEnumerator TearDown(){if(_runner!=null)Object.Destroy(_runner.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator DiskPublishedAmmoAndTrackedSkillsUseOneRealPlayerAndDelayedReload()
        {
            _project.Runtime.Actors.First(a=>a.Id=="arena-player").InitialAmmo=0;Save();Assert.IsTrue(_runner.Reload(),_runner.LastLoadError);
            Assert.IsTrue(_runner.CastPublished("f6"));Assert.AreEqual(new SkillNodeId(2009),Player.GetComp<SkillDirectorComp>().CurrentSkill);
            _runner.StepForTests(54);Assert.AreEqual(0,Player.GetComp<AmmoComp>().Current);_runner.StepForTests(1);Assert.AreEqual(60,Player.GetComp<AmmoComp>().Current);
            _runner.StepForTests(3);Assert.IsTrue(_runner.CastPublished("f6"));Assert.AreEqual(59,Player.GetComp<AmmoComp>().Current);_runner.StepForTests(40);
            Assert.IsTrue(_runner.CastPublished("f7"));_runner.StepForTests(26);var tracker=Player.GetComp<TrackedProjectilesComp>();Assert.IsTrue(tracker.TryFind("warp",out var body));
            float x=body.GetComp<TransformComp>().Position.X;var id=body.Id;Assert.IsTrue(_runner.CastPublished("f7"));_runner.StepForTests(1);
            Assert.AreEqual(x,Player.GetComp<TransformComp>().Position.X,.0001f);Assert.IsFalse(_runner.Session.World.TryGetActor(id,out _));Assert.AreEqual(59,Player.GetComp<AmmoComp>().Current);
            yield return null;Assert.AreEqual("Teleported",_runner.Status);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator Fire4UsesTheSameTrackedBranchAndRestartDisposesItsResourcesAndViews()
        {
            var input=new ArenaInputFrame{Pressed=new[]{"Fire4"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(20,0,0),0)};
            _runner.Submit(input);_runner.StepForTests(26);Assert.IsTrue(Player.GetComp<TrackedProjectilesComp>().TryFind("warp",out var body));
            float x=body.GetComp<TransformComp>().Position.X;_runner.Submit(input);_runner.StepForTests(1);Assert.AreEqual(x,Player.GetComp<TransformComp>().Position.X,.0001f);
            _runner.StepForTests(1);Assert.IsTrue(_runner.CastPublished("f7"));_runner.StepForTests(5);var previous=_runner.Session;var oldPlayer=Player;var ammo=oldPlayer.GetComp<AmmoComp>();
            _runner.Restart();Assert.AreEqual(0,ammo.Current);Assert.IsFalse(oldPlayer.IsActive);Assert.AreEqual(0,previous.World.RegistryActive().Count);
            previous.World.Events.Publish(new EvProjectileTeleport(EntityId.Invalid,EntityId.Invalid,"warp",default,true,"late event"));Assert.AreEqual("JSON Arena ready",_runner.Status);
            yield return null;Assert.AreEqual(1,_runner.GetComponentsInChildren<UnityActorView>().Length);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator BlockedRecastKeepsItsBodyAndProducesARealHudResult()
        {
            _project.Runtime.Actors.First(a=>a.Id=="arena-player").CollisionRadius=.6f;
            _project.Runtime.Arena.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=1.2f,Z=0,Width=.2f,Depth=2}};Save();Assert.IsTrue(_runner.Reload(),_runner.LastLoadError);
            Assert.IsTrue(_runner.CastPublished("f7"));_runner.StepForTests(26);Assert.IsTrue(Player.GetComp<TrackedProjectilesComp>().TryFind("warp",out var body));var id=body.Id;
            Assert.IsTrue(_runner.CastPublished("f7"));_runner.StepForTests(1);Assert.AreEqual(0,Player.GetComp<TransformComp>().Position.X);Assert.IsTrue(_runner.Session.World.TryGetActor(id,out _));
            yield return null;Assert.AreEqual("Destination blocked",_runner.Status);LogAssert.NoUnexpectedReceived();
        }
    }
}
