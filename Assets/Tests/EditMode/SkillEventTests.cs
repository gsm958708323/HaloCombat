using System;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using static Combat.Tests.ProjectileBehaviorTestProject;

namespace Combat.Tests
{
    public sealed class SkillEventTests
    {
        static SkillProjectDocument Project(string root="event-shot")
        {var p=Create(root);Node(p,"damage").Parameters["critChance"]=1;return p;}
        static void RemoveConfirmation(SkillProjectDocument p)
        {
            p.Skills[0].Nodes=p.Skills[0].Nodes.Where(n=>n.Kind!="after-damage"&&n.Kind!="impact").ToArray();
            p.Skills[0].Edges=p.Skills[0].Edges.Where(e=>e.Id!="confirm"&&e.Id!="impact").ToArray();
        }

        [Test]
        public void CriticalKillAndCompletionUseTheRealResultAndAdditionalDamageCannotReenter()
        {
            var p=Project();Targets(p,(2,0));p.Preview.Targets[0].Hp=160;RemoveConfirmation(p);
            using(var s=Run(p))
            {
                s.Cast("event-shot",10,0);s.Advance(100);var snapshot=s.Snapshot();
                Assert.AreEqual(160,snapshot.Statistics.Damage);Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="skill-hit"));
                Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="skill-critical"));Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="skill-kill"));
                Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="skill-completed"));Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="skill-projectileend"));
                Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="critical"));Assert.AreEqual(0,snapshot.Events.Count(e=>e.Kind=="damage"));
                var result=snapshot.Events.First(e=>e.Kind=="critical");Assert.AreEqual(160,result.Damage.HpLost);Assert.AreEqual("event-shot",result.Skill);Assert.Greater(result.HitIndex,0);
                int hit=snapshot.Events.Single(e=>e.Kind=="skill-hit").Sequence,crit=snapshot.Events.Single(e=>e.Kind=="skill-critical").Sequence,kill=snapshot.Events.Single(e=>e.Kind=="skill-kill").Sequence;
                Assert.Less(hit,crit);Assert.Less(crit,kill);
            }
            p.Preview.Targets[0].Hp=1000;
            using(var s=Run(p))
            {s.Cast("event-shot",10,0);s.Advance(100);Assert.AreEqual(165,s.Snapshot().Statistics.Damage);Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="skill-hit"));}
        }

        [Test]
        public void PureShieldAndInvulnerabilityDoNotQualifyForHealthEvents()
        {
            var p=Project();Targets(p,(2,0));p.Preview.Targets[0].Shield=1000;RemoveConfirmation(p);
            using(var s=Run(p))
            {s.Cast("event-shot",10,0);s.Advance(100);Assert.AreEqual(160,s.Snapshot().Statistics.Damage);Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Kind=="skill-hit"||e.Kind=="skill-critical"||e.Kind=="skill-kill"));Assert.AreEqual(0,s.Snapshot().Events.Single(e=>e.Kind=="critical").Damage.HpLost);}
        }

        [Test]
        public void OldProjectileKeepsItsEventsButLatestCastConditionDoesNotConfirmANewerCast()
        {
            var p=Project();Targets(p,(8,0));Node(p,"projectile").Parameters["speed"]=4;
            using(var s=Run(p))
            {
                s.Cast("event-shot",10,0);s.Advance(10);s.Cast("event-shot",10,0);s.Advance(160);var snapshot=s.Snapshot();
                Assert.AreEqual(2,snapshot.Events.Count(e=>e.Kind=="skill-hit"));
                Assert.AreEqual(2,snapshot.Events.Where(e=>e.Kind=="skill-hit").Select(e=>e.CastId).Distinct().Count());
                Assert.AreEqual(9.5f,snapshot.Actors.First(a=>a.Name=="target-1").X,.001f);
                Assert.AreEqual(330,snapshot.Statistics.Damage);
            }
        }

        [Test]
        public void InterruptingTheTimelineDoesNotRunTheNaturalCompletionPort()
        {
            var p=Project();Targets(p);Node(p,"cast").Parameters["duration"]=1;
            using(var s=Run(p))
            {s.Cast("event-shot",10,0);s.Advance(5);s.Interrupt();s.Advance(100);Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Kind=="skill-completed"));}
        }

        [TestCase("ground-field")]
        [TestCase("burn-shot")]
        [TestCase("ray-shot")]
        [TestCase("melee-thrust")]
        [TestCase("blink-strike")]
        public void DelayedAndSpatialDamagePreservesItsOriginalSkillAndCast(string id)
        {
            var p=SkillProjectFiles.Read(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../Content/SkillLab/project.json"));
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                s.Cast(id,0,0);s.Advance(140);
                var damage=s.Snapshot().Events.Where(e=>e.Kind=="damage"||e.Kind=="critical").ToArray();Assert.Greater(damage.Length,0);
                Assert.IsTrue(damage.All(e=>e.Skill==id&&e.CastId>0));
            }
        }

        [Test]
        public void EventSpawnedPersistentAreaKeepsTheSuppressionAcrossLaterFrames()
        {
            var p=Project();RemoveConfirmation(p);Targets(p,(2,0));
            var bonus=p.Skills[0].Nodes.First(n=>n.Id=="bonus-damage");bonus.Kind="delivery";bonus.RuntimeId=9911;
            bonus.Parameters=JObject.FromObject(new{mode="area",center="point",radius=2,persistent=true,duration=.3f,interval=.1f,pulseOnSpawn=true});
            p.Skills[0].Nodes=p.Skills[0].Nodes.Concat(new[]{new SkillNodeDocument{Id="area-damage",Kind="damage",Parameters=JObject.FromObject(new{flat=5,coefficient=0,critChance=0})}}).ToArray();
            p.Skills[0].Edges=p.Skills[0].Edges.Concat(new[]{new SkillEdgeDocument{Id="area",Source=bonus.Id,SourcePort="apply",Target="area-damage",TargetPort="execute"}}).ToArray();
            using(var s=Run(p))
            {s.Cast("event-shot",10,0);s.Advance(100);Assert.AreEqual(175,s.Snapshot().Statistics.Damage);Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="skill-hit"));Assert.AreEqual(1,s.Snapshot().Statistics.Areas);}
        }

        [TestCase(false,"skill-range",115)]
        [TestCase(true,"skill-wall",114)]
        public void ProjectileTerminalSkillPortPrecedesTheOneGeneralEnd(bool wall,string kind,int cue)
        {
            var p=Project();RemoveConfirmation(p);Targets(p);Node(p,"projectile").Parameters["range"]=3;
            if(wall)p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=1.4f,Z=0,Width=.1f,Depth=4}};
            using(var s=Run(p))
            {
                s.Cast("event-shot",10,0);s.Advance(100);var events=s.Snapshot().Events;
                Assert.AreEqual(1,events.Count(e=>e.Kind==kind));Assert.AreEqual(1,events.Count(e=>e.Kind=="skill-projectileend"));
                Assert.AreEqual(1,events.Count(e=>e.Kind=="cue"&&e.Result==cue.ToString()));
                Assert.Less(events.Single(e=>e.Kind==kind).Sequence,events.Single(e=>e.Kind=="skill-projectileend").Sequence);
            }
        }

        [Test]
        public void EventAppliedBuffRetainsSuppressionWhenItsLaterPeriodsResolve()
        {
            var p=Project();RemoveConfirmation(p);Targets(p,(2,0));
            var full=SkillProjectFiles.Read(System.IO.Path.Combine(UnityEngine.Application.dataPath,"../Content/SkillLab/project.json"));
            p.Buffs=full.Buffs.Where(b=>b.Id=="burn").ToArray();
            var bonus=p.Skills[0].Nodes.First(n=>n.Id=="bonus-damage");bonus.Kind="buff";bonus.Parameters=JObject.FromObject(new{buff="burn",stacks=1,recipient="target"});
            using(var s=Run(p))
            {
                s.Cast("event-shot",10,0);s.Advance(100);var snapshot=s.Snapshot();
                Assert.AreEqual(200,snapshot.Statistics.Damage);Assert.AreEqual(1,snapshot.Events.Count(e=>e.Kind=="skill-hit"));
                Assert.AreEqual(4,snapshot.Events.Count(e=>e.Kind=="buff-period"));Assert.IsTrue(snapshot.Events.Where(e=>e.Kind=="damage").All(e=>e.Skill=="event-shot"));
            }
        }

        [Test]
        public void AAliveAreaSourceCannotRouteAnExpiredOriginalCasterIdentity()
        {
            int calls=0;var catalog=new SkillEventCatalog();var definition=new SkillEventDefinition();
            definition.Set(SkillEventKind.Hit,new IEffect[]{new CallbackEffect(()=>calls++)});var skill=new SkillNodeId(999);
            catalog.Register(skill,definition);var w=new ProjectileMotionTestWorld(ProjectileMotionTestWorld.Definition(),skillEvents:catalog);
            var area=w.Spawn("aoe",2);w.World.TryGetActor(area,out var source);
            var context=new EffectContext{World=w.World,Source=source,Origin=new SkillOrigin(skill,1,caster:w.Owner)};
            w.World.DispatchSkillEvent(SkillEventKind.Hit,context);Assert.AreEqual(1,calls);
            w.World.TryGetActor(w.Owner,out var owner);owner.GetComp<TagComp>().Add(CommonTags.Dead,1,TagSource.Debug);
            w.World.DispatchSkillEvent(SkillEventKind.Hit,context);Assert.AreEqual(1,calls);
            w.World.RequestDespawn(w.Owner);w.World.Tick(0);var replacement=w.Spawn("owner",0);
            Assert.AreEqual(w.Owner.Index,replacement.Index);Assert.AreNotEqual(w.Owner.Generation,replacement.Generation);
            w.World.DispatchSkillEvent(SkillEventKind.Hit,context);Assert.AreEqual(1,calls);
        }
    }
}
