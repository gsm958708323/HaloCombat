using System;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Combat.Tests.ProjectileBehaviorTestProject;

namespace Combat.Tests
{
    public sealed class SkillEventJsonTests
    {
        [Test]
        public void SkillEventGraphsRoundtripWithoutSharingCompiledHandlers()
        {
            var p=Create("event-shot");var a=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,"event-shot")));
            var b=SkillPublicationJson.Read(SkillPublicationJson.Write(a.Document));Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(a.Document),JObject.FromObject(b.Document)));
            a.Content.SkillEvents.TryGet(new Combat.Core.SkillNodeId(p.Skills[0].RuntimeId),out var first);
            b.Content.SkillEvents.TryGet(new Combat.Core.SkillNodeId(p.Skills[0].RuntimeId),out var second);
            Assert.AreNotSame(first,second);Assert.AreNotSame(first.Get(Combat.Core.SkillEventKind.Hit)[0],second.Get(Combat.Core.SkillEventKind.Hit)[0]);
        }

        [Test]
        public void UnknownDamageTypeAndNonBooleanBonusAreRejectedWithFieldPaths()
        {
            var p=Create();Node(p,"damage").Parameters["damageType"]="fire-script";
            StringAssert.Contains("damageType",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            Node(p,"damage").Parameters["damageType"]="physical";Node(p,"damage").Parameters["includeBonuses"]="yes";
            StringAssert.Contains("includeBonuses",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [Test]
        public void DuplicateSkillEventRootsAndProjectileSplittingFromGeneralEventsAreRejected()
        {
            var p=Create("event-shot");var root=Node(p,"skill-events");
            p.Skills[0].Nodes=p.Skills[0].Nodes.Concat(new[]{new SkillNodeDocument{Id="duplicate",Kind="skill-events"}}).ToArray();
            StringAssert.Contains("Only one",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
            p=Create("event-shot");root=Node(p,"skill-events");
            p.Skills[0].Nodes=p.Skills[0].Nodes.Concat(new[]{new SkillNodeDocument{Id="split",Kind="split"}}).ToArray();
            p.Skills[0].Edges=p.Skills[0].Edges.Concat(new[]{new SkillEdgeDocument{Id="invalid",Source=root.Id,SourcePort="hit",Target="split",TargetPort="execute",Order=2}}).ToArray();
            StringAssert.Contains("projectile hit",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [TestCase("shield",-1)]
        [TestCase("damageDealt",-1)]
        [TestCase("damageTaken",101)]
        public void SceneAttributesAreValidatedBeforeBuildingTheWorld(string field,int value)
        {
            var p=Create();var json=JObject.Parse(SkillProjectJson.Write(p));json["preview"]["caster"][field]=value;
            StringAssert.Contains(field,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(SkillProjectJson.Read(json.ToString()))).Path);
        }
    }
}
