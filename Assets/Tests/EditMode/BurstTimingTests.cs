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
    public sealed class BurstTimingTests
    {
        static SkillProjectDocument Project(bool burst=true,int count=20,string target="direction")
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
            var s=p.Skills.Single(v=>v.Id=="arena-ammo-shot");s.Timeline=null;
            s.Nodes=s.Nodes.Where(n=>n.Kind=="target"||n.Kind=="cast"||n.Kind=="projectile-ref").ToArray();
            s.Nodes.Single(n=>n.Kind=="target").Parameters=JObject.FromObject(new{mode=target,range=1000});
            s.Nodes.Single(n=>n.Kind=="cast").Parameters=JObject.FromObject(new{mode="instant",burst,count,interval=.04f,delay=.02f,duration=.9f,cost=0,cooldown=0});
            s.Edges=s.Edges.Where(e=>e.Source=="target").Concat(new[]{new SkillEdgeDocument{Id="cast-projectile",Source="cast",SourcePort="cast",Target="projectile",TargetPort="execute"}}).ToArray();
            p.Runtime.Actors.Single(a=>a.Id=="arena-player").Recovery=0;return p;
        }
        sealed class Sequence:IRandom
        {
            public int Count;
            public float Next01(){float[] values={.05f,.35f,.65f,.95f};return values[Count++%values.Length];}
        }
        sealed class F:IDisposable
        {
            public readonly CombatWorld World;
            public readonly Actor Player;
            public readonly Sequence Random=new Sequence();
            public readonly List<(int Frame,float Yaw,EntityId Target)> Shots=new List<(int,float,EntityId)>();
            public F(SkillProjectDocument p)
            {
                var c=CombatContentCompiler.Compile(p);World=new CombatWorld(c.Runtime.CreateFactory(c.Skills),random:Random);c.Runtime.Data.Install(World);
                World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-player")),out Player);
                World.Events.Subscribe<EvEntitySpawn>(e=>{if(e.BlueprintId!="projectile")return;World.TryGetActor(e.Id,out var a);Shots.Add((World.Time.Frame,a.GetComp<TransformComp>().YawDegrees,a.GetComp<ProjectileComp>().HomingTarget));});
            }
            public bool Cast()=>Player.GetComp<SkillCastComp>().TryCast(new SkillNodeId(2001),new SkillAim(EntityId.Invalid,new SimVec3(100,0,0),0));
            public Actor Target(float x,float z=0){World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-target")),out var a);a.GetComp<TransformComp>().Position=new SimVec3(x,0,z);return a;}
            public void Tick(int n=1){for(int i=0;i<n;i++)World.Tick(.02f);}
            public void Dispose()=>World.Shutdown();
        }
        [Test]
        public void TwentyShotBurstDrawsOnceForEachActualEmissionAtItsAuthoredStep()
        {
            using(var f=new F(Project()))
            {
                Assert.IsTrue(f.Cast());Assert.AreEqual(0,f.Random.Count,"Validation must not consume a future random decision.");f.Tick(45);
                Assert.AreEqual(20,f.Shots.Count);Assert.AreEqual(20,f.Random.Count);Assert.AreEqual(4,f.Shots.Select(s=>s.Yaw).Distinct().Count());
                CollectionAssert.AreEqual(Enumerable.Range(0,20).Select(i=>1+i*2),f.Shots.Select(s=>s.Frame));
            }
        }
        [Test]
        public void DisabledBurstKeepsItsAuthoredCountButRunsOnlyOneEmissionAndRoundtrips()
        {
            var p=Project(false);string json=SkillProjectJson.Write(p);var roundtrip=SkillProjectJson.Read(json);
            Assert.AreEqual(20,roundtrip.Skills.Single(s=>s.Id=="arena-ammo-shot").Nodes.Single(n=>n.Kind=="cast").Parameters.Value<int>("count"));
            using(var f=new F(roundtrip)){Assert.IsTrue(f.Cast());f.Tick(45);Assert.AreEqual(1,f.Shots.Count);Assert.AreEqual(1,f.Random.Count);}
        }
        [Test]
        public void UnpaidBurstSegmentsDoNotDrawAnotherRandomDirectionOrSpendPartialResources()
        {
            var p=Project();var cast=p.Skills.Single(s=>s.Id=="arena-ammo-shot").Nodes.Single(n=>n.Kind=="cast");cast.Parameters["cost"]=10;cast.Parameters["costPerPulse"]=true;
            using(var f=new F(p))
            {
                f.Player.GetComp<EnergyComp>().TrySpend(75);Assert.IsTrue(f.Cast());f.Tick(45);Assert.AreEqual(2,f.Shots.Count);Assert.AreEqual(2,f.Random.Count);
                Assert.AreEqual(5,f.Player.GetComp<EnergyComp>().Current);Assert.IsFalse(f.Player.GetComp<SkillDirectorComp>().IsPlaying);
            }
        }
        [Test]
        public void RandomTargetIsReselectedAndLostTargetStopsWithoutAnotherDraw()
        {
            using(var f=new F(Project(true,3,"random")))
            {
                var a=f.Target(100);var b=f.Target(0,100);var c=f.Target(-100);Assert.IsTrue(f.Cast());Assert.AreEqual(0,f.Random.Count);f.Tick();
                Assert.AreEqual(a.Id,f.Shots[0].Target);f.World.RequestDespawn(a.Id);f.Tick(2);Assert.AreNotEqual(a.Id,f.Shots[1].Target);
                f.World.RequestDespawn(b.Id);f.World.RequestDespawn(c.Id);f.Tick(4);Assert.AreEqual(2,f.Shots.Count);Assert.AreEqual(2,f.Random.Count);Assert.IsFalse(f.Player.GetComp<SkillDirectorComp>().IsPlaying);
            }
        }
        [Test]
        public void InterruptingTheBurstClearsFutureEmissionsWithoutConsumingFutureRandomState()
        {
            using(var f=new F(Project()))
            {Assert.IsTrue(f.Cast());f.Tick(5);int count=f.Shots.Count;f.Player.GetComp<StateMachineComp>().TryEnter(ActivityId.Hit,new ActivityEnterArgs{HitDuration=.3f});f.Tick(80);Assert.AreEqual(count,f.Shots.Count);Assert.AreEqual(count,f.Random.Count);}
        }
        [Test]
        public void ExistingCountOnlyDefinitionsKeepTheirScheduleWithoutAnExplicitBurstOverride()
        {
            var p=Project();p.Skills.Single(s=>s.Id=="arena-ammo-shot").Nodes.Single(n=>n.Kind=="cast").Parameters.Remove("burst");
            using(var f=new F(p)){Assert.IsTrue(f.Cast());f.Tick(45);Assert.AreEqual(20,f.Shots.Count);}
        }
        [Test]
        public void TheDeliveredDeathShotTemplateRunsTwentyDirectionsAndRoundtripsItsBurstSwitch()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
            var selected=SkillProjectJson.SelectTemplate(p,"death-shot");Assert.AreEqual(1,selected.Skills.Length);
            var skill=selected.Skills.Single();Assert.AreEqual("direction",skill.Nodes.Single(n=>n.Kind=="target").Parameters.Value<string>("mode"));
            var cast=skill.Nodes.Single(n=>n.Kind=="cast");Assert.IsTrue(cast.Parameters.Value<bool>("burst"));Assert.AreEqual(20,cast.Parameters.Value<int>("count"));
            selected.Preview.Targets=Array.Empty<PreviewActorDocument>();using(var s=new SkillSimulation(selected,CombatContentCompiler.Compile(selected)))
            {s.Execute(new SkillSimulationCommand{Kind="cast",SkillId=skill.Id,X=0,Z=0});s.Advance(45);Assert.AreEqual(20,s.Snapshot().Statistics.Projectiles);using(var replay=new SkillReplayRun(s.CaptureReplay(skill.Id)))Assert.IsTrue(replay.Comparison.Matches);}
        }
        [TestCase("not-bool")] [TestCase("invalid-count")] [TestCase("explicit-timeline")]
        public void BurstOverridesAreStrictlyValidated(string fault)
        {
            var p=Project();var s=p.Skills.Single(v=>v.Id=="arena-ammo-shot");var cast=s.Nodes.Single(n=>n.Kind=="cast");
            if(fault=="not-bool")cast.Parameters["burst"]="false";
            if(fault=="invalid-count")cast.Parameters["count"]=1.5;
            if(fault=="explicit-timeline")s.Timeline=new TimelineDocument{RuntimeId=99001,Duration=.2f};
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
    }
}
