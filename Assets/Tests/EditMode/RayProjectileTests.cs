using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class RayProjectileTests
    {
        static SkillProjectDocument Example()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
        static SkillSimulation Simulation(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static SkillGraphDocument Skill(SkillProjectDocument p,string id)=>p.Skills.First(s=>s.Id==id);
        static SkillNodeDocument Node(SkillProjectDocument p,string id,string kind)=>Skill(p,id).Nodes.First(n=>n.Kind==kind);
        static void Cue(SkillGraphDocument skill,string source,string port,int id)
        {
            string key="cue-"+port;
            skill.Nodes=skill.Nodes.Concat(new[]{new SkillNodeDocument{Id=key,Kind="cue",Parameters=JObject.FromObject(new{id})}}).ToArray();
            skill.Edges=skill.Edges.Concat(new[]{new SkillEdgeDocument{Id=key,Source=source,SourcePort=port,Target=key,TargetPort="execute"}}).ToArray();
        }
        static void TargetsInReverseOrder(SkillProjectDocument p)
        {
            p.Preview.Targets=p.Preview.Targets.Take(3).ToArray();
            p.Preview.Targets[0].X=8;p.Preview.Targets[0].Z=0;
            p.Preview.Targets[1].X=0;p.Preview.Targets[1].Z=0;
            p.Preview.Targets[2].X=4;p.Preview.Targets[2].Z=0;
        }

        [Test]
        public void FirstRayHitsNearestContactRatherThanFirstRegisteredEntity()
        {
            var p=Example();TargetsInReverseOrder(p);
            using(var s=Simulation(p))
            {
                s.Cast("ray-shot",0,0);s.Advance(10);
                Assert.AreEqual(80,s.Snapshot().Statistics.Damage);
                Assert.AreEqual(920,s.Snapshot().Actors.First(a=>a.Name=="target-2").Hp);
                Assert.AreEqual(1000,s.Snapshot().Actors.First(a=>a.Name=="target-1").Hp);
                Assert.AreEqual(-.15f,s.Snapshot().Rays.Single().ToX,.001f);
            }
        }

        [TestCase(0,240)]
        [TestCase(2,160)]
        public void PiercingRayRespectsItsExplicitTargetBudget(int maximum,int damage)
        {
            var p=Example();TargetsInReverseOrder(p);Node(p,"piercing-ray","delivery").Parameters["maxTargets"]=maximum;
            using(var s=Simulation(p))
            {s.Cast("piercing-ray",0,0);s.Advance(20);Assert.AreEqual(damage,s.Snapshot().Statistics.Damage);Assert.AreEqual(damage/80,s.Snapshot().Statistics.Hits);}
        }

        [Test]
        public void PiercingRayStopsAtTheWallAndOnlyThenDeliversWallAndComplete()
        {
            var p=Example();TargetsInReverseOrder(p);p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=6,Z=0,Width=.5f,Depth=4}};
            var skill=Skill(p,"piercing-ray");Cue(skill,"ray","wall",11);Cue(skill,"ray","complete",12);
            using(var s=Simulation(p))
            {
                s.Cast("piercing-ray",0,0);s.Advance(10);var snapshot=s.Snapshot();
                Assert.AreEqual(160,snapshot.Statistics.Damage);Assert.AreEqual(5.6f,snapshot.Rays.Single().ToX,.001f);
                var wall=snapshot.Events.Single(e=>e.Kind=="cue"&&e.Result=="11");var complete=snapshot.Events.Single(e=>e.Kind=="cue"&&e.Result=="12");
                Assert.Less(snapshot.Events.Last(e=>e.Kind=="damage").Sequence,wall.Sequence);Assert.Less(wall.Sequence,complete.Sequence);
            }
            Node(p,"piercing-ray","delivery").Parameters["pierce"]=false;
            using(var s=Simulation(p))
            {s.Cast("piercing-ray",0,0);s.Advance(10);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Kind=="cue"&&e.Result=="11"));}
        }

        [TestCase(.6f,0)]
        [TestCase(1f,80)]
        public void RayWidthParticipatesInTheRealHitQuery(float width,int damage)
        {
            var p=Example();p.Preview.Targets=p.Preview.Targets.Take(1).ToArray();p.Preview.Targets[0].Z=.4f;
            Node(p,"ray-shot","delivery").Parameters["width"]=width;
            using(var s=Simulation(p)){s.Cast("ray-shot",0,0);s.Advance(10);Assert.AreEqual(damage,s.Snapshot().Statistics.Damage);}
        }

        [Test]
        public void ChargedRayCarriesPowerAndAlwaysCompletesWithNoHit()
        {
            var p=Example();var cast=Node(p,"ray-shot","cast").Parameters;cast["mode"]="charge";cast["autoRelease"]=false;cast["energyPerSecond"]=0;
            Cue(Skill(p,"ray-shot"),"ray","complete",22);
            using(var s=Simulation(p))
            {s.Cast("ray-shot",0,0);s.Advance(30);s.Release();s.Advance(20);Assert.AreEqual(160,s.Snapshot().Statistics.Damage,.005f);}
            p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            using(var s=Simulation(p))
            {s.Cast("ray-shot",0,0);s.Advance(30);s.Release();s.Advance(20);Assert.AreEqual(0,s.Snapshot().Statistics.Damage);Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="cue"&&e.Result=="22"));}
        }

        [Test]
        public void FastProjectileCannotTunnelThroughTargetsOrAThinWall()
        {
            var p=Example();Node(p,"basic-shot","projectile").Parameters["speed"]=1000;Node(p,"basic-shot","damage").Parameters["critChance"]=0;
            using(var s=Simulation(p)){s.Cast("basic-shot",0,0);s.Advance(20);Assert.AreEqual(80,s.Snapshot().Statistics.Damage);Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="projectile-end"&&e.Result=="HitLimit"));}
            p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="thin",X=-2,Z=0,Width=.02f,Depth=4}};
            using(var s=Simulation(p))
            {s.Cast("basic-shot",0,0);s.Advance(50);Assert.AreEqual(0,s.Snapshot().Statistics.Damage);Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="projectile-end"&&e.Result=="Wall"));}
        }

        sealed class Capture : IEffect
        {
            public readonly List<EffectContext> Calls=new List<EffectContext>();
            public void Apply(ref EffectContext context)=>Calls.Add(context);
        }

        [TestCase(false,-3.6f,"Range")]
        [TestCase(true,-4.1f,"Lifetime")]
        public void ProjectileRangeAndLifetimeAreSeparateAndHaveOneTerminalContext(bool lifetime,float endpoint,string reason)
        {
            var p=Example();p.Preview.Targets=Array.Empty<PreviewActorDocument>();
            var parameters=Node(p,"basic-shot","projectile").Parameters;parameters["speed"]=10;parameters["range"]=lifetime?100:2;parameters["lifetime"]=lifetime ? .15f : 10;
            var content=CombatContentCompiler.Compile(p);content.Projectiles.TryGet(4001,out var definition);
            var range=new Capture();var life=new Capture();var end=new Capture();definition.OnRange=new IEffect[]{range};definition.OnExpire=new IEffect[]{life};definition.OnEnd=new IEffect[]{end};
            using(var s=new SkillSimulation(p,content))
            {
                s.Cast("basic-shot",0,0);s.Advance(100);Assert.AreEqual(1,end.Calls.Count);
                Assert.AreEqual(endpoint,end.Calls[0].Point.X,.001f);Assert.IsNull(end.Calls[0].Target);Assert.IsTrue(end.Calls[0].HasDir);
                Assert.AreEqual(lifetime?0:1,range.Calls.Count);Assert.AreEqual(lifetime?1:0,life.Calls.Count);
                Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="projectile-end"&&e.Result==reason));
            }
        }

        [Test]
        public void ProjectileWallReasonPrecedesGenericEndAndHitDamagePrecedesItsEnd()
        {
            var p=Example();var skill=Skill(p,"basic-shot");Cue(skill,"projectile","wall",11);Cue(skill,"projectile","expire",22);Cue(skill,"projectile","range",33);Cue(skill,"projectile","lifetime",44);
            p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=-2,Z=0,Width=.5f,Depth=4}};
            using(var s=Simulation(p))
            {
                s.Cast("basic-shot",0,0);s.Advance(100);var events=s.Snapshot().Events;
                Assert.AreEqual(1,events.Count(e=>e.Kind=="cue"&&e.Result=="11"));Assert.AreEqual(1,events.Count(e=>e.Kind=="cue"&&e.Result=="22"));
                Assert.IsFalse(events.Any(e=>e.Kind=="cue"&&(e.Result=="33"||e.Result=="44")));
                Assert.Less(events.First(e=>e.Kind=="cue"&&e.Result=="11").Sequence,events.First(e=>e.Kind=="cue"&&e.Result=="22").Sequence);
            }
            p.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();
            using(var s=Simulation(p))
            {
                s.Cast("basic-shot",0,0);s.Advance(100);var events=s.Snapshot().Events;
                Assert.Less(events.First(e=>e.Kind=="damage").Sequence,events.First(e=>e.Kind=="projectile-end").Sequence);
                Assert.Less(events.First(e=>e.Kind=="projectile-end").Sequence,events.First(e=>e.Kind=="cue"&&e.Result=="22").Sequence);
            }
        }

        [Test]
        public void MuzzleOffsetCannotSpawnTheProjectileAcrossANearWall()
        {
            var p=Example();p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="muzzle",X=-5.45f,Z=0,Width=.1f,Depth=3}};
            using(var s=Simulation(p)){s.Cast("basic-shot",0,0);s.Advance(30);Assert.AreEqual(0,s.Snapshot().Statistics.Damage);Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="projectile-end"&&e.Result=="Wall"));}
        }
    }
}
