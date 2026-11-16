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
    public sealed class ConstructPlayModeTests
    {
        string _directory;
        SkillLabRunner _runner;
        ArenaBootstrap _arena;
        SkillProjectDocument _project;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Constructs-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            _project.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=0,Hp=1000}};var graph=_project.Skills.Single(s=>s.Id=="barrel-chain");
            graph.Nodes.Single(n=>n.Kind=="cast").Parameters["delay"]=0;graph.Nodes.Single(n=>n.Kind=="damage").Parameters["canCrit"]=false;
            graph.Nodes.Single(n=>n.Kind=="construct").Parameters["decayPeriod"]=0;Publish();
            yield return SkillLabTestScene.Load();
            _runner=Object.FindFirstObjectByType<SkillLabRunner>();_runner.AutoAdvance=false;_runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        void Publish()
        {
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,"barrel-chain")));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,"detonate-constructs")));
        }
        [UnityTearDown]
        public IEnumerator TearDown(){if(_runner!=null)Object.Destroy(_runner.gameObject);if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator RealDiskObjectsRenderThenAllThreeBlastsResolveAndRemoveTheirViews()
        {
            Assert.IsTrue(_runner.CastSlot("f6",0,0).Accepted);_runner.StepLogicForTests(25);yield return null;
            Assert.AreEqual(3,_runner.Snapshot.Actors.Count(a=>a.Kind=="construct"));Assert.AreEqual(3,_runner.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("construct_")));
            Assert.IsTrue(_runner.CastSlot("f7",0,0).Accepted);_runner.StepLogicForTests(1);yield return null;
            Assert.AreEqual(964,_runner.Snapshot.Actors.Single(a=>a.Kind=="target").Hp);Assert.AreEqual(0,_runner.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("construct_")));LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ManualRemovalAndReloadNeverConvertCleanupIntoDeathDamage()
        {
            _runner.CastSlot("f6",0,0);_runner.StepLogicForTests(25);var body=_runner.Snapshot.Actors.First(a=>a.Kind=="construct");_runner.RemoveEntity(body.Id);_runner.StepLogicForTests(1);
            Assert.AreEqual(1000,_runner.Snapshot.Actors.Single(a=>a.Kind=="target").Hp);Assert.IsFalse(_runner.Snapshot.Events.Any(e=>e.Kind=="construct-died"));
            Assert.IsTrue(_runner.ReloadPublished());yield return null;Assert.AreEqual(0,_runner.Snapshot.Actors.Count(a=>a.Kind=="construct"));Assert.AreEqual(0,_runner.Snapshot.Statistics.Damage);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator FormalFire5SpawnsTheJsonBarrelWithBoundViewAndPointExplosionCue()
        {
            Object.Destroy(_runner.gameObject);yield return null;
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            var arena=Object.FindFirstObjectByType<ArenaBootstrap>();arena.AutoAdvance=false;arena.SetTraining(true);
            _arena=arena;
            arena.Submit(new ArenaInputFrame{Pressed=new[]{"Fire5"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});arena.StepForTests(5);yield return null;
            var actor=arena.Session.World.RegistryActive().Single(a=>a.TryGetComp<ConstructComp>(out _));var id=actor.Id;var point=actor.GetComp<TransformComp>().Position;
            Assert.AreEqual(.55f,point.X,.0001f);Assert.AreEqual(5,actor.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            var formal=new RuntimeProjectLoader(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));
            var key=actor.GetComp<ConstructComp>().Def.View;var prefab=Resources.Load<GameObject>(formal.Content.Runtime.Views[key].Resource);
            var view=arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==id);
            Assert.IsNotNull(view.transform.Find("Visual/"+prefab.name+"(Clone)"));Assert.IsNotNull(view.GetComponentInChildren<Renderer>());
            arena.Session.World.Deliver(new IEffect[]{new DamageEffect{Flat=9999,Coeff=0}},actor,actor,0);yield return null;
            Assert.IsFalse(arena.Session.World.TryGetActor(id,out _));var cue=arena.transform.Find("ArenaRuntime/Effects/Cue_4206");Assert.IsNotNull(cue);Assert.AreEqual(point.X,cue.position.x,.0001f);
            arena.Restart();yield return null;Assert.AreEqual(0,arena.GetComponentsInChildren<UnityActorView>().Count(v=>v.name.StartsWith("construct_")));LogAssert.NoUnexpectedReceived();
        }
    }
}
