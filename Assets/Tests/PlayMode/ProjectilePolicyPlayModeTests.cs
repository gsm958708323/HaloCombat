using System;
using System.Collections;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    public sealed class ProjectilePolicyPlayModeTests
    {
        string _directory;
        SkillLabRunner _runner;
        SkillProjectDocument _project;
        void Publish(string first,string second)
        {
            File.WriteAllText(Path.Combine(_directory,"f6.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,first)));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(_project,second)));
        }
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory=Path.Combine(Path.GetTempPath(),"HaloCombat-ProjectilePolicy-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_directory);
            _project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            _project.Preview.Caster.X=0;_project.Preview.Caster.Z=0;_project.Preview.Recovery=0;_project.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();
            _project.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=2,Z=0,Hp=1000,Def=0}};
            foreach(string id in new[]{"behavior-pierce","behavior-chain","flight-time","flight-distance"})
                _project.Skills.Single(s=>s.Id==id).Nodes.Single(n=>n.Kind=="cast").Parameters["delay"]=0;
            Publish("flight-time","flight-distance");yield return SceneManager.LoadSceneAsync("Assets/Scenes/HaloCombat/SkillLab.unity",LoadSceneMode.Single);
            _runner=UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();Assert.IsNotNull(_runner);_runner.AutoAdvance=false;_runner.ConfigureContentDirectory(_directory);Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {if(_runner!=null)UnityEngine.Object.Destroy(_runner.gameObject);yield return null;if(Directory.Exists(_directory))Directory.Delete(_directory,true);}

        [UnityTest]
        public IEnumerator PublishedTimeAndDistanceFlightExecuteAtTheirActualPointsAndRetainAuthorIdentity()
        {
            foreach(string slot in new[]{"f6","f7"})
            {
                Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);Assert.IsTrue(_runner.CastSlot(slot,10,0).Accepted);_runner.StepLogicForTests(50);
                var state=_runner.Snapshot;var flights=state.Events.Where(e=>e.Kind=="projectile-flight").ToArray();
                Assert.AreEqual(slot=="f6"?5:2,flights.Length);Assert.IsTrue(flights.All(e=>e.Flight.Index>0&&e.CastId>0&&e.Node.NodeId=="projectile"));
                Assert.Greater(state.Statistics.Damage,0);Assert.IsTrue(state.Events.Where(e=>e.Kind=="damage").All(e=>e.Node.NodeId=="damage"));
                Assert.AreEqual(5,state.Actors.Single(a=>a.Kind=="projectile").X,.0001);yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PublishedDataBehaviorPiercesAndChainsThroughTheSameRuntime()
        {
            _project.Preview.Targets=new[]{2,4,6}.Select((x,i)=>new PreviewActorDocument{Id="target-"+i,X=x,Z=0,Hp=1000,Def=0}).ToArray();
            Publish("behavior-pierce","behavior-chain");
            foreach(string slot in new[]{"f6","f7"})
            {
                Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);_runner.CastSlot(slot,10,0);_runner.StepLogicForTests(100);
                Assert.AreEqual(3,_runner.Snapshot.Statistics.Hits);Assert.AreEqual(slot=="f6"?0:2,_runner.Snapshot.Statistics.Chains);yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }
    }
}
