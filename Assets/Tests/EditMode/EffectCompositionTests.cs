using System;
using System.IO;
using System.Linq;
using Combat.Config;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class EffectCompositionTests
    {
        [TestCase(0, 920, 920)]
        [TestCase(80, 1000, 1000)]
        [TestCase(30, 950, 920)]
        public void RayContinuesOnlyWhenTheCurrentContactLostHp(int shield, int firstHp, int otherHp)
        {
            var project = Scene(shield);
            var skill = project.Skills.Single();
            var pierceEdge = skill.Edges.Single(edge => edge.Target == "ray-pierce");
            skill.Nodes = skill.Nodes.Append(new SkillNodeDocument { Id = "gate", Kind = "after-damage" }).ToArray();
            skill.Edges = skill.Edges.Where(edge => edge != pierceEdge).Concat(new[]
            {
                Edge("damage", "next", "gate"),
                Edge("gate", "apply", pierceEdge.Target)
            }).ToArray();

            var frame = Fire(project);
            var health = frame.Actors.Where(actor => actor.Kind == "target")
                .OrderBy(actor => actor.Name, StringComparer.Ordinal).Select(actor => actor.Hp).ToArray();
            TestContext.WriteLine($"Shield {shield}: HP [{string.Join(", ", health)}]");
            CollectionAssert.AreEqual(new float?[] { firstHp, otherHp, otherHp }, health);
        }

        static SkillProjectDocument Scene(float shield)
        {
            var path = Path.Combine(Application.dataPath, "../Content/SkillLab/project.json");
            var project = SkillProjectJson.SelectTemplate(SkillProjectFiles.Read(path), "piercing-ray");
            project.Preview.Caster.X = -5;
            project.Preview.Caster.Z = 0;
            project.Preview.Caster.DamageDealt = 1;
            project.Preview.Caster.Atk = 0;
            project.Preview.Obstacles = Array.Empty<PreviewObstacleDocument>();
            project.Preview.Targets = Enumerable.Range(0, 3).Select(i => new PreviewActorDocument
            {
                Id = "t" + i,
                X = i * 2,
                Z = 0,
                Hp = 1000,
                Def = 0,
                Shield = i == 0 ? shield : 0
            }).ToArray();
            return project;
        }

        static SkillSimulationSnapshot Fire(SkillProjectDocument project)
        {
            using (var simulation = new SkillSimulation(project, CombatContentCompiler.Compile(project), false))
            {
                Assert.That(simulation.Cast("piercing-ray", 10, 0).Accepted, Is.True);
                simulation.Advance(12);
                return simulation.Snapshot();
            }
        }

        static SkillEdgeDocument Edge(string source, string port, string target) => new SkillEdgeDocument
        {
            Id = source + "-" + port + "-" + target,
            Source = source,
            SourcePort = port,
            Target = target,
            TargetPort = "execute"
        };
    }
}
