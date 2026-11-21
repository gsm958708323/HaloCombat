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
    public sealed class SkillLabPlayModeTests
    {
        string _directory;
        SkillLabRunner _runner;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "HaloCombat-SkillLab-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            foreach (var pair in new[] { new[] { "f6", "basic-shot" }, new[] { "f7", "burst-shot" } })
                File.WriteAllText(Path.Combine(_directory, pair[0] + ".json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, pair[1])));
            yield return SkillLabTestScene.Load();
            _runner = UnityEngine.Object.FindFirstObjectByType<SkillLabRunner>();
            Assert.IsNotNull(_runner);
            _runner.AutoAdvance = false;
            _runner.ConfigureContentDirectory(_directory);
            Assert.IsTrue(_runner.ReloadPublished(), _runner.LastLoadError);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_runner != null) UnityEngine.Object.Destroy(_runner.gameObject);
            yield return null;
            if (_directory != null && Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        [UnityTest]
        public IEnumerator PublishedBleedUsesF6FormationAndDisplaysActualPeriodicDamage()
        {
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            project.Preview.Caster.X = -5;
            project.Preview.Caster.Z = -1;
            project.Preview.Targets = new[] { new PreviewActorDocument { Id = "dummy", X = 1, Z = -1, Hp = 1000 } };
            project.Preview.Obstacles = new[] { new PreviewObstacleDocument { Id = "cover", X = -1, Z = 3, Width = 2, Depth = 3 } };
            File.WriteAllText(Path.Combine(_directory, "f6.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "bleed-validation")));
            Assert.IsTrue(_runner.ReloadPublished(), _runner.LastLoadError);
            Assert.IsTrue(_runner.HasDifferentSlotLayouts);
            Assert.AreEqual("f6", _runner.CurrentSlot);
            Assert.AreEqual(1, _runner.Snapshot.Obstacles.Length);
            Assert.IsTrue(_runner.SelectSlot("f7"));
            Assert.AreEqual(0, _runner.Snapshot.Statistics.Casts);
            Assert.IsTrue(_runner.SelectSlot("f6"));
            yield return null;

            var camera = Camera.main;
            Assert.IsTrue(camera.orthographic);
            Assert.That(camera.transform.eulerAngles.x, Is.InRange(55f, 61f));
            var cube = _runner.transform.Find("Obstacle_cover");
            Assert.AreEqual(-1, cube.position.x);
            Assert.AreEqual(3, cube.position.z);
            Assert.AreEqual(2, cube.localScale.x);
            Assert.AreEqual(3, cube.localScale.z);
            var caster = _runner.Snapshot.Actors.Single(actor => actor.Kind == "caster");
            Assert.AreEqual(-5, caster.X);
            Assert.AreEqual(-1, caster.Z);

            Assert.IsTrue(_runner.CastSlot("f6", 1, -1).Accepted);
            _runner.StepLogicForTests(140);
            var hits = _runner.Snapshot.Events.Where(e => e.Damage?.HpLost > 0).ToArray();
            CollectionAssert.AreEqual(new[] { 30f, 12f, 12f, 12f, 12f }, hits.Select(e => e.Damage.HpLost));
            Assert.IsNull(hits[0].PeriodicBuff);
            Assert.That(hits.Skip(1).Select(e => e.PeriodicBuff), Is.All.EqualTo("流血"));
            Assert.AreEqual(922, _runner.Snapshot.Actors.Single(actor => actor.Name == "dummy").Hp);
            yield return null;
            Assert.AreEqual(5, _runner.GetComponent<SkillLabVisuals>().ActiveDamageNumbers);
        }

        [UnityTest]
        public IEnumerator PublishedFilesRunThroughRealCoreAndSnapshotVisuals()
        {
            Assert.IsTrue(_runner.CastSlot("f6", 0, 0).Accepted);
            Assert.IsFalse(_runner.CastSlot("f6", 0, 0).Accepted);
            Assert.IsFalse(_runner.CastSlot("f7", 0, 0).Accepted, "Slot switching must not bypass the active cast guard.");
            Assert.AreEqual(90, _runner.Snapshot.Energy);
            _runner.StepLogicForTests(60);
            Assert.AreEqual(80, _runner.Snapshot.Statistics.Damage);
            Assert.AreEqual(920, _runner.Snapshot.Actors.First(actor => actor.Name == "target-1").Hp);
            Assert.IsTrue(_runner.CastSlot("f7", 0, 0).Accepted);
            _runner.StepLogicForTests(60);
            Assert.AreEqual("burst-shot", _runner.SkillId);
            Assert.AreEqual(4, _runner.Snapshot.Statistics.Projectiles);
            Assert.AreEqual(4, _runner.Snapshot.Statistics.Hits);
            Assert.AreEqual(384, _runner.Snapshot.Statistics.Damage, .001f);
            yield return null;
            Assert.IsNotNull(GameObject.Find("target_target-1"));
            Assert.IsNotNull(Camera.main);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CorruptDiskReloadKeepsTheWorkingVersionAndPendingProjectile()
        {
            _runner.CastSlot("f6", 0, 0); _runner.StepLogicForTests(15);
            string revision = _runner.Revision; int generation = _runner.Generation;
            Assert.IsTrue(_runner.Snapshot.Actors.Any(actor => actor.Kind == "projectile"));
            File.WriteAllText(Path.Combine(_directory, "f7.json"), "{broken");
            Assert.IsFalse(_runner.ReloadPublished());
            StringAssert.Contains("f7", _runner.LastLoadError);
            Assert.AreEqual(revision, _runner.Revision); Assert.AreEqual(generation, _runner.Generation);
            _runner.StepLogicForTests(45);
            Assert.AreEqual(80, _runner.Snapshot.Statistics.Damage);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SuccessfulReloadStartsANewWorldWithoutPendingOldDamage()
        {
            _runner.CastSlot("f6", 0, 0); _runner.StepLogicForTests(15);
            Assert.IsTrue(_runner.Snapshot.Actors.Any(actor => actor.Kind == "projectile"));
            Assert.IsTrue(_runner.ReloadPublished());
            Assert.AreEqual(0, _runner.Snapshot.Statistics.Casts);
            Assert.AreEqual(100, _runner.Snapshot.Energy);
            _runner.StepLogicForTests(100);
            Assert.AreEqual(0, _runner.Snapshot.Statistics.Damage);
            Assert.AreEqual(1000, _runner.Snapshot.Actors.First(actor => actor.Name == "target-1").Hp);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeldChannelUsesTheSameScheduleAndReleasesInUnity()
        {
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            project.Preview.Recovery = 0;
            File.WriteAllText(Path.Combine(_directory, "f6.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "basic-shot")));
            File.WriteAllText(Path.Combine(_directory, "f7.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "held-spray")));
            Assert.IsTrue(_runner.ReloadPublished(), _runner.LastLoadError);
            Assert.IsTrue(_runner.CastSlot("f7", 0, 0).Accepted);
            _runner.SetMovement(0, 1); _runner.StepLogicForTests(10);
            Assert.IsTrue(_runner.Snapshot.Channeling);
            Assert.AreEqual(3, _runner.Snapshot.Statistics.Projectiles);
            Assert.AreEqual(87.6f, _runner.Snapshot.Energy, .002f);
            Assert.Greater(_runner.Snapshot.Actors.First(actor => actor.Kind == "caster").Z, 0);
            _runner.ReleaseCast(); _runner.SetMovement(0, 0);
            _runner.StepLogicForTests(100);
            Assert.IsFalse(_runner.Snapshot.Channeling);
            Assert.AreEqual(3, _runner.Snapshot.Statistics.Projectiles);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChargedPublicationUsesInitialEnergyAndDealsScaledDamageAfterRelease()
        {
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            project.Preview.InitialEnergy = 50; project.Preview.Recovery = 0;
            var skill = project.Skills.First(s => s.Id == "charged-shot");
            skill.Nodes.First(n => n.Kind == "cast").Parameters["autoRelease"] = false;
            skill.Nodes.First(n => n.Kind == "cast").Parameters["energyPerSecond"] = 4;
            File.WriteAllText(Path.Combine(_directory, "f6.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "basic-shot")));
            File.WriteAllText(Path.Combine(_directory, "f7.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "charged-shot")));
            Assert.IsTrue(_runner.ReloadPublished(), _runner.LastLoadError);
            Assert.AreEqual(50, _runner.Snapshot.Energy);
            Assert.IsTrue(_runner.CastSlot("f7", 0, 0).Accepted);
            Assert.IsFalse(_runner.CastSlot("f7", 0, 0).Accepted);
            _runner.StepLogicForTests(30);
            Assert.IsTrue(_runner.Snapshot.Charging); Assert.AreEqual(.5f, _runner.Snapshot.ChargeProgress, .0001f);
            Assert.AreEqual(0, _runner.Snapshot.Statistics.Projectiles);
            _runner.ReleaseCast(); _runner.StepLogicForTests(80);
            Assert.AreEqual(160, _runner.Snapshot.Statistics.Damage, .005f);
            Assert.AreEqual(1, _runner.Snapshot.Statistics.Projectiles);
            Assert.AreEqual(37.6f, _runner.Snapshot.Energy, .003f);
            Assert.AreEqual(840, _runner.Snapshot.Actors.First(a => a.Name == "target-1").Hp.Value, .005f);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CancellingHeldChargeInTheUnityAdapterNeverFiresARelease()
        {
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            File.WriteAllText(Path.Combine(_directory, "f7.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "charged-shot")));
            Assert.IsTrue(_runner.ReloadPublished());
            _runner.CastSlot("f7", 0, 0); _runner.StepLogicForTests(30);
            _runner.CancelInput(); _runner.ReleaseCast(); _runner.StepLogicForTests(100);
            Assert.IsFalse(_runner.Snapshot.Charging);
            Assert.AreEqual(0, _runner.Snapshot.Statistics.Projectiles);
            Assert.AreEqual(0, _runner.Snapshot.Statistics.Damage);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PublishedExplosionHitsSeveralTargetsAndUsesItsRealRadius()
        {
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            File.WriteAllText(Path.Combine(_directory, "f7.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "explosive-shot")));
            Assert.IsTrue(_runner.ReloadPublished(), _runner.LastLoadError);
            _runner.CastSlot("f7", 0, 0); _runner.StepLogicForTests(30);
            Assert.AreEqual(160, _runner.Snapshot.Statistics.Damage);
            Assert.AreEqual(2, _runner.Snapshot.Statistics.Hits);
            Assert.AreEqual(1000, _runner.Snapshot.Actors.First(a => a.Name == "target-2").Hp);
            Assert.AreEqual(920, _runner.Snapshot.Actors.First(a => a.Name == "target-3").Hp);
            Assert.AreEqual(1000, _runner.Snapshot.Actors.First(a => a.Name == "target-4").Hp);
            Assert.Greater(_runner.Snapshot.Areas.Length, 0);
            yield return null;
            Assert.IsTrue(_runner.GetComponentsInChildren<LineRenderer>().Any(), "Unity must show the observed blast radius.");
            project.Skills.First(s => s.Id == "explosive-shot").Nodes.First(n => n.Kind == "delivery").Parameters["radius"] = 1;
            File.WriteAllText(Path.Combine(_directory, "f7.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "explosive-shot")));
            _runner.ReloadPublished(); _runner.CastSlot("f7", 0, 0); _runner.StepLogicForTests(80);
            Assert.AreEqual(80, _runner.Snapshot.Statistics.Damage);
        }

        [UnityTest]
        public IEnumerator PublishedAreaRendersAnIndependentLifetimeAndExpiresAfterFivePulses()
        {
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            File.WriteAllText(Path.Combine(_directory, "f7.json"), SkillPublicationJson.Write(SkillPublicationJson.Create(project, "ground-field")));
            Assert.IsTrue(_runner.ReloadPublished(), _runner.LastLoadError);
            _runner.CastSlot("f7", 0, 0); _runner.StepLogicForTests(10);
            var area = _runner.Snapshot.Actors.Single(a => a.Kind == "aoe");
            Assert.AreEqual(4, area.Radius); Assert.Greater(area.Remaining.Value, 0);
            yield return null;
            var visual = _runner.GetComponentsInChildren<Transform>().Single(t => t.name.StartsWith("aoe_"));
            Assert.AreEqual(8, visual.localScale.x);
            _runner.StepLogicForTests(70);
            Assert.AreEqual(300, _runner.Snapshot.Statistics.Damage);
            Assert.AreEqual(15, _runner.Snapshot.Statistics.Hits);
            Assert.AreEqual(5, _runner.Snapshot.Events.Count(e => e.Kind == "area-pulse"));
            Assert.IsFalse(_runner.Snapshot.Actors.Any(a => a.Kind == "aoe"));
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PublishedMeleeRendersTheActiveShapeAndInterruptClosesIt()
        {
            var project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(project,"melee-thrust")));
            Assert.IsTrue(_runner.ReloadPublished(),_runner.LastLoadError);
            _runner.CastSlot("f7",0,0);_runner.StepLogicForTests(12);
            Assert.AreEqual(80,_runner.Snapshot.Statistics.Damage);
            Assert.AreEqual(1,_runner.Snapshot.Volumes.Length);
            Assert.AreEqual("thrust",_runner.Snapshot.Volumes[0].Shape);
            yield return null;
            Assert.IsTrue(_runner.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="SkillVolume_thrust"));
            _runner.Interrupt();_runner.StepLogicForTests(60);
            Assert.AreEqual(0,_runner.Snapshot.Volumes.Length);Assert.AreEqual(80,_runner.Snapshot.Statistics.Damage);
            yield return null;
            Assert.IsFalse(_runner.GetComponentsInChildren<LineRenderer>().Any(l=>l.name=="SkillVolume_thrust"));
        }

        [UnityTest]
        public IEnumerator PublishedDashStopsOutsideTheContactedEnemyAndCanBeInterrupted()
        {
            var project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(project,"dash-strike")));
            _runner.ReloadPublished();_runner.CastSlot("f7",0,0);_runner.StepLogicForTests(80);
            Assert.AreEqual(-.8001f,_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X,.002f);
            Assert.AreEqual(80,_runner.Snapshot.Statistics.Damage);Assert.AreEqual(1,_runner.Snapshot.Statistics.Hits);
            _runner.ReloadPublished();_runner.CastSlot("f7",0,0);_runner.StepLogicForTests(20);
            float position=_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X;
            Assert.IsTrue(_runner.Snapshot.Volumes.Any(v=>v.Shape=="capsule"));
            _runner.Interrupt();_runner.StepLogicForTests(100);
            Assert.AreEqual(position,_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X,.0001f);
            Assert.AreEqual(0,_runner.Snapshot.Volumes.Length);
            yield return null;LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PublishedBlinkValidatesItsDestinationAndResolvesAtTheExactPoint()
        {
            var project=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            File.WriteAllText(Path.Combine(_directory,"f7.json"),SkillPublicationJson.Write(SkillPublicationJson.Create(project,"blink-strike")));
            _runner.ReloadPublished();Assert.IsFalse(_runner.CastSlot("f7",20,0).Accepted);Assert.AreEqual(100,_runner.Snapshot.Energy);
            Assert.IsTrue(_runner.CastSlot("f7",0,0).Accepted);_runner.StepLogicForTests(9);
            Assert.AreEqual(-6,_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X);
            _runner.StepLogicForTests(2);
            Assert.AreEqual(0,_runner.Snapshot.Actors.First(a=>a.Kind=="caster").X,.0001f);Assert.AreEqual(240,_runner.Snapshot.Statistics.Damage);
            yield return null;LogAssert.NoUnexpectedReceived();
        }
    }
}
