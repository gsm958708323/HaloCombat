using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillChannelTests
    {
        static SkillProjectDocument Example()
        {
            var project = SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json")));
            project.Preview.Recovery = 0;
            project.Skills[0].Nodes[1].Parameters["mode"] = "channel";
            project.Skills[0].Nodes[1].Parameters["delay"] = 0;
            project.Skills[0].Nodes[1].Parameters["energyPerSecond"] = 20;
            // Retained fields from switching an instant burst to channel must be inactive.
            project.Skills[0].Nodes[1].Parameters["costPerPulse"] = true;
            return project;
        }
        static SkillSimulation Simulation(SkillProjectDocument project) => new SkillSimulation(project, CombatContentCompiler.Compile(project));

        [Test]
        public void HeldCastRejectsRepeatedPressAndReleaseStopsFuturePulses()
        {
            using (var simulation = Simulation(Example()))
            {
                Assert.IsTrue(simulation.Cast("basic-shot", 0, 0).Accepted);
                Assert.IsFalse(simulation.Cast("basic-shot", 0, 0).Accepted);
                simulation.Advance(10);
                Assert.AreEqual(3, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(86, simulation.Snapshot().Energy, .001f);
                simulation.Release();
                Assert.IsFalse(simulation.Snapshot().Channeling);
                simulation.Advance(60);
                Assert.AreEqual(3, simulation.Snapshot().Statistics.Projectiles);
                Assert.Greater(simulation.Snapshot().Statistics.Damage, 0, "Released casts keep already-spawned projectile lifetimes.");
                Assert.IsTrue(simulation.Snapshot().Events.Any(e => e.Result == "Released"));
            }
        }

        [Test]
        public void FiniteChannelEndsAtExclusiveDurationAndSmallIntervalsCatchUp()
        {
            var project = Example();
            var parameters = project.Skills[0].Nodes[1].Parameters;
            parameters["sustained"] = false; parameters["channelDuration"] = .1f;
            parameters["interval"] = .001f; parameters["cost"] = 0;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("basic-shot", 0, 0); simulation.Advance(5);
                Assert.AreEqual(100, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(98, simulation.Snapshot().Energy, .001f);
                Assert.IsFalse(simulation.Snapshot().Channeling);
                simulation.Advance(20);
                Assert.AreEqual(100, simulation.Snapshot().Statistics.Projectiles);
            }
        }

        [Test]
        public void PerPulseCostRejectsOnlyUnpaidBurstSegments()
        {
            var project = Example(); project.Preview.Energy = 25;
            var parameters = project.Skills[0].Nodes[1].Parameters;
            parameters["mode"] = "instant"; parameters["count"] = 5; parameters["costPerPulse"] = true;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("basic-shot", 0, 0); simulation.Advance(50);
                Assert.AreEqual(2, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(5, simulation.Snapshot().Energy, .001f);
                Assert.IsTrue(simulation.Snapshot().Events.Any(e => e.Result == "EnergyDepleted"));
            }
        }

        [Test]
        public void EnergyBudgetEndsChannelWithoutNegativeResourcesOrAutomaticRestart()
        {
            var project = Example(); project.Preview.Energy = 18;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("basic-shot", 0, 0); simulation.Advance(50);
                Assert.IsFalse(simulation.Snapshot().Channeling);
                Assert.AreEqual(0, simulation.Snapshot().Energy, .001f);
                int count = simulation.Snapshot().Statistics.Projectiles;
                simulation.Advance(100);
                Assert.AreEqual(count, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(1, simulation.Snapshot().Statistics.Casts);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HitAndDeathStopFutureEmissionsAndMovement(bool death)
        {
            using (var simulation = Simulation(Example()))
            {
                simulation.Cast("basic-shot", 0, 0); simulation.Move(0, 1); simulation.Advance(5);
                Assert.Greater(simulation.Snapshot().Actors.First(a => a.Kind == "caster").Z, 0);
                int count = simulation.Snapshot().Statistics.Projectiles;
                simulation.Interrupt(death); simulation.Advance(100);
                Assert.AreEqual(count, simulation.Snapshot().Statistics.Projectiles);
                Assert.IsFalse(simulation.Snapshot().Channeling);
            }
        }

        [Test]
        public void ChunkedSteppingAndIndependentCastersKeepTheSameSchedule()
        {
            var project = Example();
            using (var first = Simulation(project))
            using (var second = Simulation(project))
            {
                first.Cast("basic-shot", 0, 0); second.Cast("basic-shot", 0, 0);
                first.Advance(20); for (int i = 0; i < 20; i++) second.Advance(1);
                Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(first.Snapshot()), JObject.FromObject(second.Snapshot())));
                first.Release(); second.Advance(5);
                Assert.IsFalse(first.Snapshot().Channeling); Assert.IsTrue(second.Snapshot().Channeling);
                Assert.Greater(second.Snapshot().Statistics.Projectiles, first.Snapshot().Statistics.Projectiles);
            }
        }
    }
}
