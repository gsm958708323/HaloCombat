using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Unity;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Combat.Tests
{
    public sealed class RestoredAreaOperationPlayModeTests
    {
        string _directory;SkillLabRunner _lab;
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        void Publish(SkillProjectDocument p,string skill)
        {
            if(_directory==null){_directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Restored-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);}
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,skill)));
        }
        IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);
            _lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown(){if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);_directory=null;_lab=null;}
        [UnityTest]
        public IEnumerator TheRealDiskRecastConsumesItsBodyBeforeTheNextStepAndDoesNotStartAnotherAction()
        {
            var p=Formal();p.Preview.Targets=Array.Empty<PreviewActorDocument>();Publish(p,"arena-tracked-shot");yield return Load();
            Assert.IsTrue(_lab.CastSlot("f6",20,0).Accepted);_lab.StepLogicForTests(26);var point=_lab.Snapshot.Actors.Single(a=>a.Kind=="projectile").X;
            Assert.IsTrue(_lab.CastSlot("f6",20,0).Accepted);Assert.AreEqual(26,_lab.Snapshot.Step);Assert.AreEqual(1,_lab.Snapshot.Statistics.Casts);
            Assert.IsFalse(_lab.Snapshot.Actors.Any(a=>a.Kind=="projectile"));Assert.IsNull(_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").CurrentSkill);
            _lab.StepLogicForTests(1);Assert.AreEqual(point,_lab.Snapshot.Actors.Single(a=>a.Kind=="caster").X,.00001f);
            string file=Path.Combine(_directory,"replay.json");File.WriteAllText(file,_lab.CaptureReplayJson());Assert.IsTrue(_lab.LoadReplayFile(file),_lab.LastLoadError);
            yield return null;Assert.IsTrue(_lab.ReplayComparison.Matches);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator RestoredAreaFilesUseOriginalBodyResourcesAndCleanUpAcrossReloadAndDeath()
        {
            var p=Formal();p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=1,Hp=1000}};
            var s=p.Skills.Single(v=>v.Id=="arena-projectile-warp");var cast=s.Nodes.Single(n=>n.Kind=="cast");cast.Parameters=JObject.FromObject(new{mode="instant",execution="operation",cost=0,cooldown=0});
            var effect=s.Nodes.Single(n=>n.Kind=="teleport-projectile");effect.Kind="area-ref";effect.Parameters=JObject.FromObject(new{area="arena-black-hole",center="source"});
            Publish(p,s.Id);yield return Load();_lab.CastSlot("f6",0,0);_lab.StepLogicForTests(30);yield return null;
            Assert.Less(_lab.Snapshot.Actors.Single(a=>a.Name=="target").X,1);Assert.AreEqual("buff_aoe_blackhole_view",_lab.Snapshot.Actors.Single(a=>a.Kind=="aoe").View);
            _lab.StepLogicForTests(20);Assert.IsFalse(_lab.Snapshot.Actors.Any(a=>a.Kind=="aoe"));
            effect.Parameters["area"]="arena-absorbing-shield";Publish(p,s.Id);Assert.IsTrue(_lab.ReloadPublished());_lab.CastSlot("f6",0,0);_lab.StepLogicForTests(60);yield return null;
            Assert.AreEqual("buff_aoe_shield_view",_lab.Snapshot.Actors.Single(a=>a.Kind=="aoe").View);Assert.IsNull(_lab.Snapshot.Actors.Single(a=>a.Kind=="aoe").Remaining);
            _lab.Interrupt(true);_lab.StepLogicForTests(1);yield return null;Assert.IsFalse(_lab.Snapshot.Actors.Any(a=>a.Kind=="aoe"));LogAssert.NoUnexpectedReceived();
        }
    }
}
