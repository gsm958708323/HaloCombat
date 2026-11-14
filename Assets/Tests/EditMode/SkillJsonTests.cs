using System;
using System.IO;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SkillJsonTests
    {
        static string Example => File.ReadAllText(Path.Combine(Application.dataPath, "../Content/SkillLab/project.json"));

        [Test]
        public void JsonRoundtripBuildsFreshDefinitionsAndPreservesCastRules()
        {
            var document = SkillProjectJson.Read(Example);
            var first = CombatContentCompiler.Compile(document);
            var second = CombatContentCompiler.Compile(SkillProjectJson.Read(SkillProjectJson.Write(document)));
            Assert.IsTrue(first.Skills.TryGet(new SkillNodeId(2001), out var skill));
            Assert.AreEqual(.7f, skill.Cooldown, .0001f);
            Assert.AreEqual(10f, skill.EnergyCost);
            Assert.IsTrue(first.Timelines.TryGet(skill.Timeline, out var timeline));
            Assert.IsTrue(second.Timelines.TryGet(skill.Timeline, out var other));
            Assert.AreNotSame(timeline, other);
            Assert.AreNotSame(timeline.Payloads[0].Effects[0], other.Payloads[0].Effects[0]);
        }

        [TestCase("speed", -1)]
        [TestCase("unknownField", 10)]
        public void InvalidParametersAreRejectedWithNodeAndField(string field, float value)
        {
            var json = JObject.Parse(Example);
            json["skills"][0]["nodes"][2]["parameters"][field] = value;
            var error = Assert.Throws<ContentValidationException>(() => CombatContentCompiler.Compile(SkillProjectJson.Read(json.ToString())));
            StringAssert.Contains("projectile", error.Message);
            StringAssert.Contains(field, error.Message);
        }

        [Test]
        public void RejectedCastDoesNotSpendEnergyOrReplacePlayingTimeline()
        {
            var content = CombatContentCompiler.Compile(SkillProjectJson.Read(Example));
            var world = new CombatWorld(new Factory(content));
            var id = world.SpawnActor(new ActorSpawnSpec("caster"));
            world.TryGetActor(id, out var actor);
            var cast = actor.GetComp<SkillCastComp>();
            var aim = new SkillAim(EntityId.Invalid, new SimVec3(4, 0, 0), 0);
            Assert.IsTrue(cast.TryCast(new SkillNodeId(2001), aim));
            Assert.AreEqual(90, actor.GetComp<EnergyComp>().Current);
            Assert.IsFalse(cast.TryCast(new SkillNodeId(2001), aim));
            Assert.AreEqual(90, actor.GetComp<EnergyComp>().Current);
            Assert.IsTrue(actor.GetComp<SkillDirectorComp>().IsPlaying);
        }

        [Test]
        public void ExpiredExplicitTargetDoesNotSelectANewEntityWithTheSameIndex()
        {
            var document = SkillProjectJson.Read(Example);
            document.Skills[0].Nodes[0].Parameters["mode"] = "entity";
            var content = CombatContentCompiler.Compile(document);
            var world = new CombatWorld(new Factory(content));
            var casterId = world.SpawnActor(new ActorSpawnSpec("caster"));
            var oldTarget = world.SpawnActor(new ActorSpawnSpec("target"));
            world.RequestDespawn(oldTarget); world.Tick(0);
            var replacement = world.SpawnActor(new ActorSpawnSpec("target"));
            world.TryGetActor(replacement, out var target); target.GetComp<TeamComp>().SetTeam(2);
            Assert.AreEqual(oldTarget.Index, replacement.Index);
            Assert.AreNotEqual(oldTarget.Generation, replacement.Generation);
            world.TryGetActor(casterId, out var caster);
            Assert.IsFalse(caster.GetComp<SkillCastComp>().TryCast(new SkillNodeId(2001), new SkillAim(oldTarget, SimVec3.Zero, 0)));
            Assert.AreEqual(100, caster.GetComp<EnergyComp>().Current);
            Assert.IsFalse(caster.GetComp<SkillDirectorComp>().IsPlaying);
        }

        sealed class Factory : IActorFactory
        {
            readonly CombatContentSnapshot _content;
            public Factory(CombatContentSnapshot content) => _content = content;
            public Actor Create(in ActorSpawnSpec spec)
            {
                var actor = new Actor();
                actor.AddComp(new TransformComp()); actor.AddComp(new TagComp()); actor.AddComp(new TeamComp(1));
                var attr = new AttributeSet(); attr.InitFighterDefaults(); actor.AddComp(attr);
                actor.AddComp(new BuffComp()); actor.AddComp(new HealthComp()); actor.AddComp(new StateMachineComp()); actor.AddComp(new LocomotionComp());
                actor.AddComp(new SkillDirectorComp(_content.Timelines)); actor.AddComp(new EnergyComp(100, 0));
                actor.AddComp(new SkillCastComp(_content.Skills));
                return actor;
            }
            public void Release(Actor actor) => actor.ResetForPool();
        }
    }
}
