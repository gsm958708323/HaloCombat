using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class BuffJsonTests
    {
        static SkillProjectDocument Example()=>SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
        static string Publish(SkillProjectDocument p,string id)=>SkillPublicationJson.Write(SkillPublicationJson.Create(p,id));

        [Test]
        public void SharedBuffLifecycleRoundtripsAndPublishesOnlyItsRequiredDefinitions()
        {
            var p=Example();var bundle=SkillPublicationJson.Read(Publish(p,"burn-shot"));
            Assert.AreEqual(1,bundle.Document.Project.Skills.Length);Assert.AreEqual(1,bundle.Document.Project.Buffs.Length);Assert.AreEqual("burn",bundle.Document.Project.Buffs[0].Id);
            var again=SkillPublicationJson.Read(SkillPublicationJson.Write(bundle.Document));
            Assert.IsTrue(JToken.DeepEquals(JObject.FromObject(bundle.Document),JObject.FromObject(again.Document)));
            bundle.Content.Buffs.TryGet("burn",out var first);again.Content.Buffs.TryGet("burn",out var second);
            Assert.AreNotSame(first,second);Assert.AreNotSame(first.OnPeriod[0],second.OnPeriod[0]);
            var plain=SkillPublicationJson.Read(Publish(p,"basic-shot"));Assert.AreEqual(0,plain.Document.Project.Buffs.Length);
        }

        [Test]
        public void BuffToSummonToSkillReferencesAreIncludedInTheSameClosure()
        {
            var p=Example();var buff=p.Buffs.First(b=>b.Id=="burn");
            buff.Nodes=new[]{buff.Nodes[0],new SkillNodeDocument{Id="summon",Kind="delivery",RuntimeId=9501,Parameters=JObject.FromObject(new{mode="summon",blueprint="echo-gunner",center="target",duration=.1f,count=1})}};
            buff.Edges=new[]{new SkillEdgeDocument{Id="spawn",Source="events",SourcePort="period",Target="summon",TargetPort="execute"}};
            var publication=SkillPublicationJson.Read(Publish(p,"burn-shot"));
            CollectionAssert.AreEquivalent(new[]{"basic-shot","burn-shot"},publication.Document.Project.Skills.Select(s=>s.Id));
            Assert.AreEqual(1,publication.Document.Project.SummonBlueprints.Length);Assert.AreEqual("echo-gunner",publication.Document.Project.SummonBlueprints[0].Id);
            Assert.AreEqual(1,publication.Document.Project.Buffs.Length);
        }

        [Test]
        public void TwoSlotsRejectConflictingBuffVersionsWithoutReplacingTheLiveWorld()
        {
            var p=Example();string shot=Publish(p,"burn-shot");
            using(var session=new PublishedSkillSession())
            {
                session.Replace(new Dictionary<string,string>{{"f6",shot},{"f7",Publish(p,"cleanse-burn")}});var before=session.Simulation;
                p.Buffs.First(b=>b.Id=="burn").Duration=3;
                StringAssert.Contains("Conflicting Buff",Assert.Throws<ContentValidationException>(()=>session.Replace(new Dictionary<string,string>{{"f6",shot},{"f7",Publish(p,"cleanse-burn")}})).Message);
                Assert.AreSame(before,session.Simulation);
            }
        }

        [Test]
        public void UnknownReferenceRecursiveEffectsAndActorActionsInLifecycleGraphsAreRejected()
        {
            var p=Example();p.Skills.First(s=>s.Id=="burn-shot").Nodes.First(n=>n.Kind=="buff").Parameters["buff"]="missing";
            StringAssert.Contains("parameters.buff",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            p=Example();var buff=p.Buffs.First(b=>b.Id=="burn");
            buff.Nodes=new[]{buff.Nodes[0],new SkillNodeDocument{Id="recursive",Kind="buff",Parameters=JObject.FromObject(new{buff="burn",stacks=1,recipient="target"})}};
            buff.Edges=new[]{new SkillEdgeDocument{Id="recursive",Source="events",SourcePort="period",Target="recursive",TargetPort="execute"}};
            StringAssert.Contains("Recursive",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
            buff.Nodes[1].Kind="delivery";buff.Nodes[1].Parameters=JObject.FromObject(new{mode="melee",shape="thrust",length=1});
            StringAssert.Contains("Actor actions",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [TestCase("tags")]
        [TestCase("attribute")]
        [TestCase("interval")]
        [TestCase("runtimeId")]
        public void BuffValidationLocatesTheBrokenDefinitionAndField(string field)
        {
            var p=Example();var buff=p.Buffs[0];
            if(field=="tags")buff.Tags=new[]{3101,3101};
            if(field=="attribute")buff.Modifiers[0].Attribute="hp";
            if(field=="interval")buff.Interval=0;
            if(field=="runtimeId")buff.RuntimeId=-1;
            var error=Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));StringAssert.Contains("buffs[0]",error.Path);StringAssert.Contains(field,error.Path);
        }
    }
}
