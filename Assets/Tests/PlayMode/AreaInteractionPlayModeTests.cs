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
    public sealed class AreaInteractionPlayModeTests
    {
        ArenaBootstrap _arena;
        SkillLabRunner _lab;
        string _directory;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_arena!=null)Object.Destroy(_arena.gameObject);if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        void Files(SkillProjectDocument project,string orb,string shot)
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.area-interaction-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(project));File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(project,orb)));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(project,shot)));
        }
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));
        [UnityTest]
        public IEnumerator DiskPublicationsRunTheSameGrowingAreaAndFullSnapshotsInUnityAndCore()
        {
            var p=Formal();p.Preview.Caster.X=0;p.Preview.Caster.Z=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();Files(p,"arena-absorbing-orb","arena-ammo-shot");
            using(var reference=new PublishedSkillSession())
            {
                reference.Replace(new System.Collections.Generic.Dictionary<string,string>{{"f6",File.ReadAllText(Path.Combine(_directory,"f6.json"))},{"f7",File.ReadAllText(Path.Combine(_directory,"f7.json"))}});
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
                Assert.IsTrue(_lab.CastSlot("f6",5,0).Accepted);reference.Simulation.Cast("arena-absorbing-orb",5,0);_lab.StepLogicForTests(30);reference.Simulation.Advance(30);
                Assert.IsTrue(_lab.CastSlot("f7",5,0).Accepted);reference.Simulation.Cast("arena-ammo-shot",5,0);_lab.StepLogicForTests(40);reference.Simulation.Advance(40);
                Assert.IsTrue(_lab.CastSlot("f7",5,0).Accepted);reference.Simulation.Cast("arena-ammo-shot",5,0);_lab.StepLogicForTests(40);reference.Simulation.Advance(40);
                var area=_lab.Snapshot.Actors.Single(a=>a.Kind=="aoe");Assert.AreEqual(2,area.AbsorbedProjectiles);Assert.AreEqual(.275f,area.Radius,.0001);Assert.AreEqual(.6f,area.AreaVelocityX.Value,.0001);Assert.AreEqual(55,_lab.Snapshot.Ammo);
                Assert.IsTrue(Newtonsoft.Json.Linq.JToken.DeepEquals(Newtonsoft.Json.Linq.JToken.FromObject(reference.Simulation.Snapshot()),Newtonsoft.Json.Linq.JToken.FromObject(_lab.Snapshot)));
                yield return null;var visual=_lab.GetComponentsInChildren<Transform>().Single(t=>t.name=="aoe_"+area.Name);
                Assert.AreEqual(area.ViewScale,visual.Find("Visual").localScale.x,.0001);Assert.IsNotNull(visual.GetComponentInChildren<Renderer>());LogAssert.NoUnexpectedReceived();
            }
        }
        [UnityTest]
        public IEnumerator FormalMonkeyInputCreatesTheActualAreaAndRestartReleasesItsGrownView()
        {
            Files(Formal(),"arena-absorbing-orb","arena-ammo-shot");yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(),_arena.LastLoadError);
            _arena.Submit(new ArenaInputFrame{Pressed=new[]{"Monkey"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});_arena.StepForTests(30);
            var area=_arena.Session.World.RegistryActive().Single(a=>a.TryGetComp<AoeComp>(out _));Assert.AreEqual(4102,area.GetComp<AoeComp>().Def.SpecId);Assert.IsTrue(_arena.CastPublished("f7"));_arena.StepForTests(40);yield return null;
            var body=area.GetComp<AoeComp>();Assert.AreEqual(1,body.AbsorbedProjectiles);var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==area.Id);
            Assert.AreEqual(body.Radius/body.Def.Radius,view.transform.Find("Visual").localScale.x,.0001f);Assert.IsNotNull(view.GetComponentInChildren<Renderer>());
            _arena.Restart();yield return null;Assert.IsTrue(view==null);Assert.IsFalse(_arena.Session.World.RegistryActive().Any(a=>a.TryGetComp<AoeComp>(out _)));Assert.AreEqual(1,_arena.GetComponentsInChildren<UnityActorView>().Length);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RealPublicationReturnsThroughTheSameTargetTwiceAndEndsCleanly()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));p.Preview.Caster.X=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=2,Hp=1000}};Files(p,"repeat-contact","absorbing-orb");
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            Assert.IsTrue(_lab.CastSlot("f6",5,0).Accepted);_lab.StepLogicForTests(80);Assert.AreEqual(2,_lab.Snapshot.Statistics.Hits);Assert.AreEqual(160,_lab.Snapshot.Statistics.Damage,.001);Assert.IsFalse(_lab.Snapshot.Actors.Any(a=>a.Kind=="projectile"));
            yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator InvalidAreaMotionCannotReplaceTheWorkingArena()
        {
            var p=Formal();Files(p,"arena-absorbing-orb","arena-ammo-shot");yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(false));var previous=_arena.Session;
            p.Skills.Single(s=>s.Id=="arena-absorbing-orb").Nodes.Single(n=>n.Id=="orb").Parameters["moveSpeed"]=-1;File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
            Assert.IsFalse(_arena.Reload(false));Assert.AreSame(previous,_arena.Session);StringAssert.Contains("moveSpeed",_arena.LastLoadError);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
