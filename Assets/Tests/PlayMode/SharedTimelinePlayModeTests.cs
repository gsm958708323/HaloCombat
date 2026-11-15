using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Combat.Unity;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Combat.Tests
{
    public sealed class SharedTimelinePlayModeTests
    {
        string _directory;
        ArenaBootstrap _arena;
        SkillLabRunner _lab;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_arena!=null)Object.Destroy(_arena.gameObject);if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        static SkillProjectDocument Formal()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile)));
        void Files(SkillProjectDocument p)
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.shared-timeline-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-second")));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-finisher")));
        }
        Actor Player{get{_arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var player);return player;}}
        IEnumerator Ready(int skill)
        {
            int count=0;
            while(count++<120)
            {
                var player=Player;var director=player.GetComp<SkillDirectorComp>();
                if(director.HasComboConfirmation&&player.GetComp<TagComp>().Has(CommonTags.Cancel)&&player.Time.HitstopLeft==0&&!player.Time.IsStopped)break;
                _arena.StepForTests(1);
            }
            Assert.AreEqual(skill,Player.GetComp<SkillDirectorComp>().CurrentSkill.Value);Assert.IsTrue(Player.GetComp<SkillDirectorComp>().HasComboConfirmation);Assert.Less(count,120);yield return null;
        }
        void Input()=>_arena.Submit(new ArenaInputFrame{Pressed=new[]{"Fire1"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});
        [UnityTest]
        public IEnumerator DiskSharedActionsUseIndependentCostsGradesAndIdenticalUnityCoreSnapshots()
        {
            var p=Formal();p.Preview.Caster.X=0;p.Preview.Caster.DamageDealt=1;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=1,Hp=1000,Atk=0}};Files(p);
            using(var reference=new PublishedSkillSession())
            {
                reference.Replace(new Dictionary<string,string>{{"f6",File.ReadAllText(Path.Combine(_directory,"f6.json"))},{"f7",File.ReadAllText(Path.Combine(_directory,"f7.json"))}});
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
                Assert.IsTrue(_lab.CastSlot("f6",4,0).Accepted);reference.Simulation.Cast("arena-combo-second",4,0);_lab.StepLogicForTests(40);reference.Simulation.Advance(40);
                Assert.IsTrue(_lab.CastSlot("f7",4,0).Accepted);reference.Simulation.Cast("arena-combo-finisher",4,0);_lab.StepLogicForTests(40);reference.Simulation.Advance(40);
                Assert.AreEqual(55,_lab.Snapshot.Ammo);Assert.AreEqual(2,_lab.Snapshot.Statistics.Hits);Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(reference.Simulation.Snapshot()),JToken.FromObject(_lab.Snapshot)));
                Assert.IsFalse(_lab.Snapshot.Events.Any(e=>e.Kind=="hurt"),"Original PD4012 has no HurtFeedback effect; its graded damage and hit Cue remain separate.");yield return null;LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest]
        public IEnumerator RealArenaPrimaryContactAdvancesTwoSkillsOnOneAxisAndAnimatorReplaysEachCast()
        {
            Files(Formal());yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(),_arena.LastLoadError);
            Input();yield return Ready(2001);Input();_arena.StepForTests(1);yield return Ready(2012);
            var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==Player.Id);Assert.AreEqual("Fire",view.Animation.State);long before=view.Animation.CastId;int starts=view.Animation.ActionStarts;
            Input();_arena.StepForTests(1);yield return null;Assert.AreEqual(2013,Player.GetComp<SkillDirectorComp>().CurrentSkill.Value);Assert.Greater(view.Animation.CastId,before);Assert.Greater(view.Animation.ActionStarts,starts);
            Assert.AreEqual(54,Player.GetComp<AmmoComp>().Current);_arena.StepForTests(50);_arena.Restart();yield return null;Assert.IsTrue(view==null);Assert.AreEqual(1,_arena.GetComponentsInChildren<UnityActorView>().Length);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ConflictingSharedVersionsCannotReplaceAnArenaOrItsViews()
        {
            var p=Formal();Files(p);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload());var previous=_arena.Session;
            p.Timelines[0].Timeline.Payloads[0].Time=.3f;File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-finisher")));
            Assert.IsFalse(_arena.Reload());Assert.AreSame(previous,_arena.Session);StringAssert.Contains("timeline:",_arena.LastLoadError);Assert.AreEqual(1,_arena.GetComponentsInChildren<UnityActorView>().Length);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator SharedPayloadEditsApplyAtReloadAndNeverRewriteThePreviousDirector()
        {
            var p=Formal();Files(p);yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload());Assert.IsTrue(_arena.CastPublished("f6"));_arena.StepForTests(5);var previous=Player;
            p.Timelines[0].Timeline.Payloads[0].Time=.3f;File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-second")));File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-finisher")));
            Assert.AreEqual(.1f,previous.GetComp<SkillDirectorComp>().TimelineTime,.0001);Assert.IsTrue(_arena.Reload());Assert.IsFalse(previous.IsActive);
            Assert.IsTrue(_arena.CastPublished("f6"));_arena.StepForTests(12);Assert.IsFalse(_arena.Session.World.RegistryActive().Any(a=>a.TryGetComp<ProjectileComp>(out _)));_arena.StepForTests(3);Assert.IsTrue(_arena.Session.World.RegistryActive().Any(a=>a.TryGetComp<ProjectileComp>(out _)));yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
