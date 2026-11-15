using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class SummonRuntimeTests
    {
        static SkillProjectDocument Example()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
            p.Preview.Recovery=0;p.Skills.First(s=>s.Id=="basic-shot").Nodes.First(n=>n.Kind=="damage").Parameters["critChance"]=0;return p;
        }
        static SkillSimulation Simulation(SkillProjectDocument p)=>new SkillSimulation(p,CombatContentCompiler.Compile(p));
        static SkillNodeDocument Node(SkillProjectDocument p)=>p.Skills.First(s=>s.Id=="summon-gunners").Nodes.First(n=>n.Kind=="delivery");
        static SummonBlueprintDocument Blueprint(SkillProjectDocument p)=>p.SummonBlueprints.First(b=>b.Id=="echo-gunner");
        static void Cue(SkillProjectDocument p,string port,int id)
        {
            var skill=p.Skills.First(s=>s.Id=="summon-gunners");string key="cue-"+port;
            skill.Nodes=skill.Nodes.Concat(new[]{new SkillNodeDocument{Id=key,Kind="cue",Parameters=JObject.FromObject(new{id})}}).ToArray();
            skill.Edges=skill.Edges.Concat(new[]{new SkillEdgeDocument{Id=key,Source="summon",SourcePort=port,Target=key,TargetPort="execute"}}).ToArray();
        }

        [Test]
        public void BlueprintProducesIndependentBodiesWithOwnerAndRealAiDamage()
        {
            var p=Example();var blueprint=Blueprint(p);blueprint.Hp=123;blueprint.Defense=7;
            using(var s=Simulation(p))
            {
                s.Cast("summon-gunners",0,0);s.Advance(10);var pets=s.Snapshot().Actors.Where(a=>a.Kind=="summon").ToArray();
                Assert.AreEqual(2,pets.Length);Assert.AreEqual(-4.8f,pets[0].X,.001f);Assert.AreEqual(-7.2f,pets[1].X,.001f);
                foreach(var pet in pets){Assert.AreEqual(123,pet.MaxHp);Assert.AreEqual("caster",pet.Owner);Assert.AreEqual("echo-gunner",pet.Blueprint);Assert.AreEqual("basic-shot",pet.Skill);}
                s.Advance(70);Assert.Greater(s.Snapshot().Statistics.Damage,0);Assert.Greater(s.Snapshot().Statistics.Casts,1);
                Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="damage"&&e.Source.StartsWith("射击分身#")));
            }
        }

        [Test]
        public void AiUsesItsOwnEnergyBudgetInsteadOfBypassingTheCastGuard()
        {
            var p=Example();Node(p).Parameters["count"]=1;Node(p).Parameters["duration"]=3;
            var b=Blueprint(p);b.Energy=25;b.InitialEnergy=25;b.Recovery=0;
            using(var s=Simulation(p))
            {
                s.Cast("summon-gunners",0,0);s.Advance(100);
                Assert.AreEqual(3,s.Snapshot().Statistics.Casts,"One root cast and two paid summon casts.");
                Assert.AreEqual(2,s.Snapshot().Statistics.Projectiles);Assert.AreEqual(160,s.Snapshot().Statistics.Damage);
                Assert.AreEqual(5,s.Snapshot().Actors.Single(a=>a.Kind=="summon").Energy);
            }
        }

        [Test]
        public void ChargedGenerationPowerIsSnapshottedIntoTheChildAttack()
        {
            var p=Example();Node(p).Parameters["count"]=1;Node(p).Parameters["duration"]=3;
            var cast=p.Skills.First(s=>s.Id=="summon-gunners").Nodes.First(n=>n.Kind=="cast").Parameters;
            cast["mode"]="charge";cast["autoRelease"]=false;cast["energyPerSecond"]=0;
            var b=Blueprint(p);b.Energy=10;b.InitialEnergy=10;b.Recovery=0;
            using(var s=Simulation(p))
            {s.Cast("summon-gunners",0,0);s.Advance(30);s.Release();s.Advance(100);Assert.AreEqual(160,s.Snapshot().Statistics.Damage,.01f);Assert.AreEqual(1,s.Snapshot().Statistics.Projectiles);}
        }

        [TestCase("charged-shot",.6f,160)]
        [TestCase("held-spray",.2f,240)]
        public void AiCanHoldAndReleaseExistingChargeOrChannelSkills(string skill,float hold,int damage)
        {
            var p=Example();Node(p).Parameters["count"]=1;Node(p).Parameters["duration"]=3;
            var b=Blueprint(p);b.SkillId=skill;b.HoldDuration=hold;b.Recovery=0;
            var attack=p.Skills.First(s=>s.Id==skill);attack.Nodes.First(n=>n.Kind=="cast").Parameters["cooldown"]=10;
            attack.Nodes.First(n=>n.Kind=="damage").Parameters["critChance"]=0;
            using(var s=Simulation(p))
            {
                s.Cast("summon-gunners",0,0);s.Advance(120);
                Assert.AreEqual(damage,s.Snapshot().Statistics.Damage,.01f);
                Assert.IsTrue(s.Snapshot().Events.Any(e=>(e.Kind=="released"||e.Result=="Released")&&e.Source.StartsWith("射击分身#")));
            }
        }

        [Test]
        public void NaturalExpiryHasOrderedHooksAndStopsAllLaterAttacks()
        {
            var p=Example();p.Preview.Targets=Array.Empty<PreviewActorDocument>();Node(p).Parameters["count"]=1;Node(p).Parameters["duration"]=.2f;
            Cue(p,"apply",11);Cue(p,"expire",22);Cue(p,"removed",33);
            using(var s=Simulation(p))
            {
                s.Cast("summon-gunners",0,0);s.Advance(40);var events=s.Snapshot().Events;
                Assert.AreEqual(0,s.Snapshot().Actors.Count(a=>a.Kind=="summon"));Assert.AreEqual(0,s.Snapshot().Statistics.Projectiles);
                Assert.AreEqual(1,events.Count(e=>e.Kind=="cue"&&e.Result=="11"));
                Assert.AreEqual(1,events.Count(e=>e.Kind=="cue"&&e.Result=="22"));Assert.AreEqual(1,events.Count(e=>e.Kind=="cue"&&e.Result=="33"));
                Assert.Less(events.First(e=>e.Kind=="cue"&&e.Result=="22").Sequence,events.First(e=>e.Kind=="cue"&&e.Result=="33").Sequence);
            }
        }

        [Test]
        public void OwnerDeathAndManualRemovalCleanBodiesAndTheirPendingProjectiles()
        {
            var p=Example();Node(p).Parameters["duration"]=5;Cue(p,"expire",22);Cue(p,"removed",33);
            using(var s=Simulation(p))
            {
                s.Cast("summon-gunners",0,0);s.Advance(15);Assert.IsTrue(s.Snapshot().Actors.Any(a=>a.Kind=="projectile"));
                s.Interrupt(true);s.Advance(100);
                Assert.IsFalse(s.Snapshot().Actors.Any(a=>a.Kind=="summon"||a.Kind=="projectile"||a.Kind=="aoe"));
                Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Kind=="cue"&&e.Result=="22"));Assert.AreEqual(2,s.Snapshot().Events.Count(e=>e.Kind=="cue"&&e.Result=="33"));
            }
            using(var s=Simulation(p))
            {
                s.Cast("summon-gunners",0,0);s.Advance(15);string pet=s.Snapshot().Actors.First(a=>a.Kind=="summon").Id;s.Remove(pet);
                Assert.AreEqual(1,s.Snapshot().Actors.Count(a=>a.Kind=="summon"));s.Advance(1);
                Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="summon-removed"));
            }
        }

        [Test]
        public void GenerationChecksBlockedPositionsAndPerOwnerAliveQuota()
        {
            var p=Example();p.Preview.Targets=Array.Empty<PreviewActorDocument>();Node(p).Parameters["center"]="point";Node(p).Parameters["count"]=1;
            p.Preview.Obstacles=new[]{new PreviewObstacleDocument{Id="wall",X=-2,Z=0,Width=.5f,Depth=4}};
            using(var s=Simulation(p)){s.Cast("summon-gunners",-2,0);s.Advance(30);Assert.AreEqual(0,s.Snapshot().Statistics.Summons);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Kind=="summon-rejected"&&e.Result=="BlockedPosition"));}
            p.Preview.Obstacles=Array.Empty<PreviewObstacleDocument>();Node(p).Parameters["center"]="source";Node(p).Parameters["count"]=2;Node(p).Parameters["maxAlive"]=1;Node(p).Parameters["duration"]=5;
            p.Skills.First(s=>s.Id=="summon-gunners").Nodes.First(n=>n.Kind=="cast").Parameters["cooldown"]=0;
            using(var s=Simulation(p))
            {for(int i=0;i<3;i++){s.Cast("summon-gunners",0,0);s.Advance(20);}Assert.AreEqual(1,s.Snapshot().Statistics.Summons);Assert.AreEqual(70,s.Snapshot().Energy);Assert.IsTrue(s.Snapshot().Events.Any(e=>e.Result=="OwnerLimit"));}
        }

        [Test]
        public void FollowingWithoutTargetsMovesTowardTheRealOwner()
        {
            var p=Example();p.Preview.Targets=Array.Empty<PreviewActorDocument>();Node(p).Parameters["count"]=1;Node(p).Parameters["duration"]=5;Blueprint(p).MoveSpeed=5;
            using(var s=Simulation(p))
            {s.Cast("summon-gunners",0,0);s.Move(1,0);s.Advance(80);var pet=s.Snapshot().Actors.Single(a=>a.Kind=="summon");var owner=s.Snapshot().Actors.Single(a=>a.Kind=="caster");Assert.Greater(pet.X,-5);Assert.LessOrEqual(Math.Abs(pet.X-owner.X),2.25f);}
        }

        [Test]
        public void KilledSummonRunsRemovedButNotNaturalExpiryOrAnotherAiAttack()
        {
            var p=Example();Node(p).Parameters["count"]=1;Cue(p,"expire",22);Cue(p,"removed",33);var skill=p.Skills.First(s=>s.Id=="summon-gunners");
            skill.Nodes=skill.Nodes.Concat(new[]{new SkillNodeDocument{Id="kill",Kind="damage",Parameters=JObject.FromObject(new{flat=200,coefficient=0,critChance=0})}}).ToArray();
            skill.Edges=skill.Edges.Concat(new[]{new SkillEdgeDocument{Id="kill",Source="summon",SourcePort="apply",Target="kill",TargetPort="execute"}}).ToArray();
            using(var s=Simulation(p))
            {s.Cast("summon-gunners",0,0);s.Advance(80);Assert.AreEqual(0,s.Snapshot().Actors.Count(a=>a.Kind=="summon"));Assert.AreEqual(0,s.Snapshot().Statistics.Projectiles);Assert.AreEqual(1,s.Snapshot().Events.Count(e=>e.Kind=="cue"&&e.Result=="33"));Assert.IsFalse(s.Snapshot().Events.Any(e=>e.Kind=="cue"&&e.Result=="22"));}
        }
    }
}
