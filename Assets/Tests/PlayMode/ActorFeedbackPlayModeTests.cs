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
    public sealed class ActorFeedbackPlayModeTests
    {
        ArenaBootstrap _arena;
        SkillLabRunner _lab;
        string _directory;
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_arena!=null)Object.Destroy(_arena.gameObject);if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator FormalPrimaryPublishesGradedHurtAndFreezesOnlyTheSelectedViewsAndActors()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.SetTraining(true);_arena.StepForTests(1);
            var s=_arena.Session;var player=s.World.RegistryActive().Single(a=>a.Id==s.PlayerId);var target=s.World.RegistryActive().Single(a=>a.Id!=s.PlayerId);
            target.GetComp<AttributeSet>().SetBase(AttrId.Hp,1000);target.GetComp<TransformComp>().Position=new SimVec3(1,0,0);
            var unrelated=s.World.SpawnActor(new ActorSpawnSpec("fighter"));s.World.TryGetActor(unrelated,out var other);other.GetComp<TransformComp>().Position=new SimVec3(0,0,-2);other.GetComp<LocomotionComp>().RequestMoveIntent(0,-1);
            int hurts=0;s.World.Events.Subscribe<EvHurt>(e=>hurts++);_arena.Submit(new ArenaInputFrame{PrimaryHeld=false,Pressed=new[]{"Fire1"},AimValid=true,Aim=new SkillAim(target.Id,new SimVec3(1,0,0),0)});
            for(int i=0;i<30&&hurts==0;i++)_arena.StepForTests(1);Assert.AreEqual(1,hurts);_arena.StepForTests(1);Assert.IsTrue(player.Time.IsStopped);Assert.IsTrue(target.Time.IsStopped);Assert.IsFalse(other.Time.IsStopped);Assert.IsFalse(s.World.InHitstop);
            yield return null;var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==target.Id);Assert.AreEqual(1,view.Feedback.Sequence);Assert.AreEqual(HitFeedbackLevel.Light,view.Feedback.Level);Assert.Greater(view.Feedback.Flash,0);
            Assert.IsNotNull(view.Animation,"The restored training target uses its original skeletal model.");
            var animator=view.Animation.Animator;float phase=animator.GetCurrentAnimatorStateInfo(0).normalizedTime;float before=view.Feedback.Flash;
            yield return null;Assert.AreEqual(phase,animator.GetCurrentAnimatorStateInfo(0).normalizedTime,.00001f);Assert.LessOrEqual(view.Feedback.Flash,before);Assert.Less(other.GetComp<TransformComp>().Position.Z,-2);
            _arena.StepForTests(1);Assert.IsFalse(player.Time.IsStopped);Assert.IsTrue(target.Time.IsStopped);_arena.StepForTests(1);Assert.IsFalse(target.Time.IsStopped);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator PublishedJsonSnapshotAndReleaseKeepIndependentTimingAcrossTheUnityBoundary()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-ActorFeedback-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));p.Preview.Caster.X=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=2,Hp=1000}};
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"actor-hitstop")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            Assert.IsTrue(_lab.CastSlot("f6",2,0).Accepted);_lab.StepLogicForTests(1);_lab.SetMovement(1,0);_lab.StepLogicForTests(2);var shot=_lab.Snapshot;
            Assert.IsTrue(shot.Actors.Single(a=>a.Kind=="caster").Stopped);Assert.IsTrue(shot.Actors.Single(a=>a.Kind=="target").Stopped);Assert.AreEqual(0,shot.Actors.Single(a=>a.Kind=="caster").X);Assert.IsTrue(shot.Events.Any(e=>e.Kind=="hurt"&&e.Result=="heavy"));
            _lab.StepLogicForTests(1);Assert.IsFalse(_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").Stopped);Assert.IsTrue(_lab.Snapshot.Actors.Single(a=>a.Kind=="target").Stopped);
            _lab.StepLogicForTests(2);Assert.IsFalse(_lab.Snapshot.Actors.Single(a=>a.Kind=="target").Stopped);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RestartReleasesFeedbackAndLateOldGenerationHurtCannotAffectTheNewView()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.SetTraining(true);var old=_arena.Session;var id=old.PlayerId;
            old.World.Events.Publish(new EvHurt(EntityId.Invalid,id,HitFeedbackLevel.Finisher));_arena.Restart();old.World.Events.Publish(new EvHurt(EntityId.Invalid,id,HitFeedbackLevel.Finisher));yield return null;
            var current=_arena.GetComponentsInChildren<UnityActorView>().Single();Assert.AreEqual(0,current.Feedback.Sequence);Assert.AreEqual(0,current.Feedback.Flash);
            // A current-world callback still has to match the complete generation.
            var registry=new ViewRegistry(new UnityViewFactory());var newer=new EntityId(100,2);registry.HandleSpawn(new EvEntitySpawn(newer,"fighter",EntityId.Invalid));
            var newView=Object.FindObjectsByType<UnityActorView>(FindObjectsSortMode.None).Single(v=>v.Id==newer);registry.HandleHurt(new EvHurt(EntityId.Invalid,new EntityId(100,1),HitFeedbackLevel.Heavy));Assert.AreEqual(0,newView.Feedback.Sequence);registry.ReleaseAll();LogAssert.NoUnexpectedReceived();
        }
    }
}
