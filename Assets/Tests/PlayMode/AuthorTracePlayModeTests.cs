using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed class AuthorTracePlayModeTests
    {
        string _directory;
        SkillLabRunner _lab;
        [UnityTearDown]
        public IEnumerator TearDown(){if(_lab!=null)Object.Destroy(_lab.gameObject);yield return null;if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);}

        [UnityTest]
        public IEnumerator PublishedDelayedBuffAndRecycledProjectileCarryExactSourceNodesInUnity()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
            var text=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"burn-shot"));
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-AuthorTrace-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);File.WriteAllText(Path.Combine(_directory,"f6.json"),text);
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);
            _lab=Object.FindFirstObjectByType<SkillLabRunner>();_lab.AutoAdvance=false;_lab.ConfigureContentDirectory(_directory);Assert.IsTrue(_lab.ReloadPublished(),_lab.LastLoadError);
            using(var reference=new PublishedSkillSession())
            {
                reference.Replace(new Dictionary<string,string>{{"f6",text}});reference.Simulation.Cast("burn-shot",0,0);Assert.IsTrue(_lab.CastSlot("f6",0,0).Accepted);
                foreach(int step in new[]{20,40,80})
                {
                    reference.Simulation.Advance(step);_lab.StepLogicForTests(step);
                    Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(reference.Simulation.Snapshot()),JObject.FromObject(_lab.Snapshot)));
                }
                var state=_lab.Snapshot;var damage=state.Events.Where(e=>e.Kind=="damage").ToArray();Assert.Greater(damage.Length,0);
                Assert.IsTrue(damage.All(e=>e.Node.GraphKind=="buff"&&e.CastId==1&&e.SourceId!=null&&e.TargetId!=null));
                Assert.IsTrue(state.Events.Any(e=>e.Kind=="cleanup"&&e.Node.NodeKind=="projectile"&&e.BodyId!=null));
                Assert.IsTrue(_lab.ReloadPublished());Assert.AreEqual(0,_lab.Snapshot.Events.Count(e=>e.Kind=="execute"));
            }
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
