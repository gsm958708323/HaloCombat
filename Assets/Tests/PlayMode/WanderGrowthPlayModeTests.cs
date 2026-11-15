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
    public sealed class WanderGrowthPlayModeTests
    {
        string _directory;ArenaBootstrap _arena;
        static SkillProjectDocument Formal()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
        void WriteFiles(SkillProjectDocument p)
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.wander-growth-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            System.IO.File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
        }
        IEnumerator Open()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",false);Assert.IsTrue(_arena.Reload(),_arena.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator Cleanup(){if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator RealArenaShowsSampledHealthMovementAndTheOriginalFirstShotBoundary()
        {
            var p=Formal();p.Runtime.Arena.MaxEnemies=2;WriteFiles(p);yield return Open();_arena.StepForTests(1);var world=_arena.Session.World;
            var enemies=world.RegistryActive().Where(a=>a.Id!=_arena.Session.PlayerId).ToArray();CollectionAssert.AreEqual(new[]{50f,52f},enemies.Select(a=>a.GetComp<AttributeSet>().GetBase(AttrId.MaxHp)));
            var positions=enemies.Select(a=>a.GetComp<TransformComp>().Position).ToArray();int enemyCasts=0;world.Events.Subscribe<EvSkillStarted>(e=>{if(e.Source!=_arena.Session.PlayerId)enemyCasts++;});
            _arena.StepForTests(149);Assert.AreEqual(0,enemyCasts);Assert.IsTrue(enemies.Where((a,i)=>SpatialGeometry.DistanceSquared(positions[i],a.GetComp<TransformComp>().Position)>.1f).Any());
            _arena.StepForTests(1);Assert.AreEqual(2,enemyCasts);_arena.StepForTests(12);Assert.IsTrue(world.RegistryActive().Any(a=>a.TryGetComp<ProjectileComp>(out var b)&&b.Def.SpecId==4002));yield return null;
            Assert.AreEqual(2,_arena.GetComponentsInChildren<UnityActorView>().Count(v=>enemies.Any(a=>a.Id==v.Id)));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator DiskGrowthAndTimingEditsApplyOnlyOnReloadAndRestartResetsTheOrdinal()
        {
            var p=Formal();p.Runtime.Arena.MaxEnemies=1;WriteFiles(p);yield return Open();_arena.StepForTests(1);var previous=_arena.Session;
            var e=p.Runtime.Actors.Single(a=>a.Id=="arena-enemy");e.Hp=80;e.SpawnStats.HpPerSpawn=7;e.Wander.FirstShot=.2f;
            System.IO.File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));Assert.AreEqual(50,_arena.Session.World.RegistryActive().Single(a=>a.Id!=_arena.Session.PlayerId).GetComp<AttributeSet>().GetBase(AttrId.MaxHp));
            Assert.IsTrue(_arena.Reload(false));Assert.AreNotSame(previous,_arena.Session);_arena.StepForTests(1);Assert.AreEqual(80,_arena.Session.World.RegistryActive().Single(a=>a.Id!=_arena.Session.PlayerId).GetComp<AttributeSet>().GetBase(AttrId.MaxHp));
            int shots=0;_arena.Session.World.Events.Subscribe<EvSkillStarted>(v=>{if(v.Source!=_arena.Session.PlayerId)shots++;});_arena.StepForTests(10);Assert.AreEqual(1,shots);
            _arena.Restart();_arena.StepForTests(1);Assert.AreEqual(1,_arena.Session.SpawnedEnemies);Assert.AreEqual(80,_arena.Session.World.RegistryActive().Single(a=>a.Id!=_arena.Session.PlayerId).GetComp<AttributeSet>().GetBase(AttrId.MaxHp));yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator InvalidGrowthCannotReplaceTheCurrentWorldOrItsRandomSequence()
        {
            var p=Formal();WriteFiles(p);yield return Open();_arena.StepForTests(25);var previous=_arena.Session;
            p.Runtime.Actors.Single(a=>a.Id=="arena-enemy").Wander.ShotIntervalMax=0;System.IO.File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
            Assert.IsFalse(_arena.Reload(false));Assert.AreSame(previous,_arena.Session);_arena.StepForTests(126);Assert.Greater(_arena.Session.World.Time.Frame,150);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
