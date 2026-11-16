using System;
using System.Collections;
using System.IO;
using Combat.Config;
using Combat.Unity;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    public sealed class SkillReplayPlayModeTests
    {
        string _directory;
        SkillLabRunner _runner;
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-Replay-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,"held-spray")));
            yield return SkillLabTestScene.Load();
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();_runner.AutoAdvance=false;_runner.ConfigureContentDirectory(_directory);
            Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;
            if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);
        }
        [UnityTest]
        public IEnumerator RecordedUnityInputReplaysFromDiskAndStaysAtTheFinalStep()
        {
            Assert.IsTrue(_runner.CastSlot("f6",0,0).Accepted);_runner.SetMovement(0,1);_runner.StepLogicForTests(15);
            _runner.ReleaseCast();_runner.SetMovement(0,0);_runner.StepLogicForTests(60);
            var before=JObject.FromObject(_runner.Snapshot);string file=Path.Combine(_directory,"input-replay.json");File.WriteAllText(file,_runner.CaptureReplayJson());
            _runner.AutoAdvance=true;
            Assert.IsTrue(_runner.LoadReplayFile(file),_runner.LastLoadError);Assert.IsTrue(_runner.ReplayMode);Assert.IsFalse(_runner.AutoAdvance);
            Assert.IsTrue(_runner.ReplayComparison.Matches);Assert.IsTrue(JToken.DeepEquals(before,JObject.FromObject(_runner.Snapshot)));
            _runner.SendMessage("OnApplicationFocus",false);yield return null;yield return null;
            Assert.IsTrue(JToken.DeepEquals(before,JObject.FromObject(_runner.Snapshot)),"Focus changes must not modify the replay result.");
            Assert.IsTrue(_runner.ReloadPublished());Assert.IsFalse(_runner.ReplayMode);Assert.IsTrue(_runner.AutoAdvance);Assert.AreEqual(0,_runner.Snapshot.Step);LogAssert.NoUnexpectedReceived();
        }
        [UnityTest]
        public IEnumerator CorruptOrMismatchingReplayKeepsTheRunningWorldAndGeneration()
        {
            _runner.CastSlot("f6",0,0);_runner.StepLogicForTests(15);string revision=_runner.Revision;int generation=_runner.Generation;
            string file=Path.Combine(_directory,"bad-replay.json");File.WriteAllText(file,"{broken");Assert.IsFalse(_runner.LoadReplayFile(file));
            var token=JObject.Parse(_runner.CaptureReplayJson());token["expectedState"]["energy"]=-100;
            File.WriteAllText(file,token.ToString());Assert.IsFalse(_runner.LoadReplayFile(file));
            Assert.AreEqual(generation,_runner.Generation);Assert.AreEqual(revision,_runner.Revision);Assert.IsFalse(_runner.ReplayMode);
            Assert.IsTrue(_runner.Snapshot.Channeling);_runner.StepLogicForTests(5);Assert.AreEqual(20,_runner.Snapshot.Step);
            _runner.ReleaseCast();yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
