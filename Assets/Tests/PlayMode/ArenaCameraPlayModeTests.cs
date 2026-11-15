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
    public sealed class ArenaCameraPlayModeTests
    {
        string _directory;ArenaBootstrap _arena;SkillProjectDocument _project;
        ArenaPresentation Presentation=>_arena.GetComponent<ArenaPresentation>();
        Actor Player{get{_arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var actor);return actor;}}
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _project=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.camera-audit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);Save();
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);
            Assert.IsTrue(_arena.Reload(false),_arena.LastLoadError);
        }
        void Save()=>File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(_project));
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator RealCameraFollowsTheOriginalSpringAndLowersForDownedPostureWithoutAdvancingCombat()
        {
            var camera=Presentation.Camera;Assert.AreEqual(4.1,camera.transform.position.y,.0001);Assert.AreEqual(-2.5,camera.transform.position.z,.0001);
            int frame=_arena.Session.World.Time.Frame;Player.GetComp<TransformComp>().Position=new SimVec3(10,0,0);Presentation.Sample(1,.1f,0);
            Assert.AreEqual(10*(1-Math.Exp(-.8)),camera.transform.position.x,.0001);Assert.AreEqual(frame,_arena.Session.World.Time.Frame);
            Player.GetComp<TagComp>().Add(CommonTags.Downed,1,TagSource.Debug);Presentation.Sample(1,1,0);
            Assert.AreEqual(2.5+.55+1.05*Math.Exp(-8),camera.transform.position.y,.0001);Assert.AreEqual(60,camera.fieldOfView);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
        static void Hit(CombatWorld world,Actor source,Actor target,HitFeedbackLevel level)
        {world.Deliver(new IEffect[]{new DamageEffect{Flat=1,Coeff=0,CanCrit=false,FireOnHurted=false,Feedback=level,InheritSkillFeedback=false}},source,target,0);}
        [UnityTest]
        public IEnumerator OnlyTheActualLocalSourceGradesTheCameraAndPresentationTimeDecaysItsFov()
        {
            _arena.StepForTests(1);var world=_arena.Session.World;var target=world.RegistryActive().Single(a=>a.Id!=_arena.Session.PlayerId);
            Hit(world,Player,target,HitFeedbackLevel.Heavy);Presentation.Sample(1,0,0);Assert.AreEqual(61.375,Presentation.Camera.fieldOfView,.0001);
            Hit(world,target,Player,HitFeedbackLevel.Finisher);Presentation.Sample(1,0,0);Assert.AreEqual(61.375,Presentation.Camera.fieldOfView,.0001);
            _arena.SetPaused(true);int frame=world.Time.Frame;Presentation.Sample(1,.1f,0);Assert.AreEqual(61.125,Presentation.Camera.fieldOfView,.0001);Assert.AreEqual(frame,world.Time.Frame);
            Presentation.Sample(1,2,0);Assert.AreEqual(60,Presentation.Camera.fieldOfView,.0001);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator JsonShakeDisableAndRestartKeepCameraStateSeparateAndUseTheOriginalTrainingModel()
        {
            _project.Runtime.Arena.CameraShakeEnabled=false;Save();Assert.IsTrue(_arena.Reload(false),_arena.LastLoadError);_arena.StepForTests(1);
            var world=_arena.Session.World;var target=world.RegistryActive().Single(a=>a.Id!=_arena.Session.PlayerId);Hit(world,Player,target,HitFeedbackLevel.Finisher);Presentation.Sample(1,0,0);
            Assert.AreEqual(60,Presentation.Camera.fieldOfView,.0001);Assert.AreEqual(4.1,Presentation.Camera.transform.position.y,.0001);
            yield return null;
            var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==target.Id);Assert.IsNotNull(view.Animation);Assert.IsNotNull(view.GetComponentInChildren<SkinnedMeshRenderer>());
            var prefab=Resources.Load<GameObject>("Combat/Actors/MaleGunner");Assert.AreSame(prefab.GetComponentInChildren<Animator>(true).runtimeAnimatorController,view.Animation.Animator.runtimeAnimatorController);
            var previous=_arena.Session;_project.Runtime.Arena.CameraShakeEnabled=true;Save();Assert.IsTrue(_arena.Reload(false),_arena.LastLoadError);
            Assert.AreEqual(0,previous.World.RegistryActive().Count);Assert.AreEqual(60,Presentation.Camera.fieldOfView,.0001);Assert.AreEqual(4.1,Presentation.Camera.transform.position.y,.0001);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
