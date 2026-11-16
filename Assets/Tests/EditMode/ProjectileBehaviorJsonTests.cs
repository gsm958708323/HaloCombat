using System;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Combat.Tests.ProjectileBehaviorTestProject;

namespace Combat.Tests
{
    public sealed class ProjectileBehaviorJsonTests
    {
        [TestCase("count",65)]
        [TestCase("spread",361)]
        [TestCase("chainCount",65)]
        [TestCase("chainRadius",-1)]
        public void InvalidProjectileFieldsIdentifyTheirExactPath(string field,int value)
        {
            var p=Create();Node(p,"projectile").Parameters[field]=value;
            StringAssert.Contains("parameters."+field,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [TestCase("count",33)]
        [TestCase("generations",9)]
        [TestCase("powerMultiplier",0)]
        [TestCase("range",-1)]
        public void InvalidSplitFieldsIdentifyTheirExactPath(string field,int value)
        {
            var p=Create("split-shot");Node(p,"split").Parameters[field]=value;
            StringAssert.Contains("parameters."+field,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [Test]
        public void UnlimitedPiercingAndChainCannotSilentlyOverrideEachOther()
        {
            var p=Create("chain-shot");Node(p,"projectile").Parameters["maxHits"]=0;
            StringAssert.Contains("maxHits",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [TestCase("cast")]
        [TestCase("wall")]
        [TestCase("range")]
        [TestCase("lifetime")]
        [TestCase("expire")]
        public void SplitCannotBeConnectedToAnEventWithoutAProjectileHit(string port)
        {
            var p=Create("split-shot");var split=Node(p,"split");
            p.Skills[0].Edges=p.Skills[0].Edges.Where(e=>e.Target!=split.Id).Concat(new[]{new SkillEdgeDocument{
                Id="bad-context",Source=port=="cast"?Node(p,"cast").Id:Node(p,"projectile").Id,SourcePort=port,Target=split.Id,TargetPort="execute",Order=1}}).ToArray();
            StringAssert.Contains("projectile hit",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [Test]
        public void ReusingOneSplitFromAValidAndInvalidBranchStillRejectsTheInvalidContext()
        {
            var p=Create("split-shot");var split=Node(p,"split");
            p.Skills[0].Edges=p.Skills[0].Edges.Concat(new[]{new SkillEdgeDocument{Id="bad-context",Source=Node(p,"cast").Id,SourcePort="cast",Target=split.Id,TargetPort="execute",Order=1}}).ToArray();
            StringAssert.Contains("projectile hit",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [Test]
        public void PublicationRoundtripPreservesBehaviorsOrderingAndIndependentDefinitions()
        {
            foreach(string id in new[]{"spread-shot","chain-shot","split-shot","fan-channel"})
            {
                var p=Create(id);var original=SkillPublicationJson.Create(p,id);var first=SkillPublicationJson.Read(SkillPublicationJson.Write(original));
                var second=SkillPublicationJson.Read(SkillPublicationJson.Write(first.Document));
                Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(first.Document),JObject.FromObject(second.Document)));
                int runtime=Node(p,"projectile").RuntimeId;first.Content.Projectiles.TryGet(runtime,out var a);second.Content.Projectiles.TryGet(runtime,out var b);
                Assert.AreNotSame(a,b);Assert.AreNotSame(a.OnHit[0],b.OnHit[0]);Assert.AreEqual(a.OnHit.OfType<ChainProjectileEffect>().FirstOrDefault()?.Count,b.OnHit.OfType<ChainProjectileEffect>().FirstOrDefault()?.Count);
            }
        }
    }
}
