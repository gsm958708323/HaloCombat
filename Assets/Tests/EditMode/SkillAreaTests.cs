using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillAreaTests
    {
        static SkillProjectDocument Example() => SkillProjectFiles.Read(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));
        static SkillGraphDocument Skill(SkillProjectDocument p, string id) => p.Skills.First(s => s.Id == id);
        static SkillNodeDocument Node(SkillProjectDocument p, string id, string kind) => Skill(p, id).Nodes.First(n => n.Kind == kind);
        static SkillSimulation Simulation(SkillProjectDocument p) => new SkillSimulation(p, CombatContentCompiler.Compile(p));
        static void Cue(SkillGraphDocument skill, string port, int id)
        {
            string key = "cue-" + port;
            skill.Nodes = skill.Nodes.Concat(new[] { new SkillNodeDocument { Id = key, Kind = "cue", Parameters = JObject.FromObject(new { id }) } }).ToArray();
            skill.Edges = skill.Edges.Concat(new[] { new SkillEdgeDocument { Id = key, Source = "area", SourcePort = port, Target = key, TargetPort = "execute" } }).ToArray();
        }

        [TestCase(4, 0, 240)]
        [TestCase(1, 0, 80)]
        [TestCase(4, 2, 160)]
        public void ExplosionUsesTheRealHitPointRadiusAndTargetLimit(float radius, int maximum, int damage)
        {
            var p = Example(); var d = Node(p, "explosive-shot", "damage").Parameters;
            d["radius"] = radius; d["maxTargets"] = maximum;
            using (var simulation = Simulation(p))
            {
                simulation.Cast("explosive-shot", 0, 0); simulation.Advance(80);
                Assert.AreEqual(damage, simulation.Snapshot().Statistics.Damage);
                Assert.AreEqual(damage / 80, simulation.Snapshot().Statistics.Hits);
                Assert.AreEqual(1000, simulation.Snapshot().Actors.First(a => a.Name == "target-4").Hp);
                Assert.AreEqual(1, simulation.Snapshot().Events.Count(e => e.Kind == "area"));
            }
        }

        [TestCase(false, 60, 0)]
        [TestCase(true, 300, 1)]
        public void AreaDeliveryUsesInstantOrIndependentPersistentLifetime(bool persistent, int damage, int areas)
        {
            var p = Example(); Node(p, "ground-field", "delivery").Parameters["persistent"] = persistent;
            using (var simulation = Simulation(p))
            {
                simulation.Cast("ground-field", 0, 0); simulation.Advance(80);
                Assert.AreEqual(damage, simulation.Snapshot().Statistics.Damage);
                Assert.AreEqual(areas, simulation.Snapshot().Statistics.Areas);
                Assert.IsFalse(simulation.Snapshot().Actors.Any(a => a.Kind == "aoe"));
                Assert.AreEqual(1000 - damage / 3, simulation.Snapshot().Actors.First(a => a.Name == "target-1").Hp);
                Assert.AreEqual(persistent ? 5 : 0, simulation.Snapshot().Events.Count(e => e.Kind == "area-pulse"));
            }
        }

        [Test]
        public void PulseOnSpawnOffAndExclusiveEndPreventAnExtraLastPulse()
        {
            var p = Example(); Node(p, "ground-field", "delivery").Parameters["pulseOnSpawn"] = false;
            using (var simulation = Simulation(p))
            {
                simulation.Cast("ground-field", 0, 0); simulation.Advance(80);
                Assert.AreEqual(240, simulation.Snapshot().Statistics.Damage);
                Assert.AreEqual(4, simulation.Snapshot().Events.Count(e => e.Kind == "area-pulse"));
                Assert.AreEqual(1, simulation.Snapshot().Events.Count(e => e.Kind == "area-expired"));
            }
        }

        [Test]
        public void SmallPulseIntervalsCatchUpWithinTheFiniteLifetime()
        {
            var p = Example(); p.Preview.Targets = p.Preview.Targets.Take(1).ToArray();
            var parameters = Node(p, "ground-field", "delivery").Parameters;
            parameters["duration"] = .1f; parameters["interval"] = .001f;
            Node(p, "ground-field", "damage").Parameters["flat"] = 1;
            using (var simulation = Simulation(p))
            {
                simulation.Cast("ground-field", 0, 0); simulation.Advance(50);
                Assert.AreEqual(100, simulation.Snapshot().Statistics.Damage);
                Assert.AreEqual(100, simulation.Snapshot().Events.Count(e => e.Kind == "area-pulse"));
                Assert.IsFalse(simulation.Snapshot().Actors.Any(a => a.Kind == "aoe"));
            }
        }

        [Test]
        public void ChargePowerSurvivesEveryPersistentAreaPulse()
        {
            var p = Example(); var cast = Node(p, "ground-field", "cast").Parameters;
            cast["mode"] = "charge"; cast["chargeMin"] = .2f; cast["chargeMax"] = 1;
            cast["powerMin"] = 1; cast["powerMax"] = 3; cast["autoRelease"] = false; cast["energyPerSecond"] = 0;
            using (var simulation = Simulation(p))
            {
                simulation.Cast("ground-field", 0, 0); simulation.Advance(30); simulation.Release(); simulation.Advance(80);
                Assert.AreEqual(600, simulation.Snapshot().Statistics.Damage, .01f);
                Assert.AreEqual(15, simulation.Snapshot().Statistics.Hits);
            }
        }

        [Test]
        public void OccupancyEnterLeaveAndNaturalExpiryHaveIndependentOrderedEvents()
        {
            var p = Example(); p.Preview.Targets = p.Preview.Targets.Take(1).ToArray();
            var skill = Skill(p, "ground-field"); Cue(skill, "enter", 11); Cue(skill, "exit", 12); Cue(skill, "stay", 13); Cue(skill, "expire", 14);
            using (var simulation = Simulation(p))
            {
                simulation.Cast("ground-field", 0, 0); simulation.Advance(10);
                Assert.AreEqual(1, simulation.Snapshot().Events.Count(e => e.Kind == "area-enter"));
                simulation.Place("target-1", 20, 0); simulation.Advance(2);
                Assert.AreEqual(1, simulation.Snapshot().Events.Count(e => e.Kind == "area-exit"));
                simulation.Place("target-1", 0, 0); simulation.Advance(2);
                Assert.AreEqual(2, simulation.Snapshot().Events.Count(e => e.Kind == "area-enter"));
                simulation.Advance(80);
                var events = simulation.Snapshot().Events;
                Assert.AreEqual(2, events.Count(e => e.Kind == "area-exit"));
                Assert.AreEqual(1, events.Count(e => e.Kind == "area-expired"));
                Assert.AreEqual(1, events.Count(e => e.Kind == "cue" && e.Result == "14"));
                Assert.Less(events.Last(e => e.Kind == "area-exit").Sequence, events.First(e => e.Kind == "area-expired").Sequence);
                Assert.AreEqual(events.First(e => e.Kind == "area-enter").Source, events.First(e => e.Kind == "cue" && e.Result == "11").Source);
            }
        }

        [Test]
        public void OwnerDeathClearsOccupancyWithoutPretendingToExpire()
        {
            var p = Example(); var skill = Skill(p, "ground-field"); Cue(skill, "enter", 11); Cue(skill, "exit", 12); Cue(skill, "expire", 14);
            using (var simulation = Simulation(p))
            {
                simulation.Cast("ground-field", 0, 0); simulation.Advance(10);
                int hits = simulation.Snapshot().Statistics.Hits;
                simulation.Interrupt(true); simulation.Advance(100);
                Assert.AreEqual(hits, simulation.Snapshot().Statistics.Hits);
                Assert.AreEqual(3, simulation.Snapshot().Events.Count(e => e.Kind == "area-exit"));
                Assert.AreEqual(1, simulation.Snapshot().Events.Count(e => e.Kind == "area-removed"));
                Assert.IsFalse(simulation.Snapshot().Events.Any(e => e.Kind == "area-expired" || (e.Kind == "cue" && e.Result == "14")));
            }
        }

        [TestCase("target", "target-1")]
        [TestCase("self", "caster")]
        public void DirectDeliveryUsesTheExplicitRecipient(string recipient, string victim)
        {
            var p = Example(); Node(p, "direct-hit", "delivery").Parameters["recipient"] = recipient;
            using (var simulation = Simulation(p))
            {
                simulation.Cast("direct-hit", 0, 0); simulation.Advance(25);
                Assert.AreEqual(920, simulation.Snapshot().Actors.First(a => a.Name == victim).Hp);
                Assert.AreEqual(80, simulation.Snapshot().Statistics.Damage);
            }
        }

        [Test]
        public void MissingExplicitTargetCenterDoesNotFallBackToThePoint()
        {
            var p = Example(); Node(p, "ground-field", "delivery").Parameters["center"] = "target";
            using (var simulation = Simulation(p))
            {
                simulation.Cast("ground-field", 0, 0); simulation.Advance(80);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Areas);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Damage);
            }
        }

        [Test]
        public void RepeatedDefinitionReferencesExecuteTwiceAndDisabledBranchesDoNotRun()
        {
            var p = Example(); var skill = Skill(p, "explosive-shot");
            var duplicate = new SkillEdgeDocument { Id = "again", Source = "cast", SourcePort = "cast", Target = "projectile", TargetPort = "execute", Order = 1 };
            skill.Edges = skill.Edges.Concat(new[] { duplicate }).ToArray();
            using (var simulation = Simulation(p))
            {
                simulation.Cast("explosive-shot", 0, 0); simulation.Advance(80);
                Assert.AreEqual(2, simulation.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(480, simulation.Snapshot().Statistics.Damage);
            }
            skill.Nodes.First(n => n.Kind == "projectile").Enabled = false;
            // Keep one valid effect so disabling the projectile is not an empty-cast error.
            skill.Nodes = skill.Nodes.Concat(new[] { new SkillNodeDocument { Id = "cue", Kind = "cue", Parameters = JObject.FromObject(new { id = 2 }) } }).ToArray();
            skill.Edges = skill.Edges.Concat(new[] { new SkillEdgeDocument { Id = "cast-cue", Source = "cast", SourcePort = "cast", Target = "cue", TargetPort = "execute", Order = 2 } }).ToArray();
            using (var simulation = Simulation(p))
            {
                simulation.Cast("explosive-shot", 0, 0); simulation.Advance(80);
                Assert.AreEqual(0, simulation.Snapshot().Statistics.Projectiles); Assert.AreEqual(0, simulation.Snapshot().Statistics.Damage);
            }
        }

        [Test]
        public void InvalidCentersRadiiAndInactiveLifecyclePortsReportNodePaths()
        {
            var p = Example(); var node = Node(p, "ground-field", "delivery"); node.Parameters["radius"] = -1;
            StringAssert.Contains("radius", Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(p)).Path);
            node.Parameters["radius"] = 3; node.Parameters["center"] = "guess";
            StringAssert.Contains("center", Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(p)).Path);
            node.Parameters["center"] = "point"; node.Parameters["persistent"] = false; Cue(Skill(p, "ground-field"), "exit", 2);
            StringAssert.Contains("cue-exit", Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(p)).Path);
        }
    }
}
