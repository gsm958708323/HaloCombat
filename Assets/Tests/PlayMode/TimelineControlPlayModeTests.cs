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
    public sealed class TimelineControlPlayModeTests
    {
        SkillLabRunner _lab;
        ArenaBootstrap _arena;
        string _directory;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_lab!=null)Object.Destroy(_lab.gameObject);if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator RealPublicationAppliesControlLeasesAndFastAuthoredTimingAndReleasesOnInterrupt()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-TimelineControl-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));p.Preview.Caster.X=0;p.Preview.Caster.ActionSpeed=2;p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"controlled-shot")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            Assert.IsTrue(_lab.CastSlot("f6",4,0).Accepted);_lab.SetMovement(0,1);_lab.StepLogicForTests(5);var actor=_lab.Snapshot.Actors.Single(a=>a.Kind=="caster");
            CollectionAssert.AreEquivalent(new[]{1011,1012,1013},actor.ControlTags);Assert.AreEqual(2,actor.ActionRate);Assert.AreEqual(.2f,actor.ActionTime.Value,.0001);Assert.AreEqual(0,actor.Z);Assert.AreEqual(1,_lab.Snapshot.Statistics.Projectiles);
            _lab.Interrupt();Assert.AreEqual(0,_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").ControlTags.Length);_lab.StepLogicForTests(20);_lab.SetMovement(0,1);_lab.StepLogicForTests(1);Assert.Greater(_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").Z,0);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator FormalDiskActorSpeedDrivesTheSameTimelineAndBodyPoseWhileMoveInputIsBlocked()
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.timeline-controls-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));p.Runtime.Actors.Single(a=>a.Id=="arena-player").ActionSpeed=2;
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-controlled-shot")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;_arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(),_arena.LastLoadError);
            Assert.IsTrue(_arena.CastPublished("f6"));_arena.Submit(new ArenaInputFrame{MoveZ=1,AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});_arena.StepForTests(10);
            _arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var actor);Assert.AreEqual(2,actor.GetComp<TransformComp>().Position.X,.0001);Assert.AreEqual(0,actor.GetComp<TransformComp>().Position.Z,.0001);Assert.AreEqual(.4f,actor.GetComp<SkillDirectorComp>().TimelineTime,.0001);
            yield return null;var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==actor.Id);Assert.AreEqual(2,view.transform.position.x,.0001);_arena.StepForTests(5);Assert.IsFalse(actor.GetComp<TagComp>().Has(CommonTags.BlockMove));
            _arena.Restart();yield return null;Assert.AreEqual(1,_arena.GetComponentsInChildren<UnityActorView>().Length);_arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var fresh);Assert.IsFalse(fresh.GetComp<TagComp>().Has(CommonTags.BlockSkill));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator InvalidControlMetadataKeepsThePreviouslyLoadedWorld()
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.timeline-controls-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));string file=Path.Combine(_directory,"project.json");File.WriteAllText(file,SkillProjectJson.Write(p));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;_arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(false));var previous=_arena.Session;
            p.Skills.Single(s=>s.Id=="arena-controlled-shot").Timeline.ControlTags=new[]{1005};File.WriteAllText(file,SkillProjectJson.Write(p));Assert.IsFalse(_arena.Reload(false));Assert.AreSame(previous,_arena.Session);StringAssert.Contains("controlTags",_arena.LastLoadError);
            _arena.StepForTests(5);Assert.AreEqual(5,previous.World.Time.Frame);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
