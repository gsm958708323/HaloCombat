using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillChargeTests
    {
        static SkillProjectDocument Example()
        {
            var project = SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
            project.Preview.Recovery = 0;
            return project;
        }
        static SkillNodeDocument CastNode(SkillProjectDocument project) => project.Skills.First(s => s.Id == "charged-shot").Nodes.First(n => n.Kind == "cast");
        static SkillNodeDocument Node(SkillProjectDocument project, string kind) => project.Skills.First(s => s.Id == "charged-shot").Nodes.First(n => n.Kind == kind);
        static SkillSimulation Simulation(SkillProjectDocument project) => new SkillSimulation(project, CombatContentCompiler.Compile(project));

        [Test]
        public void EarlyReleaseCancelsWithoutDamageOrRefund()
        {
            using (var simulation = Simulation(Example()))
            {
                Assert.IsTrue(simulation.Cast("charged-shot", 0, 0).Accepted);
                simulation.Advance(5); simulation.Release(); simulation.Advance(100);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(90, simulation.Snapshot().Energy);
                Assert.IsTrue(simulation.Snapshot().Events.Any(e => e.Result == "EarlyRelease"));
            }
        }

        [TestCase(10, 80)]
        [TestCase(30, 160)]
        [TestCase(50, 240)]
        public void ChargeTimeProducesRealScaledDamageAtMinimumMiddleAndMaximum(int steps, int damage)
        {
            var project = Example(); CastNode(project).Parameters["autoRelease"] = false;
            // Inactive burst fields retained by a mode switch cannot cause extra releases/cost.
            CastNode(project).Parameters["count"] = 20; CastNode(project).Parameters["costPerPulse"] = true;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("charged-shot", 0, 0); simulation.Advance(steps);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Projectiles);
                Assert.IsTrue(simulation.Snapshot().Charging);
                simulation.Release(); simulation.Advance(80);
                Assert.AreEqual(1, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(damage, simulation.Snapshot().Statistics.Damage, .005f);
                Assert.AreEqual(1000 - damage, simulation.Snapshot().Actors.First(a => a.Name == "target-1").Hp.Value, .005f);
                Assert.AreEqual(90, simulation.Snapshot().Energy);
            }
        }

        [Test]
        public void AutoReleaseFiresOnceAndManualFullChargeKeepsWaiting()
        {
            var project = Example();
            using (var auto = Simulation(project))
            {
                auto.Cast("charged-shot", 0, 0); auto.Advance(49);
                Assert.IsTrue(auto.Snapshot().Charging); Assert.AreEqual(0, auto.Snapshot().Statistics.Projectiles);
                auto.Advance(1); Assert.IsFalse(auto.Snapshot().Charging);
                auto.Advance(100); Assert.AreEqual(1, auto.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(240, auto.Snapshot().Statistics.Damage, .005f);
                Assert.AreEqual(1, auto.Snapshot().Events.Count(e => e.Kind == "released"));
                Assert.Less(auto.Snapshot().Events.First(e => e.Kind == "released").Sequence,
                    auto.Snapshot().Events.First(e => e.Kind == "spawn" && e.Result == "projectile").Sequence);
            }
            CastNode(project).Parameters["autoRelease"] = false;
            using (var manual = Simulation(project))
            {
                manual.Cast("charged-shot", 0, 0); manual.Advance(100);
                Assert.IsTrue(manual.Snapshot().Charging); Assert.AreEqual(1, manual.Snapshot().ChargeProgress);
                Assert.AreEqual(0, manual.Snapshot().Statistics.Projectiles);
                manual.Release(); manual.Advance(80);
                Assert.AreEqual(240, manual.Snapshot().Statistics.Damage, .005f);
            }
        }

        [Test]
        public void ReleaseLocksTheLatestAimBeforeTheReleaseDelay()
        {
            var project = Example(); CastNode(project).Parameters["delay"] = .2f;
            project.Preview.Targets = project.Preview.Targets.Take(2).ToArray();
            project.Preview.Targets[1].X = -6; project.Preview.Targets[1].Z = 6;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("charged-shot", 0, 0); simulation.Advance(10);
                simulation.Aim(-6, 6); simulation.Release(); simulation.Aim(0, 0);
                simulation.Advance(5); Assert.AreEqual(0, simulation.Snapshot().Statistics.Projectiles);
                simulation.Advance(80);
                Assert.AreEqual(1000, simulation.Snapshot().Actors.First(a => a.Name == "target-1").Hp);
                Assert.AreEqual(920, simulation.Snapshot().Actors.First(a => a.Name == "target-2").Hp.Value, .005f);
            }
        }

        [TestCase("hit")]
        [TestCase("death")]
        [TestCase("cancel")]
        public void InterruptionsAfterMinimumTimeCannotReleasePendingDamage(string reason)
        {
            using (var simulation = Simulation(Example()))
            {
                simulation.Cast("charged-shot", 0, 0); simulation.Advance(20);
                if (reason == "cancel") simulation.CancelInput(); else simulation.Interrupt(reason == "death");
                simulation.Release(); simulation.Advance(100);
                Assert.IsFalse(simulation.Snapshot().Charging);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Damage);
            }
        }

        [Test]
        public void ChargeEnergyAndClockFreezeDuringWorldHitstop()
        {
            var project = Example(); CastNode(project).Parameters["energyPerSecond"] = 10;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("charged-shot", 0, 0); simulation.RequestHitstop(10); simulation.Advance(20);
                Assert.AreEqual(88, simulation.Snapshot().Energy, .002f);
                Assert.AreEqual(0, simulation.Snapshot().ChargeProgress, .00001f);
                simulation.Advance(20);
                Assert.AreEqual(.5f, simulation.Snapshot().ChargeProgress, .0001f);
                simulation.Release(); simulation.Advance(80);
                Assert.AreEqual(160, simulation.Snapshot().Statistics.Damage, .005f);
                Assert.AreEqual(84, simulation.Snapshot().Energy, .003f, "Only the hold phase spends continuous energy.");
            }
        }

        [Test]
        public void InsufficientHoldEnergyCancelsWithoutAutomaticRelease()
        {
            var project = Example(); project.Preview.InitialEnergy = 11;
            CastNode(project).Parameters["energyPerSecond"] = 10;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("charged-shot", 0, 0); simulation.Advance(100);
                Assert.AreEqual(0, simulation.Snapshot().Energy, .001f);
                Assert.IsFalse(simulation.Snapshot().Charging);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Projectiles);
            }
        }

        [Test]
        public void OldProjectilesKeepTheirOwnPowerAfterTheNextCastChangesPower()
        {
            var project = Example(); Node(project, "projectile").Parameters["speed"] = 1;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("charged-shot", 0, 0); simulation.Advance(30); simulation.Release(); simulation.Advance(20);
                Assert.IsTrue(simulation.Cast("charged-shot", 0, 0).Accepted);
                simulation.Advance(10); simulation.Release(); simulation.Advance(500);
                Assert.AreEqual(2, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(240, simulation.Snapshot().Statistics.Damage, .005f);
            }
        }

        [Test]
        public void ChargePowerScalesBaseDamageBeforeDefense()
        {
            var project = Example(); project.Preview.Targets[0].Def = 20;
            using (var simulation = Simulation(project))
            {
                simulation.Cast("charged-shot", 0, 0); simulation.Advance(30); simulation.Release(); simulation.Advance(80);
                Assert.AreEqual(140, simulation.Snapshot().Statistics.Damage, .005f, "80 * 2 - 20 defense.");
                Assert.AreEqual(860, simulation.Snapshot().Actors.First(a => a.Name == "target-1").Hp.Value, .005f);
            }
        }

        [Test]
        public void SharedDefinitionsAndSeparateInstancesDoNotShareChargeState()
        {
            var project = Example(); var content = CombatContentCompiler.Compile(project);
            using (var first = new SkillSimulation(project, content))
            using (var second = new SkillSimulation(project, content))
            {
                first.Cast("charged-shot", 0, 0); second.Cast("charged-shot", 0, 0);
                first.Advance(30); second.Advance(10);
                first.Release(); second.Release(); first.Advance(80); second.Advance(80);
                Assert.AreEqual(160, first.Snapshot().Statistics.Damage, .005f);
                Assert.AreEqual(80, second.Snapshot().Statistics.Damage, .005f);
                Assert.AreEqual(80, Node(project, "damage").Parameters.Value<int>("flat"));
            }
        }

        [Test]
        public void InitialEnergyRecoveryAndInvalidConfigurationUseExplicitBoundaries()
        {
            var project = Example(); project.Preview.InitialEnergy = 25; project.Preview.Recovery = 10;
            using (var simulation = Simulation(project))
            {
                Assert.AreEqual(25, simulation.Snapshot().Energy);
                simulation.Advance(50); Assert.AreEqual(35, simulation.Snapshot().Energy, .002f);
            }
            project.Preview.InitialEnergy = 101;
            StringAssert.Contains("initialEnergy", Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path);
            project.Preview.InitialEnergy = 100; CastNode(project).Parameters["chargeMin"] = 2;
            StringAssert.Contains("chargeMin", Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path);
            CastNode(project).Parameters["chargeMin"] = .2f; CastNode(project).Parameters["powerMax"] = .5f;
            StringAssert.Contains("powerMax", Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(project)).Path);
        }
    }
}
