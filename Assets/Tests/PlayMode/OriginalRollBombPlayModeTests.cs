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
    public sealed class OriginalRollBombPlayModeTests
    {
        string _directory;ArenaBootstrap _arena;SkillLabRunner _lab;
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        void Files(SkillProjectDocument p,string first="arena-bomb-shot",string second="arena-roll")
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.roll-bomb-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,first)));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,second)));
        }
        [UnityTearDown]
        public IEnumerator TearDown(){if(_arena!=null)Object.Destroy(_arena.gameObject);if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator RealBombAndRollPublicationsMatchCoreAcrossBothDelayedAreasAndMovement()
        {
            var p=Formal();p.Preview.Caster.X=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();Files(p);
            using(var reference=new PublishedSkillSession())
            {
                reference.Replace(new Dictionary<string,string>{{"f6",File.ReadAllText(Path.Combine(_directory,"f6.json"))},{"f7",File.ReadAllText(Path.Combine(_directory,"f7.json"))}});
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
                Assert.IsTrue(_lab.CastSlot("f6",4,0).Accepted);Assert.IsTrue(reference.Simulation.Cast("arena-bomb-shot",4,0).Accepted);
                foreach(int steps in new[]{29,81,150,160}){_lab.StepLogicForTests(steps);reference.Simulation.Advance(steps);Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(reference.Simulation.Snapshot()),JToken.FromObject(_lab.Snapshot)));}
                Assert.AreEqual(2,_lab.Snapshot.Events.Count(e=>e.Kind=="cue"&&e.Result=="4206"));Assert.IsFalse(_lab.Snapshot.Actors.Any(a=>a.Kind=="projectile"||a.Kind=="aoe"));
                Assert.IsTrue(_lab.CastSlot("f7",4,0).Accepted);Assert.IsTrue(reference.Simulation.Cast("arena-roll",4,0).Accepted);_lab.StepLogicForTests(45);reference.Simulation.Advance(45);
                Assert.IsTrue(JToken.DeepEquals(JToken.FromObject(reference.Simulation.Snapshot()),JToken.FromObject(_lab.Snapshot)));Assert.AreEqual(2,_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").X,.0001);yield return null;LogAssert.NoUnexpectedReceived();
            }
        }
        IEnumerator Arena()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(),_arena.LastLoadError);
        }
        [UnityTest]
        public IEnumerator RollCueLoopsFollowActorsAndReleaseSeparatelyOnInterruptBuffEndAndRestart()
        {
            Files(Formal());yield return Arena();_arena.Submit(new ArenaInputFrame{Pressed=new[]{"Roll"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});_arena.StepForTests(6);yield return null;
            Assert.AreEqual(2,_arena.GetComponentsInChildren<UnityCueLoop>().Length);_arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var player);Assert.AreEqual(2002,player.GetComp<SkillDirectorComp>().CurrentSkill.Value);
            player.GetComp<SkillDirectorComp>().Stop(DirectorStopReason.Manual);yield return null;yield return null;
            var remaining=_arena.GetComponentsInChildren<UnityCueLoop>();Assert.AreEqual(1,remaining.Length);Assert.AreEqual("Cue_4212",remaining[0].name);
            _arena.StepForTests(40);yield return null;yield return null;Assert.AreEqual(0,_arena.GetComponentsInChildren<UnityCueLoop>().Length);
            _arena.Restart();yield return null;Assert.AreEqual(0,_arena.GetComponentsInChildren<UnityCueLoop>().Length);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ArenaFire3RunsTheOriginalBouncingBombAndItsDelayedAreaChain()
        {
            Files(Formal());yield return Arena();_arena.StepForTests(1);
            var world=_arena.Session.World;foreach(var target in world.RegistryActive().Where(a=>a.Id!=_arena.Session.PlayerId))target.GetComp<TransformComp>().Position=new SimVec3(20,0,0);
            _arena.Submit(new ArenaInputFrame{Pressed=new[]{"Fire3"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});_arena.StepForTests(29);
            var body=world.RegistryActive().Single(a=>a.TryGetComp<ProjectileComp>(out _));Assert.AreEqual(4005,body.GetComp<ProjectileComp>().Def.SpecId);Assert.AreEqual(1,body.GetComp<TransformComp>().Position.Y,.001);
            _arena.StepForTests(81);Assert.AreEqual(1,world.RegistryActive().Count(a=>a.TryGetComp<AoeComp>(out var b)&&b.Def.SpecId==4105));_arena.StepForTests(150);
            Assert.IsTrue(world.RegistryActive().Any(a=>a.TryGetComp<AoeComp>(out var b)&&b.Def.SpecId==4106));_arena.Restart();yield return null;
            Assert.IsFalse(_arena.Session.World.RegistryActive().Any(a=>a.TryGetComp<AoeComp>(out _)||a.TryGetComp<ProjectileComp>(out _)));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ConflictingSharedAreaVersionsKeepTheExistingArenaAndViews()
        {
            var p=Formal();Files(p,"arena-bomb-shot","arena-bomb-shot");yield return Arena();var previous=_arena.Session;
            var area=p.Areas[0];area.Nodes.Single(n=>n.Id==area.Root).Parameters["radius"]=2;File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-bomb-shot")));
            Assert.IsFalse(_arena.Reload());Assert.AreSame(previous,_arena.Session);StringAssert.Contains("area:",_arena.LastLoadError);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
