using System;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Combat.Tests.ProjectileBehaviorTestProject;

namespace Combat.Tests
{
    public sealed class ProjectilePolicyJsonTests
    {
        internal static SkillNodeDocument Behavior(SkillProjectDocument p,string mode="pierce")
        {
            var root=Node(p,"projectile");foreach(string key in new[]{"maxHits","chain","chainCount","chainRadius","chainMode"})root.Parameters.Remove(key);
            var behavior=new SkillNodeDocument{Id="behavior",Kind="projectile-behavior",Parameters=JObject.FromObject(new{mode,maxHits=3,chainCount=2,chainRadius=8,chainMode="nearest"})};
            p.Skills[0].Nodes=p.Skills[0].Nodes.Concat(new[]{behavior}).ToArray();
            p.Skills[0].Edges=p.Skills[0].Edges.Concat(new[]{new SkillEdgeDocument{Id="behavior-input",Source=behavior.Id,SourcePort="behavior",Target=root.Id,TargetPort="behavior"}}).ToArray();return behavior;
        }

        [TestCase("pierce",3,0)]
        [TestCase("chain",1,2)]
        public void DataBehaviorCompilesToOneRuntimeDefinitionAndExecutesRealHits(string mode,int hits,int chains)
        {
            var p=Create();Behavior(p,mode);var content=CombatContentCompiler.Compile(p);content.Projectiles.TryGet(Node(p,"projectile").RuntimeId,out var def);
            Assert.AreEqual(hits,def.MaxHits);Assert.AreEqual(chains,def.Chain?.ExtraChains??0);
            using(var simulation=Run(p)){simulation.Cast(p.Skills[0].Id,10,0);simulation.Advance(100);Assert.AreEqual(3,simulation.Snapshot().Statistics.Hits);Assert.AreEqual(chains,simulation.Snapshot().Statistics.Chains);}
        }

        [Test]
        public void UnlimitedAndDisabledBehaviorDoNotLeaveASecondInlinePolicy()
        {
            var p=Create();var b=Behavior(p);b.Parameters["unlimited"]=true;var content=CombatContentCompiler.Compile(p);content.Projectiles.TryGet(Node(p,"projectile").RuntimeId,out var def);Assert.AreEqual(0,def.MaxHits);
            b.Enabled=false;content=CombatContentCompiler.Compile(p);content.Projectiles.TryGet(Node(p,"projectile").RuntimeId,out def);Assert.AreEqual(1,def.MaxHits);Assert.IsNull(def.Chain);
            Cue(p,Node(p,"cast").Id,"cast",9913,1);Node(p,"projectile").Enabled=false;b.Enabled=true;Assert.DoesNotThrow(()=>CombatContentCompiler.Compile(p));
        }

        [TestCase("maxHits")][TestCase("chain")][TestCase("chainCount")][TestCase("chainRadius")][TestCase("chainMode")]
        public void AnEnabledBehaviorRejectsConflictingInlineFields(string key)
        {
            var p=Create();Behavior(p);Node(p,"projectile").Parameters[key]=key=="chain"?(JToken)false:key=="chainMode"?(JToken)"nearest":1;
            StringAssert.Contains("parameters."+key,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [TestCase("second")][TestCase("type")][TestCase("port")][TestCase("cycle")]
        public void InvalidBehaviorWiringIsRejectedBeforeCreatingARuntime(string fault)
        {
            var p=Create();var b=Behavior(p);var edge=p.Skills[0].Edges.Single(e=>e.Id=="behavior-input");
            if(fault=="type")b.Kind="cue";if(fault=="port")edge.SourcePort="next";
            if(fault=="second"){
                var second=new SkillNodeDocument{Id="other-behavior",Kind=b.Kind,Parameters=(JObject)b.Parameters.DeepClone()};p.Skills[0].Nodes=p.Skills[0].Nodes.Concat(new[]{second}).ToArray();
                p.Skills[0].Edges=p.Skills[0].Edges.Concat(new[]{new SkillEdgeDocument{Id="second",Source=second.Id,SourcePort="behavior",Target=edge.Target,TargetPort="behavior"}}).ToArray();
            }
            if(fault=="cycle")p.Skills[0].Edges=p.Skills[0].Edges.Concat(new[]{new SkillEdgeDocument{Id="cycle",Source=edge.Target,SourcePort="hit",Target=b.Id,TargetPort="execute"}}).ToArray();
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }

        [TestCase("contactMode","unknown")][TestCase("sameTargetMode","unknown")][TestCase("flightTrigger","unknown")]
        public void UnknownPolicyEnumsIdentifyTheirFields(string key,string value)
        {var p=Create();Node(p,"projectile").Parameters[key]=value;StringAssert.Contains("parameters."+key,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);}

        [TestCase("flightInterval",0)][TestCase("flightInterval",3601)][TestCase("flightDistance",0)][TestCase("flightDistance",1001)]
        public void InvalidFlightIntervalsIdentifyTheirFields(string key,float value)
        {var p=Create();Node(p,"projectile").Parameters[key]=value;StringAssert.Contains("parameters."+key,Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);}

        [Test]
        public void IntervalPolicyNeedsADelayAndChainNeedsBehaviorContact()
        {
            var p=Create();Node(p,"projectile").Parameters["sameTargetMode"]="interval";
            StringAssert.Contains("sameTargetDelay",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            p=Create();Behavior(p,"chain");Node(p,"projectile").Parameters["contactMode"]="continue";
            StringAssert.Contains("contactMode",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
        }

        [Test]
        public void FlightBranchesNeedAnEnabledClockAndCannotInheritAHitSplitContext()
        {
            var p=Create();Cue(p,Node(p,"projectile").Id,"flight",9911);
            StringAssert.Contains("flightTrigger",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Path);
            Node(p,"projectile").Parameters["flightTrigger"]="time";Assert.DoesNotThrow(()=>CombatContentCompiler.Compile(p));
            p=Create("split-shot");var root=Node(p,"projectile");root.Parameters["flightTrigger"]="distance";var edge=p.Skills[0].Edges.Single(e=>e.Target==Node(p,"split").Id);edge.Source=root.Id;edge.SourcePort="flight";
            StringAssert.Contains("projectile hit",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }

        [TestCase("time")][TestCase("distance")]
        public void PublicationRoundtripRetainsBehaviorFlightEventsAndOriginalCastIdentity(string clock)
        {
            var p=Create();Behavior(p);var root=Node(p,"projectile");root.Parameters["contactMode"]="ignore";root.Parameters["flightTrigger"]=clock;root.Parameters["flightInterval"]=.2f;root.Parameters["flightDistance"]=2;
            Cue(p,root.Id,"flight",9912);var publication=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(p,p.Skills[0].Id)));
            using(var simulation=new SkillSimulation(publication.Document.Project,publication.Content)){
                simulation.Cast(p.Skills[0].Id,10,0);simulation.Advance(50);var state=simulation.Snapshot();var flights=state.Events.Where(e=>e.Kind=="projectile-flight").ToArray();
                Assert.AreEqual(5,flights.Length);Assert.IsTrue(flights.All(e=>e.CastId>0&&e.Node.NodeId==root.Id&&e.BodyId!=null&&e.Flight.Clock==clock));
                Assert.AreEqual(5,state.Events.Count(e=>e.Kind=="cue"&&e.Result.Contains("9912")));Assert.AreEqual(0,state.Statistics.Hits);
                Assert.AreEqual(5,flights.Last().Flight.Index);Assert.AreEqual(1,flights.Last().Flight.Age,.00001);Assert.AreEqual(10,flights.Last().Flight.Distance,.00001);
            }
        }
    }
}
