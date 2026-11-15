using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class AuthorTraceTests
    {
        static SkillProjectDocument Example()=>SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        static SkillSimulation Run(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));

        [TestCase("basic-shot",1)] [TestCase("burst-shot",3)]
        public void RealDamageHasItsActualNodeCastAndFullEntityIdentities(string skill,int hits)
        {
            var p=Example();using(var s=Run(p))
            {
                s.Cast(skill,0,0);s.Advance(140);var state=s.Snapshot();var damage=state.Events.Where(e=>e.Kind=="damage"||e.Kind=="critical").ToArray();
                Assert.AreEqual(hits,damage.Length);Assert.IsTrue(damage.All(e=>e.Node.GraphKind=="skill"&&e.Node.GraphId==skill&&e.Node.NodeKind=="damage"));
                Assert.IsTrue(damage.All(e=>e.CastId>0&&e.ExecutionId>0&&e.SourceId==state.Actors.Single(a=>a.Kind=="caster").Id));
                Assert.IsTrue(damage.All(e=>e.TargetId!=null&&state.Actors.Any(a=>a.Id==e.TargetId)));Assert.IsTrue(damage.All(e=>e.Step>0));
                Assert.AreEqual("cast",state.Events.Single(e=>e.Kind=="cast").Node.NodeKind);
                var spawned=state.Events.First(e=>e.Kind=="spawn"&&e.Result=="projectile");
                Assert.AreEqual("projectile",spawned.Node.NodeKind);Assert.IsNotNull(spawned.BodyId);
                Assert.IsTrue(state.Events.Any(e=>e.Kind=="cleanup"&&e.BodyId==spawned.BodyId));
            }
        }

        [Test]
        public void SharedProjectileDamageLocatesTheDefinitionAndKeepsTheCallingSkill()
        {
            var p=Formal();p.Preview.Targets[0].X=1;
            using(var s=Run(p))
            {
                s.Cast("arena-double-shot",4,0);s.Advance(80);
                var damage=s.Snapshot().Events.Where(e=>e.Kind=="damage").ToArray();Assert.Greater(damage.Length,0);
                Assert.IsTrue(damage.All(e=>e.Node.GraphKind=="projectile"&&e.Skill=="arena-double-shot"));
                Assert.IsTrue(damage.All(e=>p.Projectiles.Single(d=>d.Id==e.Node.GraphId).Nodes.Any(n=>n.Id==e.Node.NodeId&&n.Kind=="damage")));
            }
        }

        [Test]
        public void TheSharedTimelineKeepsItsOwnEffectNodeAndTheCallersCastIdentity()
        {
            var p=Formal();var skill=p.Skills.First(s=>s.TimelineRef!=null);p.Preview.Targets[0].X=1;
            using(var s=Run(p))
            {
                s.Cast(skill.Id,4,0);s.Advance(100);var executed=s.Snapshot().Events.Where(e=>e.Kind=="execute"&&e.Node.GraphKind=="timeline").ToArray();
                Assert.Greater(executed.Length,0);Assert.IsTrue(executed.All(e=>e.Node.GraphId==skill.TimelineRef&&e.Skill==skill.Id&&e.CastId==1));
            }
        }

        [Test]
        public void SpatialActionEventsPointToTheActionDeliveryNode()
        {
            var p=Example();using(var s=Run(p))
            {
                s.Cast("dash-strike",0,0);s.Advance(80);var events=s.Snapshot().Events.Where(e=>e.Kind=="action-open"||e.Kind=="action-close").ToArray();
                Assert.Greater(events.Length,0);Assert.IsTrue(events.All(e=>e.Node.NodeKind=="delivery"&&e.Node.GraphId=="dash-strike"&&e.CastId==1));
            }
        }

        [Test]
        public void DelayedBuffTicksPointIntoTheBuffGraphAfterTheCastEnds()
        {
            var p=Example();using(var s=Run(p))
            {
                s.Cast("burn-shot",0,0);s.Advance(200);var damage=s.Snapshot().Events.Where(e=>e.Kind=="damage").ToArray();
                Assert.Greater(damage.Length,0);Assert.IsTrue(damage.All(e=>e.Node.GraphKind=="buff"&&e.Skill=="burn-shot"));
                Assert.IsTrue(damage.All(e=>e.CastId==1));
                Assert.Greater(damage.Last().Step,s.Snapshot().Events.Single(e=>e.Kind=="completed").Step);
            }
        }

        [Test]
        public void RepeatedEffectEntriesRemainTwoExecutionsOfTheSameAuthorNode()
        {
            var p=Formal();var skill=p.Skills.Single(s=>s.Id=="arena-controlled-shot");
            skill.Timeline.Payloads=Array.Empty<TimelinePayloadDocument>();skill.Timeline.Clips=new[]{new TimelineClipDocument{Id="hit",Kind="hitbox",Start=0,End=.04f,HitRadius=1,Effects=new[]{"damage","damage"}}};
            skill.Nodes=skill.Nodes.Where(n=>n.Kind!="projectile").ToArray();skill.Edges=skill.Edges.Where(e=>e.Source!="shot").ToArray();p.Preview.Targets[0].X=.2f;
            using(var s=Run(p))
            {
                s.Cast(skill.Id,4,0);s.Advance(3);var damage=s.Snapshot().Events.Where(e=>e.Kind=="damage").ToArray();
                Assert.AreEqual(2,damage.Length);Assert.AreEqual(damage[0].Node.NodeId,damage[1].Node.NodeId);Assert.AreNotEqual(damage[0].ExecutionId,damage[1].ExecutionId);
                Assert.Less(damage[0].Sequence,damage[1].Sequence);
            }
        }

        [Test]
        public void NestedEffectsRestoreTheirParentsAndAnotherWorldHasNoSharedTraceState()
        {
            var p=Example();var content=CombatContentCompiler.Compile(p);var trace=new AuthorExecutionTrace(content.Sources);var other=new AuthorExecutionTrace(content.Sources);
            var projectile=content.Skills.All.Single(s=>s.Key=="basic-shot");content.Timelines.TryGet(projectile.Timeline,out var timeline);var mapped=timeline.Payloads[0].Effects[0];
            var context=new EffectContext();int first=trace.Enter(mapped,context);Assert.IsNotNull(trace.Current.Node);Assert.IsNull(other.Current.Node);
            var parent=trace.Current;var pipeline=new EffectPipeline(trace);
            Assert.Throws<InvalidOperationException>(()=>{var ctx=new EffectContext();pipeline.Run(ref ctx,new IEffect[]{new CallbackEffect(()=>throw new InvalidOperationException("probe"))});});
            Assert.AreEqual(parent.Id,trace.Current.Id);trace.Exit(first);Assert.IsNull(trace.Current.Node);
            int next=other.Enter(mapped,context);Assert.AreEqual(1,other.Current.Id);other.Exit(next);
            Action<AuthorExecution> reject=e=>throw new InvalidOperationException("observer probe");trace.Executing+=reject;
            Assert.Throws<InvalidOperationException>(()=>trace.Enter(mapped,context));Assert.IsNull(trace.Current.Node);trace.Executing-=reject;
        }

        [Test]
        public void ReentrantSkillEventEffectsKeepTheirActualParentExecution()
        {
            using(var s=Run(Example()))
            {
                s.Cast("event-shot",0,0);s.Advance(100);var events=s.Snapshot().Events;
                var nested=events.Where(e=>e.Kind=="execute"&&e.ParentExecutionId>0).ToArray();Assert.Greater(nested.Length,0);
                foreach(var child in nested)
                {
                    var parent=events.Single(e=>e.Kind=="execute"&&e.ExecutionId==child.ParentExecutionId);
                    Assert.Less(parent.Sequence,child.Sequence);Assert.AreEqual(parent.CastId,child.CastId);Assert.IsNotNull(child.Node);
                }
            }
        }

        [Test]
        public void LayoutAndPublicationRoundtripPreserveSourceIdentityAndExecutionSemantics()
        {
            var p=Example();var copy=SkillProjectJson.Read(SkillProjectJson.Write(p));foreach(var n in copy.Skills.SelectMany(s=>s.Nodes))n.Position=new NodePosition{X=900,Y=500};
            var pub=SkillPublicationJson.Read(SkillPublicationJson.Write(SkillPublicationJson.Create(copy,"burn-shot"))).Document.Project;
            using(var a=Run(p))using(var b=Run(pub))
            {
                a.Cast("burn-shot",0,0);b.Cast("burn-shot",0,0);a.Advance(200);b.Advance(200);
                var x=a.Snapshot().Events.Select(e=>new{e.Kind,e.CastId,e.SourceId,e.TargetId,e.BodyId,e.Node});
                var y=b.Snapshot().Events.Select(e=>new{e.Kind,e.CastId,e.SourceId,e.TargetId,e.BodyId,e.Node});
                Assert.IsTrue(JToken.DeepEquals(JArray.FromObject(x),JArray.FromObject(y)));Assert.AreEqual(a.Snapshot().Statistics.Damage,b.Snapshot().Statistics.Damage);
            }
        }
    }
}
