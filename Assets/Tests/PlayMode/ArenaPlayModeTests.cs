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
    public sealed class ArenaPlayModeTests
    {
        ArenaBootstrap _runner;
        string _relative,_directory;
        SkillProjectDocument _project;
        string ProjectFile=>Path.Combine(_directory,"project.json");
        Actor Player {get{_runner.Session.World.TryGetActor(_runner.Session.PlayerId,out var actor);return actor;}}
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _project=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile)));
            _relative="Combat/.arena-tests-"+Guid.NewGuid().ToString("N");_directory=Path.Combine(Application.streamingAssetsPath,_relative);Directory.CreateDirectory(_directory);Save();
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _runner=Object.FindFirstObjectByType<ArenaBootstrap>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;
            _runner.Configure(_relative+"/project.json",true);Assert.IsTrue(_runner.Reload(false),_runner.LastLoadError);
        }
        void Save()=>File.WriteAllText(ProjectFile,SkillProjectJson.Write(_project));
        static ArenaInputFrame Input(bool held=false,params string[] presses)=>new ArenaInputFrame{PrimaryHeld=held,Pressed=presses,AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)};
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(_runner!=null)Object.Destroy(_runner.gameObject);yield return null;
            if(Directory.Exists(_directory))Directory.Delete(_directory,true);
        }
        [UnityTest]
        public IEnumerator RealSceneRunsJsonMovementAimComboDamageAndViews()
        {
            Assert.IsNull(_runner.LastLoadError);Assert.IsNotNull(Camera.main);
            foreach(var root in SceneManager.GetActiveScene().GetRootGameObjects())Assert.IsFalse(root.GetComponentsInChildren<Component>(true).Any(c=>c==null));
            var input=Input();input.MoveZ=1;_runner.Submit(input);_runner.StepForTests(10);
            Assert.Greater(Player.GetComp<TransformComp>().Position.Z,0);Assert.AreEqual(0,Player.GetComp<TransformComp>().YawDegrees,.001f);
            _runner.Restart();float damage=0;_runner.Session.World.Events.Subscribe<EvDamage>(e=>damage+=e.Amount);
            _runner.Submit(Input(true,"Fire1"));_runner.StepForTests(90);_runner.Submit(Input());
            Assert.Greater(damage,0);yield return null;
            var view=_runner.GetComponentsInChildren<UnityActorView>().First(v=>v.Id==_runner.Session.PlayerId);
            Assert.AreEqual(Player.GetComp<TransformComp>().Position.X,view.transform.position.x,.001f);
            Assert.NotNull(view.Animation);Assert.AreEqual(1,view.GetComponentsInChildren<Animator>().Length);
            Assert.Greater(view.GetComponentInChildren<SkinnedMeshRenderer>().bounds.size.y,.5f);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RestartDisposesActorsViewsEffectsAndOldEventSubscriptions()
        {
            _runner.StepForTests(1);var previous=_runner.Session;var actor=Player;
            var oldRoot=_runner.transform.Find("ArenaRuntime").gameObject;previous.World.Events.Publish(new EvCue(101,actor.Id,"test"));
            _runner.Restart();Assert.AreNotSame(previous,_runner.Session);Assert.IsFalse(actor.IsActive);Assert.AreEqual(0,previous.World.RegistryActive().Count);
            previous.World.Events.Publish(new EvEntitySpawn(new EntityId(100,1),"arena-enemy",EntityId.Invalid));
            yield return null;Assert.IsTrue(oldRoot==null);Assert.AreEqual(1,_runner.GetComponentsInChildren<UnityActorView>().Length);
            Assert.AreEqual(1,_runner.transform.Cast<Transform>().Count(t=>t.name=="ArenaRuntime"));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator BadJsonKeepsTheCurrentSessionAndItsScheduledSkill()
        {
            _runner.Submit(Input(false,"Fire1"));_runner.StepForTests(4);var previous=_runner.Session;
            File.WriteAllText(ProjectFile,"{broken");Assert.IsFalse(_runner.Reload(false));Assert.AreSame(previous,_runner.Session);Assert.IsNotNull(_runner.LastLoadError);
            Assert.IsTrue(Player.GetComp<SkillDirectorComp>().IsPlaying);_runner.StepForTests(50);Assert.AreEqual(54,previous.World.Time.Frame);
            yield return null;Assert.IsTrue(_runner.GetComponentsInChildren<UnityActorView>().Length>=2);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator FailureDuringCandidateStartAlsoKeepsTheLiveSessionAndViewTree()
        {
            var previous=_runner.Session;var oldRoot=_runner.transform.Find("ArenaRuntime").gameObject;
            _project.Runtime.Arena.HalfWidth=3;_project.Runtime.Arena.HalfDepth=3;_project.Runtime.Arena.SpawnDistance=3;
            _project.Runtime.Actors.First(a=>a.Id=="arena-target").CollisionRadius=2;Save();
            Assert.IsFalse(_runner.Reload(false));StringAssert.Contains("training target spawn",_runner.LastLoadError);Assert.AreSame(previous,_runner.Session);
            yield return null;Assert.IsTrue(oldRoot.activeInHierarchy);Assert.AreEqual(1,_runner.GetComponentsInChildren<UnityActorView>().Length);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator DiskPublicationsUseTheFormalPlayerCostGuardAndPreserveWorldOnConflict()
        {
            _project.Runtime.Actors.First(a=>a.Id=="arena-player").Recovery=0;Save();
            _project.Skills[0].Nodes.First(n=>n.Kind=="cast").Parameters["cost"]=20;
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,"g1")));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,"homing")));
            Assert.IsTrue(_runner.Reload(),_runner.LastLoadError);Assert.IsTrue(_runner.CastPublished("f6"));Assert.AreEqual(80,Player.GetComp<EnergyComp>().Current);
            Assert.IsFalse(_runner.CastPublished("f7"));Assert.AreEqual(80,Player.GetComp<EnergyComp>().Current);var previous=_runner.Session;
            _project.Id="different";File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,"homing")));
            Assert.IsFalse(_runner.Reload());Assert.AreSame(previous,_runner.Session);_runner.StepForTests(50);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator PauseDeathAndWallCleanupDoNotAdvanceFrozenCombat()
        {
            _runner.StepForTests(1);int frame=_runner.Session.World.Time.Frame;_runner.SetPaused(true);_runner.StepForTests(30);_runner.AdvanceWallForTests(10);Assert.AreEqual(frame,_runner.Session.World.Time.Frame);
            _runner.SetPaused(false);var id=_runner.Session.PlayerId;Player.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead,new ActivityEnterArgs{Reason="test"});
            Assert.IsTrue(_runner.Session.PlayerDead);_runner.StepForTests(30);_runner.AdvanceWallForTests(5.01f);Assert.AreEqual(frame,_runner.Session.World.Time.Frame);Assert.IsFalse(_runner.Session.World.TryGetActor(id,out _));
            _runner.Restart();Assert.IsFalse(_runner.Session.PlayerDead);Assert.IsFalse(_runner.Session.Paused);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
