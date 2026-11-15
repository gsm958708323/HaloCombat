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
    public sealed class SummonJsonTests
    {
        static SkillProjectDocument Example()=>SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
        static string Publish(SkillProjectDocument p,string key)=>SkillPublicationJson.Write(SkillPublicationJson.Create(p,key));
        static SkillNodeDocument Summon(SkillProjectDocument p)=>p.Skills.First(s=>s.Id=="summon-gunners").Nodes.First(n=>n.Kind=="delivery");

        [Test]
        public void PublicationContainsTheAttackSkillAndOnlyTheRequiredBlueprint()
        {
            var p=Example();p.Skills=p.Skills.Reverse().ToArray();
            var bundle=SkillPublicationJson.Read(Publish(p,"summon-gunners"));
            CollectionAssert.AreEquivalent(new[]{"summon-gunners","basic-shot"},bundle.Document.Project.Skills.Select(s=>s.Id));
            CollectionAssert.AreEqual(new[]{"echo-gunner"},bundle.Document.Project.SummonBlueprints.Select(b=>b.Id));
            var plain=SkillPublicationJson.Read(Publish(p,"basic-shot"));
            Assert.AreEqual(1,plain.Document.Project.Skills.Length);Assert.AreEqual(0,plain.Document.Project.SummonBlueprints.Length);
        }

        [Test]
        public void TwoSlotsMergeSharedAttackDefinitionsAndRejectConflictingVersionsAtomically()
        {
            var p=Example();string old=Publish(p,"basic-shot");
            using(var session=new PublishedSkillSession())
            {
                session.Replace(new Dictionary<string,string>{{"f6",old},{"f7",Publish(p,"summon-gunners")}});
                Assert.IsTrue(session.Simulation.Cast("summon-gunners",0,0).Accepted);session.Simulation.Advance(80);
                Assert.Greater(session.Simulation.Snapshot().Statistics.Damage,0);
                var original=session.Simulation;
                p.Skills.First(s=>s.Id=="basic-shot").Nodes.First(n=>n.Kind=="damage").Parameters["flat"]=130;
                StringAssert.Contains("Conflicting",Assert.Throws<ContentValidationException>(()=>session.Replace(new Dictionary<string,string>{{"f6",old},{"f7",Publish(p,"summon-gunners")}})).Message);
                Assert.AreSame(original,session.Simulation);
            }
        }

        [Test]
        public void UnknownBlueprintAndAttackReferenceAreErrorsWithPaths()
        {
            var p=Example();Summon(p).Parameters["blueprint"]="missing";
            StringAssert.Contains("parameters.blueprint",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            p=Example();p.SummonBlueprints[0].SkillId="missing";
            StringAssert.Contains("summonBlueprints[0].skillId",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            p=Example();p.SummonBlueprints[0].InitialEnergy=101;
            StringAssert.Contains("initialEnergy",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            p=Example();p.SummonBlueprints[0].Id="projectile";
            StringAssert.Contains("reserved",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [Test]
        public void SummonSkillRecursionIsRejectedInsteadOfBuildingAnUnboundedActorTree()
        {
            var p=Example();p.SummonBlueprints[0].SkillId="summon-gunners";
            StringAssert.Contains("Recursive",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
            p=Example();p.SummonBlueprints[0].SkillId="summon-blade";p.SummonBlueprints[1].SkillId="summon-gunners";
            StringAssert.Contains("Recursive",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [TestCase(8,true)]
        [TestCase(9,false)]
        public void DependencyDepthHasAnExplicitBound(int levels,bool accepted)
        {
            var p=Example();var prototype=p.Skills.First(s=>s.Id=="summon-gunners");var skills=p.Skills.ToList();var blueprints=p.SummonBlueprints.ToList();
            for(int i=0;i<levels;i++)
            {
                var graph=JObject.FromObject(prototype).ToObject<SkillGraphDocument>();graph.Id="level-"+i;graph.RuntimeId=3000+i;
                var node=graph.Nodes.First(n=>n.Kind=="delivery");node.RuntimeId=9000+i;node.Parameters["blueprint"]="blueprint-"+i;
                blueprints.Add(new SummonBlueprintDocument{Id="blueprint-"+i,SkillId=i==levels-1?"basic-shot":"level-"+(i+1)});skills.Add(graph);
            }
            p.Skills=skills.ToArray();p.SummonBlueprints=blueprints.ToArray();
            if(accepted)Assert.AreEqual(levels+1,SkillPublicationJson.Read(Publish(p,"level-0")).Document.Project.Skills.Length);
            else StringAssert.Contains("depth",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [Test]
        public void DisabledSummonDoesNotIntroduceAFalseDependencyCycle()
        {
            var p=Example();p.SummonBlueprints[0].SkillId="summon-gunners";var skill=p.Skills.First(s=>s.Id=="summon-gunners");Summon(p).Enabled=false;
            skill.Nodes=skill.Nodes.Concat(new[]{new SkillNodeDocument{Id="cue",Kind="cue",Parameters=JObject.FromObject(new{id=1})}}).ToArray();
            skill.Edges=skill.Edges.Concat(new[]{new SkillEdgeDocument{Id="cue",Source="cast",SourcePort="cast",Target="cue",TargetPort="execute",Order=1}}).ToArray();
            Assert.AreEqual(1,SkillPublicationJson.Read(Publish(p,"summon-gunners")).Document.Project.Skills.Length);
        }
    }
}
