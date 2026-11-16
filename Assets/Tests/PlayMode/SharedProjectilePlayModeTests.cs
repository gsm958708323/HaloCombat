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
    public sealed class SharedProjectilePlayModeTests
    {
        string _directory;SkillLabRunner _lab;ArenaBootstrap _arena;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_lab!=null)Object.Destroy(_lab.gameObject);if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));
        void Files(SkillProjectDocument p,string first,string second)
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.shared-projectile-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,first)));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,second)));
        }
        [UnityTest]
        public IEnumerator ActualSharedProjectileFilesEmitThreeIndependentBodiesAndMatchCoreSnapshots()
        {
            var p=Formal();p.Preview.Caster.X=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();Files(p,"arena-ammo-shot","arena-double-shot");
            using(var reference=new PublishedSkillSession())
            {
                reference.Replace(new Dictionary<string,string>{{"f6",File.ReadAllText(Path.Combine(_directory,"f6.json"))},{"f7",File.ReadAllText(Path.Combine(_directory,"f7.json"))}});
                yield return SkillLabTestScene.Load();_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
                Assert.IsTrue(_lab.CastSlot("f6",5,0).Accepted);reference.Simulation.Cast("arena-ammo-shot",5,0);_lab.StepLogicForTests(45);reference.Simulation.Advance(45);
                Assert.IsTrue(_lab.CastSlot("f7",5,0).Accepted);reference.Simulation.Cast("arena-double-shot",5,0);_lab.StepLogicForTests(30);reference.Simulation.Advance(30);
                var shots=_lab.Snapshot.Actors.Where(a=>a.Kind=="projectile").ToArray();Assert.AreEqual(3,shots.Length);Assert.AreEqual(2,shots.Select(a=>a.CastId).Distinct().Count());Assert.AreEqual(59,_lab.Snapshot.Ammo);
                Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(reference.Simulation.Snapshot()),JToken.FromObject(_lab.Snapshot)));yield return null;LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest]
        public IEnumerator ScheduledAndSineMotionUseThePublishedParametersAndRealVerticalPose()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));p.Preview.Caster.X=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();Files(p,"phase-bounce","sampled-return");
            yield return SkillLabTestScene.Load();_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            Assert.IsTrue(_lab.CastSlot("f6",5,0).Accepted);_lab.StepLogicForTests(29);var shot=_lab.Snapshot.Actors.Single(a=>a.Kind=="projectile");Assert.AreEqual(1,shot.Y,.0001);yield return null;
            var visual=_lab.GetComponentsInChildren<Transform>().Single(t=>t.name=="projectile_"+shot.Name);Assert.Greater(visual.position.y,1);
            _lab.StepLogicForTests(25);Assert.AreEqual(1,_lab.Snapshot.Events.Count(e=>e.Kind=="projectile-ground-bounce"));Assert.AreEqual(0,_lab.Snapshot.Actors.Single(a=>a.Kind=="projectile").Y,.0001);
            _lab.StepLogicForTests(50);Assert.AreEqual(3,_lab.Snapshot.Events.Count(e=>e.Kind=="projectile-ground-bounce"));Assert.IsFalse(_lab.Snapshot.Actors.Any(a=>a.Kind=="projectile"));
            Assert.IsTrue(_lab.CastSlot("f7",5,0).Accepted);_lab.StepLogicForTests(5);var returning=_lab.Snapshot.Actors.Single(a=>a.Kind=="projectile");Assert.AreEqual(.5f,returning.Speed.Value,.0001);_lab.StepLogicForTests(150);Assert.IsTrue(_lab.Snapshot.Events.Any(e=>e.Kind=="projectile-end"&&e.Result=="Returned"));yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator FormalFire2AndHomingUseTheOriginalDefinitionsAndRestartCleansTheirViews()
        {
            Files(Formal(),"arena-returning-shot","arena-homing-shot");yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(),_arena.LastLoadError);
            _arena.Submit(new ArenaInputFrame{Pressed=new[]{"Fire2"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});_arena.StepForTests(5);
            Assert.AreEqual(4003,_arena.Session.World.RegistryActive().Single(a=>a.TryGetComp<ProjectileComp>(out _)).GetComp<ProjectileComp>().Def.SpecId);Assert.IsFalse(_arena.CastPublished("f7"));
            _arena.StepForTests(25);Assert.IsTrue(_arena.CastPublished("f7"));_arena.StepForTests(5);Assert.IsTrue(_arena.Session.World.RegistryActive().Any(a=>a.TryGetComp<ProjectileComp>(out var b)&&b.Def.SpecId==4006));
            _arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var player);Assert.AreEqual(58,player.GetComp<AmmoComp>().Current);_arena.Restart();yield return null;
            Assert.IsFalse(_arena.Session.World.RegistryActive().Any(a=>a.TryGetComp<ProjectileComp>(out _)));Assert.AreEqual(1,_arena.GetComponentsInChildren<UnityActorView>().Length);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ConflictingSharedProjectileVersionsLeaveTheCurrentArenaAlive()
        {
            var p=Formal();Files(p,"arena-ammo-shot","arena-double-shot");yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload());var previous=_arena.Session;
            var d=p.Projectiles.Single(v=>v.Id=="arena-normal-projectile");d.Nodes.Single(n=>n.Id==d.Root).Parameters["speed"]=9;
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-double-shot")));Assert.IsFalse(_arena.Reload());Assert.AreSame(previous,_arena.Session);StringAssert.Contains("projectile:",_arena.LastLoadError);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
