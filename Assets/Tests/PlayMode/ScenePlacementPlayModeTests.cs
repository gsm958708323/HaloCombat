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
    public sealed class ScenePlacementPlayModeTests
    {
        string _directory;SkillLabRunner _lab;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);_directory=null;_lab=null;}
        [UnityTest]
        public IEnumerator PlacementKeepsTheRunningWorldAndReplaysItsExactSameStepChangeInUnity()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
            string text=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"basic-shot"));
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Placement-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);File.WriteAllText(Path.Combine(_directory,"f6.json"),text);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);
            _lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished());
            Assert.IsTrue(_lab.CastSlot("f6",0,0).Accepted);_lab.StepLogicForTests(10);int generation=_lab.Generation;
            var before=_lab.Snapshot;var target=before.Actors.Single(a=>a.SceneId==p.Preview.Targets[0].Id);var body=before.Actors.Single(a=>a.Kind=="projectile");
            _lab.PlaceActor(target.SceneId,target.Id,2,2);Assert.AreEqual(10,_lab.Snapshot.Step);Assert.AreEqual(generation,_lab.Generation);
            Assert.AreEqual(body.X,_lab.Snapshot.Actors.Single(a=>a.Id==body.Id).X);Assert.AreEqual(2,_lab.Snapshot.Actors.Single(a=>a.Id==target.Id).Z);
            var saved=JObject.FromObject(_lab.Snapshot);string file=Path.Combine(_directory,"placement.json");File.WriteAllText(file,_lab.CaptureReplayJson());
            Assert.IsTrue(_lab.LoadReplayFile(file),_lab.LastLoadError);Assert.IsTrue(_lab.ReplayComparison.Matches);Assert.IsTrue(JToken.DeepEquals(saved,JObject.FromObject(_lab.Snapshot)));
            yield return null;Assert.AreEqual(10,_lab.Snapshot.Step);LogAssert.NoUnexpectedReceived();
        }
    }
}
