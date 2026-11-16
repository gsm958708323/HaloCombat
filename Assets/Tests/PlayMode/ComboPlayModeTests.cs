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
    public sealed class ComboPlayModeTests
    {
        ArenaBootstrap _arena;
        SkillLabRunner _lab;
        string _relative,_directory;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_arena!=null)Object.Destroy(_arena.gameObject);if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator FormalDiskComboRequiresCurrentContactAndRunsTheConfirmedTransition()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));
            p.Runtime.Arena.Bindings.Single(b=>b.Action=="Fire1").Input="ConfirmAttack";
            _relative="Combat/.combo-test-"+Guid.NewGuid().ToString("N");_directory=Path.Combine(Application.streamingAssetsPath,_relative);Directory.CreateDirectory(_directory);File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure(_relative+"/project.json",true);Assert.IsTrue(_arena.Reload(false),_arena.LastLoadError);_arena.StepForTests(1);
            var s=_arena.Session;s.World.TryGetActor(s.PlayerId,out var actor);actor.GetComp<ComboComp>().DiagnosticsEnabled=true;
            var target=s.World.RegistryActive().Single(a=>a.Id!=s.PlayerId);target.GetComp<TransformComp>().Position=new SimVec3(.55f,0,0);target.GetComp<AttributeSet>().SetBase(AttrId.Hp,1000);
            var press=new ArenaInputFrame{Pressed=new[]{"Fire1"},AimValid=true,Aim=new SkillAim(target.Id,new SimVec3(4,0,0),0)};
            _arena.Submit(press);_arena.StepForTests(10);Assert.IsTrue(actor.GetComp<SkillDirectorComp>().HasComboConfirmation);Assert.AreEqual(SkillNodeId.G1,actor.GetComp<SkillDirectorComp>().CurrentSkill);
            _arena.Submit(press);_arena.StepForTests(4);Assert.AreEqual(SkillNodeId.G2,actor.GetComp<SkillDirectorComp>().CurrentSkill);Assert.IsFalse(actor.GetComp<InputBufferComp>().TryPeek(out _));
            Assert.IsFalse(actor.GetComp<SkillDirectorComp>().HasComboConfirmation);
            var decision=actor.GetComp<ComboComp>().LastDecision;Assert.AreEqual("cast",decision.Phase);Assert.AreEqual("confirmed-g1-g2",decision.Rule);Assert.AreEqual(SkillNodeId.G2,decision.ActualSkill);
            Assert.IsTrue(decision.Confirmed);Assert.IsTrue(decision.WindowOpen);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RejectedComboTableCannotReplaceTheActiveArenaWorld()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));
            _relative="Combat/.combo-test-"+Guid.NewGuid().ToString("N");_directory=Path.Combine(Application.streamingAssetsPath,_relative);Directory.CreateDirectory(_directory);var path=Path.Combine(_directory,"project.json");File.WriteAllText(path,SkillProjectJson.Write(p));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure(_relative+"/project.json",true);Assert.IsTrue(_arena.Reload(false));var old=_arena.Session;
            var source=p.Runtime.Combos.Single(c=>c.Id=="confirmed-g1-g2");p.Runtime.Combos=p.Runtime.Combos.Concat(new[]{new RuntimeComboDocument{Id="duplicate-priority",Input=source.Input,PreSkills=source.PreSkills,Priority=source.Priority,ToSkill="g1"}}).ToArray();File.WriteAllText(path,SkillProjectJson.Write(p));
            Assert.IsFalse(_arena.Reload(false));Assert.AreSame(old,_arena.Session);StringAssert.Contains("priority",_arena.LastLoadError);_arena.StepForTests(5);Assert.AreEqual(5,old.World.Time.Frame);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator SkillOnlyPublicationsCannotInventMissingGameplayComboBindings()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Combo-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));p.Preview.Caster.X=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=.55f,Hp=1000}};
            // A publication deliberately excludes gameplay Combo bindings. The real formal
            // table is installed here through the same shared SkillSimulation as the host.
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"g1")));
            yield return SkillLabTestScene.Load();_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished());
            _lab.SubmitInput("ConfirmAttack",4,0);_lab.StepLogicForTests(1);Assert.AreEqual(0,_lab.Snapshot.Statistics.Casts);Assert.AreEqual("ConfirmAttack",_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").BufferedInput);
            Assert.AreEqual("unmatched",_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").ComboDecision.Phase);
            Assert.IsTrue(_lab.CastSlot("f6",4,0).Accepted);_lab.StepLogicForTests(1);Assert.AreEqual(1,_lab.Snapshot.Statistics.Casts);yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
