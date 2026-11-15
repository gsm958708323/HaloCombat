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
    public sealed class OriginalArenaAuditPlayModeTests
    {
        string _directory;ArenaBootstrap _arena;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.original-audit-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"))));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;
            _arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(false),_arena.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown(){if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator SameFrameQueuedPressesExpireWhileTheRealRollAnimationFinishes()
        {
            _arena.Submit(new ArenaInputFrame{Pressed=new[]{"Roll","Homing","Fire1"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});
            _arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var player);Assert.AreEqual(3,player.GetComp<InputBufferComp>().Count);
            _arena.StepForTests(1);Assert.AreEqual(2002,player.GetComp<SkillDirectorComp>().CurrentSkill.Value);
            _arena.StepForTests(51);Assert.IsFalse(player.GetComp<SkillDirectorComp>().IsPlaying);Assert.AreEqual(0,player.GetComp<InputBufferComp>().Count);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator BuffTiersFromDifferentCastsAllStopOnRemovalAndRestart()
        {
            _arena.StepForTests(1);var world=_arena.Session.World;world.TryGetActor(_arena.Session.PlayerId,out var source);
            var target=world.RegistryActive().Single(a=>a.Id!=source.Id);var loader=UnityRuntimeContent.Load();loader.Content.Buffs.TryGet(5102,out var definition);
            for(int i=1;i<=3;i++)target.GetComp<BuffComp>().Apply(definition,source,1,origin:new SkillOrigin(new SkillNodeId(2005),i,caster:source.Id));
            yield return null;Assert.AreEqual(3,_arena.GetComponentsInChildren<UnityCueLoop>().Length);
            Assert.IsTrue(_arena.GetComponentsInChildren<UnityCueLoop>().All(v=>v.name=="Cue_4210"&&!v.StopWithCast));
            target.GetComp<BuffComp>().RemoveInstance(target.GetComp<BuffComp>().Snapshot().Single().InstanceId);
            yield return null;yield return null;Assert.AreEqual(0,_arena.GetComponentsInChildren<UnityCueLoop>().Length);
            target.GetComp<BuffComp>().Apply(definition,source,1);yield return null;Assert.AreEqual(1,_arena.GetComponentsInChildren<UnityCueLoop>().Length);
            _arena.Restart();yield return null;yield return null;Assert.AreEqual(0,_arena.GetComponentsInChildren<UnityCueLoop>().Length);LogAssert.NoUnexpectedReceived();
        }
    }
}
