using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Combat.Tests
{
    public sealed class BurstPreviewPlayModeTests
    {
        string _directory;Combat.Unity.SkillLabRunner _lab;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);_directory=null;_lab=null;}
        [UnityTest]
        public IEnumerator PublishedTwentyShotRandomBurstAndDisabledSingleShotUseTheSameFileInUnity()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            var s=p.Skills.Single(v=>v.Id=="basic-shot");var cast=s.Nodes.Single(n=>n.Kind=="cast");s.Nodes.Single(n=>n.Kind=="target").Parameters["mode"]="direction";
            cast.Parameters=JObject.FromObject(new{mode="instant",burst=true,count=20,interval=.04,delay=0,cost=0,cooldown=0,duration=.9});
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Burst-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            string publication=SkillPublicationJson.Write(SkillPublicationJson.Create(p,s.Id));File.WriteAllText(Path.Combine(_directory,"f6.json"),publication);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);
            _lab=Object.FindFirstObjectByType<Combat.Unity.SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            using(var reference=new PublishedSkillSession())
            {
                reference.Replace(new System.Collections.Generic.Dictionary<string,string>{{"f6",publication}});
                reference.Simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId=s.Id,X=0,Z=0});Assert.IsTrue(_lab.CastSlot("f6",0,0).Accepted);
                reference.Simulation.Advance(45);_lab.StepLogicForTests(45);
                Assert.AreEqual(20,_lab.Snapshot.Statistics.Projectiles);Assert.Greater(_lab.Snapshot.Actors.Where(a=>a.Kind=="projectile").Select(a=>a.Yaw).Distinct().Count(),10);
                Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(reference.Simulation.Snapshot()),JObject.FromObject(_lab.Snapshot)));
                string replay=Path.Combine(_directory,"burst-replay.json");File.WriteAllText(replay,_lab.CaptureReplayJson());Assert.IsTrue(_lab.LoadReplayFile(replay),_lab.LastLoadError);
            }
            cast.Parameters["burst"]=false;File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,s.Id)));
            Assert.IsTrue(_lab.ReloadPublished());_lab.CastSlot("f6",0,0);_lab.StepLogicForTests(45);Assert.AreEqual(1,_lab.Snapshot.Statistics.Projectiles);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
