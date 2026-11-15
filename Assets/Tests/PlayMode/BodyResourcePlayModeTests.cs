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
    public sealed class BodyResourcePlayModeTests
    {
        string _directory;SkillLabRunner _lab;ArenaBootstrap _arena;
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        void Files(SkillProjectDocument p)
        {
            _directory=Path.Combine(Application.streamingAssetsPath,"Combat/.body-resources-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            File.WriteAllText(Path.Combine(_directory,"project.json"),SkillProjectJson.Write(p));
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-ammo-shot")));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-bomb-shot")));
        }
        [UnityTearDown]
        public IEnumerator Cleanup(){if(_lab!=null)Object.Destroy(_lab.gameObject);if(_arena!=null)Object.Destroy(_arena.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}
        [UnityTest]
        public IEnumerator SkillLabShowsThePublishedBodyAndNewFilesRemainPendingUntilExplicitReload()
        {
            var p=Formal();p.Preview.Caster.X=0;p.Preview.Targets=Array.Empty<PreviewActorDocument>();Files(p);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            string previous=_lab.SlotRevisions["f6"];Assert.IsTrue(_lab.CastSlot("f6",5,0).Accepted);_lab.StepLogicForTests(15);yield return null;
            var shot=_lab.Snapshot.Actors.Single(a=>a.Kind=="projectile");Assert.AreEqual("buff_projectile_normal_view",shot.View);Assert.AreEqual(6,shot.Speed);
            Assert.IsTrue(_lab.GetComponentsInChildren<Transform>().Any(t=>t.name=="BulletNormal0(Clone)"));
            var d=p.Projectiles.Single(x=>x.Id=="arena-normal-projectile");d.Nodes.Single(n=>n.Id==d.Root).Parameters["speed"]=9;var updated=SkillPublicationJson.Create(p,"arena-ammo-shot");
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(updated));_lab.CheckPublications();Assert.IsTrue(_lab.PendingPublication);Assert.AreEqual(previous,_lab.SlotRevisions["f6"]);
            Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);Assert.IsFalse(_lab.PendingPublication);Assert.AreEqual(updated.Revision,_lab.SlotRevisions["f6"]);
            Assert.IsTrue(_lab.CastSlot("f6",5,0).Accepted);_lab.StepLogicForTests(15);Assert.AreEqual(9,_lab.Snapshot.Actors.Single(a=>a.Kind=="projectile").Speed);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator MissingPublishedBodyResourcesKeepTheCurrentSkillLabWorldAndVersion()
        {
            var p=Formal();Files(p);yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);_lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished());int generation=_lab.Generation;string revision=_lab.Revision;
            p.Runtime.Views.Single(v=>v.Id=="buff_projectile_normal_view").Resource="Combat/MissingResource";File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-ammo-shot")));File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-bomb-shot")));
            Assert.IsFalse(_lab.ReloadPublished());Assert.AreEqual(generation,_lab.Generation);Assert.AreEqual(revision,_lab.Revision);StringAssert.Contains("Missing Unity prefab",_lab.LastLoadError);yield return null;LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator ArenaUsesOriginalBoomerangSpinAndMuzzleAnchorsWithUrpMaterials()
        {
            Files(Formal());yield return SceneManager.LoadSceneAsync("Assets/Scenes/Arena.unity",LoadSceneMode.Single);yield return null;_arena=Object.FindFirstObjectByType<ArenaBootstrap>();_arena.AutoAdvance=false;_arena.Configure("Combat/"+Path.GetFileName(_directory)+"/project.json",true);Assert.IsTrue(_arena.Reload(false));
            _arena.Submit(new ArenaInputFrame{Pressed=new[]{"Fire2"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});_arena.StepForTests(20);yield return null;
            var body=_arena.Session.World.RegistryActive().Single(a=>a.TryGetComp<ProjectileComp>(out _));var view=_arena.GetComponentsInChildren<UnityActorView>().Single(v=>v.Id==body.Id);
            Assert.IsTrue(view.GetComponentsInChildren<Transform>().Any(t=>t.name=="Boomerang(Clone)"));Assert.IsNull(view.Animation);
            foreach(var renderer in view.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)Assert.IsTrue(material.shader.name.StartsWith("Universal Render Pipeline/"));
            _arena.Session.World.TryGetActor(_arena.Session.PlayerId,out var player);player.GetComp<SkillDirectorComp>().Stop(DirectorStopReason.Manual);player.GetComp<InputBufferComp>().Push(new InputToken("ArenaPrimary"));_arena.StepForTests(6);yield return null;
            Assert.IsTrue(_arena.GetComponentsInChildren<Transform>().Any(t=>t.name=="Cue_4201"&&t.parent.name=="CueAnchor_Muzzle"));_arena.Restart();yield return null;Assert.IsFalse(_arena.GetComponentsInChildren<UnityActorView>().Any(v=>v.GetComponentsInChildren<Transform>().Any(t=>t.name=="Boomerang(Clone)")));LogAssert.NoUnexpectedReceived();
        }
    }
}
