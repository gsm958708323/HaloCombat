using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    public sealed class EditorOptimizationPlayModeTests
    {
        string _directory;
        SkillLabRunner _runner;
        [UnityTest]
        public IEnumerator PublishedSweepShowsProgressAndRealDamageNumbersThenDashStopsAtTheEnemyBody()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-EditorOptimizations-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            p.Preview.Caster.X=-1.5f;p.Preview.Caster.Z=0;p.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();
            p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=0,Hp=1000,CollisionRadius=.4f}};
            foreach(var pair in new[]{new[]{"f6","melee-fan"},new[]{"f7","dash-strike"}})
                File.WriteAllText(Path.Combine(_directory,pair[0]+".json"),SkillPublicationJson.Write(SkillPublicationJson.Create(p,pair[1])));
            yield return SkillLabTestScene.Load();
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;
            _runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
            Assert.IsTrue(_runner.CastSlot("f6",5,0).Accepted);
            for(int i=0;i<30&&_runner.Snapshot.Volumes.Length==0;i++)_runner.StepLogicForTests(1);
            var first=_runner.Snapshot.Volumes.Single();_runner.StepLogicForTests(2);var second=_runner.Snapshot.Volumes.Single();Assert.Greater(second.Progress,first.Progress);
            yield return null;
            var sweep=_runner.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="SweepEdge");Assert.Greater(sweep.GetPosition(1).magnitude,1);
            Assert.Greater(_runner.GetComponent<SkillLabVisuals>().ActiveDamageNumbers,0);Assert.AreEqual(1,_runner.Snapshot.Statistics.Hits);
            yield return new WaitForSecondsRealtime(1.2f);Assert.AreEqual(0,_runner.GetComponent<SkillLabVisuals>().ActiveDamageNumbers);
            _runner.StepLogicForTests(80);yield return null;Assert.IsFalse(_runner.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="SweepEdge"));
            Assert.IsTrue(_runner.ReloadPublished());Assert.IsTrue(_runner.CastSlot("f7",5,0).Accepted);_runner.StepLogicForTests(80);yield return null;
            var caster=_runner.Snapshot.Actors.Single(a=>a.Kind=="caster");var target=_runner.Snapshot.Actors.Single(a=>a.Name=="target");
            Assert.GreaterOrEqual(target.X-caster.X,caster.Radius+target.Radius-.0002f);Assert.AreEqual(1,_runner.Snapshot.Statistics.Hits);
            LogAssert.NoUnexpectedReceived();
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;
            if(_directory!=null&&Directory.Exists(_directory))Directory.Delete(_directory,true);
        }
    }
}
