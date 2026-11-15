using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Combat.Presentation;
using Combat.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Combat.Tests
{
    public sealed class ActorAnimationPlayModeTests
    {
        UnityActorView _view;
        ArenaBootstrap _arena;
        string _directory;
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(_view!=null)Object.Destroy(_view.gameObject);if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;
            if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);
        }
        UnityActorView Create()
        {
            var content=UnityRuntimeContent.Load().Content;
            _view=(UnityActorView)new UnityViewFactory(content.Runtime.Actors,definitions:content.Runtime.Views).Create("arena-player");
            _view.Bind(new EntityId(200,1),"arena-player");return _view;
        }
        static PoseSample Pose(float time=0)=>new PoseSample{ActorTime=time,Grounded=true,YawDeg=0,Id=new EntityId(200,1)};

        [UnityTest]
        public IEnumerator RealResourcesHaveValidSkeletonStatesMaterialsAndNoOldScripts()
        {
            var view=Create();view.Sample(Pose());yield return null;
            Assert.IsTrue(view.Animation.Animator.avatar.isValid);Assert.AreEqual(0,view.GetComponentsInChildren<MonoBehaviour>().Count(m=>m==null));
            Assert.AreEqual(1,view.GetComponentsInChildren<Animator>().Length);Assert.IsFalse(view.Animation.Animator.applyRootMotion);Assert.IsFalse(view.Animation.Animator.fireEvents);
            Assert.Greater(view.GetComponentsInChildren<SkinnedMeshRenderer>().Length,0);
            foreach(var renderer in view.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)
            {Assert.NotNull(material);Assert.AreEqual("Universal Render Pipeline/Lit",material.shader.name);Assert.NotNull(material.GetTexture("_BaseMap"));}
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator BodyCyclesAdvanceAndStopIndependentlyFromRenderFramesAndTimelineActionsRestartByCast()
        {
            var view=Create();var pose=Pose();pose.MoveIntent=new SimVec3(1,0,0);view.Sample(pose);Assert.AreEqual("MoveForward",view.Animation.State);
            pose.ActorTime=.2f;view.Sample(pose);float phase=view.Animation.Phase;Assert.Greater(phase,0);
            for(int i=0;i<5;i++)view.Sample(pose);Assert.AreEqual(phase,view.Animation.Phase,.00001);
            pose.ActorTime=.4f;view.Sample(pose);Assert.AreNotEqual(phase,view.Animation.Phase);
            pose.Animation="Fire";pose.CastId=1;pose.ActionTime=.2f;pose.ActionDuration=.4f;view.Sample(pose);
            Assert.AreEqual("Fire",view.Animation.State);Assert.AreEqual(.5f,view.Animation.Phase,.0001);Assert.AreEqual(1,view.Animation.ActionStarts);
            pose.InHitstop=true;view.Sample(pose);yield return null;view.Sample(pose);Assert.AreEqual(.5f,view.Animation.Phase,.0001);Assert.AreEqual(1,view.Animation.ActionStarts);
            pose.CastId=2;pose.ActionTime=0;pose.InHitstop=false;view.Sample(pose);Assert.AreEqual(0,view.Animation.Phase,.0001);Assert.AreEqual(2,view.Animation.ActionStarts);
            pose.Animation=null;pose.CastId=0;pose.MoveBlocked=true;view.Sample(pose);Assert.AreEqual("Stand",view.Animation.State);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator DirectionalRollUsesTheCoreYawConventionAndRootMotionCannotMoveTheActor()
        {
            var view=Create();var pose=Pose();pose.Animation="Roll";pose.CastId=1;pose.ActionTime=.3f;pose.ActionDuration=.6f;
            pose.MoveIntent=new SimVec3(0,0,1);pose.MoveBlocked=true;pose.LogicPos=new SimVec3(3,0,4);view.Sample(pose);
            Assert.AreEqual("RollLeft",view.Animation.State);Assert.AreEqual(.5f,view.Animation.Phase,.0001);
            yield return null;view.Sample(pose);Assert.AreEqual(new Vector3(3,0,4),view.transform.position);
            pose.MoveIntent=new SimVec3(0,0,-1);view.Sample(pose);Assert.AreEqual("RollRight",view.Animation.State);
            pose.MoveIntent=new SimVec3(-1,0,0);view.Sample(pose);Assert.AreEqual("RollBack",view.Animation.State);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator HurtUsesActorTimeAndDeathUsesCleanupWindowWithoutMutatingSharedMaterials()
        {
            var view=Create();var pose=Pose();view.Sample(pose);var material=view.GetComponentInChildren<SkinnedMeshRenderer>().sharedMaterial;Color original=material.GetColor("_BaseColor");
            view.OnHurt(new EvHurt(EntityId.Invalid,view.Id,HitFeedbackLevel.Heavy));pose.InHitstop=true;view.Sample(pose);Assert.AreEqual("Hurt1",view.Animation.State);Assert.AreEqual(0,view.Animation.Phase,.0001);
            yield return null;view.Sample(pose);Assert.AreEqual(0,view.Animation.Phase,.0001);Assert.AreEqual(original,material.GetColor("_BaseColor"));
            pose.InHitstop=false;pose.ActorTime=.1f;view.Sample(pose);Assert.Greater(view.Animation.Phase,0);
            view.OnDead(new EvEntityDead(view.Id,EntityId.Invalid));view.Sample(pose);Assert.AreEqual("Dead",view.Animation.State);float phase=view.Animation.Phase;
            yield return null;view.Sample(pose);Assert.Greater(view.Animation.Phase,phase);
            pose.PresentationPaused=true;view.Sample(pose);phase=view.Animation.Phase;yield return null;view.Sample(pose);Assert.AreEqual(phase,view.Animation.Phase,.000001);
            pose.PresentationPaused=false;view.Release();yield return null;
            Assert.IsTrue(material!=null);Assert.AreEqual(original,material.GetColor("_BaseColor"));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ArenaUsesScaledTimelineAndIndependentHitstopAndRebuildsExactlyOneFreshView()
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.actor-animation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));p.Runtime.Actors.Single(a=>a.Id=="arena-player").ActionSpeed=2;
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-controlled-shot")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(),_arena.LastLoadError);
            Assert.IsTrue(_arena.CastPublished("f6"));_arena.StepForTests(5);yield return null;var world=_arena.Session.World;
            var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==_arena.Session.PlayerId);Assert.AreEqual("Fire",view.Animation.State);Assert.AreEqual(1f/3,view.Animation.Phase,.0001);
            world.RequestActorHitstop(view.Id,3);_arena.StepForTests(3);yield return null;Assert.AreEqual(1f/3,view.Animation.Phase,.0001);
            _arena.StepForTests(5);yield return null;Assert.AreEqual(2f/3,view.Animation.Phase,.0001);
            var old=view;_arena.Restart();yield return null;Assert.IsTrue(old==null);Assert.AreEqual(1,_arena.GetComponentsInChildren<UnityActorView>().Length);
            Assert.AreEqual("Stand",_arena.GetComponentInChildren<UnityActorView>().Animation.State);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator MissingResourceAndAnimatorStateLeaveTheRunningArenaUntouched()
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.actor-animation-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));string file=Path.Combine(_directory,"project.json");
            File.WriteAllText(file,SkillProjectJson.Write(p));yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(false));var previous=_arena.Session;
            p.Runtime.Views[0].States["Fire"]="Base Layer.Missing";File.WriteAllText(file,SkillProjectJson.Write(p));Assert.IsFalse(_arena.Reload(false));Assert.AreSame(previous,_arena.Session);StringAssert.Contains("Missing Animator state",_arena.LastLoadError);
            p.Runtime.Views[0].Resource="Combat/Missing";File.WriteAllText(file,SkillProjectJson.Write(p));Assert.IsFalse(_arena.Reload(false));Assert.AreSame(previous,_arena.Session);StringAssert.Contains("Missing Unity prefab",_arena.LastLoadError);
            _arena.StepForTests(2);Assert.AreEqual(2,previous.World.Time.Frame);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
