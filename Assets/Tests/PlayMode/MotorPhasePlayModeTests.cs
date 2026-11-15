using System;
using System.Collections;
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
    public sealed class MotorPhasePlayModeTests
    {
        string _directory;
        SkillLabRunner _lab;
        ArenaBootstrap _arena;
        static SkillProjectDocument Project(float distance)
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,UnityRuntimeContent.DefaultFile));
            var s=p.Skills.Single(v=>v.Id=="arena-controlled-shot");s.Nodes=s.Nodes.Where(n=>n.Kind!="projectile").ToArray();s.Edges=s.Edges.Where(e=>e.Source!="shot").ToArray();
            s.Nodes.Single(n=>n.Id=="damage").Parameters=JObject.FromObject(new{flat=10,coefficient=0,canCrit=false});
            s.Timeline=new TimelineDocument{RuntimeId=3015,Duration=.02f,ControlTags=new[]{1011},Clips=new[]{
                new TimelineClipDocument{Id="move",Kind="move",Start=0,End=.02f,MoveX=distance},
                new TimelineClipDocument{Id="contact",Kind="hitbox",Start=0,End=.02f,HitRadius=.3f,Effects=new[]{"damage"}}}};
            p.Preview.Caster.X=0;p.Preview.Caster.Z=0;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target-1",X=distance+.2f,Hp=1000}};
            return p;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(_lab!=null)Object.Destroy(_lab.gameObject);if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;
            if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);
        }

        [UnityTest]
        public IEnumerator ARealPublishedOneStepSkillMovesHitsCompletesAndClearsItsFinalHitbox()
        {
            var p=Project(1);_directory=Path.Combine(Path.GetTempPath(),"HaloCombat-MotorPhase-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-controlled-shot")));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);
            _lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            Assert.IsTrue(_lab.CastSlot("f6",4,0).Accepted);_lab.SetMovement(0,1);_lab.StepLogicForTests(1);
            var caster=_lab.Snapshot.Actors.Single(a=>a.Kind=="caster");
            Assert.AreEqual(1,caster.X,.0001);Assert.AreEqual(0,caster.Z);Assert.AreEqual(10,_lab.Snapshot.Statistics.Damage);Assert.AreEqual(1,_lab.Snapshot.Statistics.Hits);
            Assert.IsNull(caster.ActionTime);Assert.AreEqual(0,caster.ControlTags.Length);Assert.AreEqual(1,_lab.Snapshot.Events.Count(e=>e.Kind=="completed"));
            _lab.StepLogicForTests(1);Assert.Greater(_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").Z,0);Assert.AreEqual(10,_lab.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ArenaLoadsTheFullJsonAndResolvesTheFinalStepContactBeforeReleasingControl()
        {
            string relative="Combat/.motor-phase-"+Guid.NewGuid().ToString("N");_directory=Path.Combine(Application.streamingAssetsPath,relative);Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(Project(4)));
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;
            _arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure(relative+"/project.json",true);Assert.IsTrue(_arena.Reload(false),_arena.LastLoadError);
            _arena.StepForTests(1);var world=_arena.Session.World;world.TryGetActor(_arena.Session.PlayerId,out var player);
            var target=world.RegistryActive().Single(a=>a.Id!=player.Id);target.GetComp<AttributeSet>().SetBase(AttrId.Hp,1000);float before=target.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            Assert.IsTrue(player.GetComp<SkillCastComp>().TryCast(new SkillNodeId(2015),new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)));
            _arena.Submit(new ArenaInputFrame{MoveZ=1});_arena.StepForTests(1);
            Assert.AreEqual(4,player.GetComp<TransformComp>().Position.X,.0001);Assert.AreEqual(0,player.GetComp<TransformComp>().Position.Z,.0001);
            Assert.AreEqual(before-10,target.GetComp<AttributeSet>().GetBase(AttrId.Hp),.0001);Assert.IsFalse(player.GetComp<HitboxComp>().IsOpen);Assert.IsFalse(player.GetComp<SkillDirectorComp>().IsPlaying);
            yield return null;_arena.GetComponent<ArenaPresentation>().Sample(1,0,0);
            var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==player.Id);Assert.AreEqual(4,view.transform.position.x,.0001);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
