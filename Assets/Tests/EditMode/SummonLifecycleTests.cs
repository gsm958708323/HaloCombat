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
    public sealed class SummonLifecycleTests
    {
        sealed class CounterComp : Comp
        {public int Count;public override bool WantsTick=>true;public override void Tick(float dt)=>Count++;}
        sealed class Factory : IActorFactory
        {
            public Actor Create(in ActorSpawnSpec spec)
            {
                var a=new Actor();a.AddComp(new TransformComp());a.AddComp(new TagComp());a.AddComp(new TeamComp(1));
                if(spec.BlueprintId=="projectile"){a.AddComp(new ProjectileComp());return a;}
                if(spec.BlueprintId=="aoe"){a.AddComp(new AoeComp());return a;}
                var attributes=new AttributeSet();attributes.InitFighterDefaults();a.AddComp(attributes);a.AddComp(new HealthComp());a.AddComp(new StateMachineComp());a.AddComp(new LocomotionComp());
                if(spec.BlueprintId=="pet"){a.AddComp(new SummonComp());a.AddComp(new CounterComp());}
                a.AddComp(new SkillDirectorComp(new TimelineLibrary()));return a;
            }
            public void Release(Actor a)=>a.ResetForPool();
        }
        static Actor Spawn(CombatWorld world,string key)
        {var id=world.SpawnActor(new ActorSpawnSpec(key));world.TryGetActor(id,out var actor);return actor;}

        [Test]
        public void ExpiredBodyDoesNotTickLaterComponentsInTheSameFrame()
        {
            var world=new CombatWorld(new Factory());var owner=Spawn(world,"owner");var pet=Spawn(world,"pet");
            var counter=pet.GetComp<CounterComp>();pet.GetComp<SummonComp>().Setup(owner.Id,new SummonDefinition{SpecId=1,Lifetime=.02f});
            world.Tick(.02f);Assert.AreEqual(0,counter.Count);Assert.IsFalse(world.TryGetActor(pet.Id,out _));
        }

        [TestCase(.2f,10)]
        [TestCase(3f,150)]
        [TestCase(5f,250)]
        public void FixedStepLifetimeDoesNotDriftPastItsScheduledEnd(float lifetime,int steps)
        {
            var world=new CombatWorld(new Factory());var owner=Spawn(world,"owner");var pet=Spawn(world,"pet");
            pet.GetComp<SummonComp>().Setup(owner.Id,new SummonDefinition{SpecId=1,Lifetime=lifetime});
            for(int i=0;i<steps-1;i++)world.Tick(.02f);Assert.IsTrue(pet.IsActive);
            world.Tick(.02f);Assert.IsFalse(pet.IsActive);Assert.IsFalse(world.TryGetActor(pet.Id,out _));
        }

        [Test]
        public void StaleOwnerIdentityCannotBindToAReplacementAtTheSameIndex()
        {
            var world=new CombatWorld(new Factory());var old=Spawn(world,"owner").Id;world.RequestDespawn(old);world.Tick(0);
            var owner=Spawn(world,"owner");Assert.AreEqual(old.Index,owner.Id.Index);Assert.AreNotEqual(old.Generation,owner.Id.Generation);
            var pet=Spawn(world,"pet");pet.GetComp<SummonComp>().Setup(old,new SummonDefinition{SpecId=1,Lifetime=1});
            world.Tick(.02f);Assert.IsFalse(pet.IsActive);Assert.IsTrue(owner.IsActive);
        }

        [Test]
        public void PendingSpawnsCannotOutliveTheirRemovedOwner()
        {
            var world=new CombatWorld(new Factory());var owner=Spawn(world,"owner");
            var projectiles=new ProjectileCatalog();projectiles.Register(new ProjectileDefinition{SpecId=1});world.ReplaceCatalogs(projectiles,null,null);
            world.Intents.Post(new SpawnProjectileIntent(owner.Id,1,SimVec3.Zero,0,1));
            world.RequestDespawn(owner.Id);world.Tick(.02f);
            Assert.IsFalse(world.RegistryActive().Any(a=>a.TryGetComp<ProjectileComp>(out _)));
        }

        [Test]
        public void OwnerRemovalCleansMultipleGenerationsAndTheirRuntimeBodiesImmediately()
        {
            var world=new CombatWorld(new Factory());var owner=Spawn(world,"owner");var parent=Spawn(world,"pet");var child=Spawn(world,"pet");int removed=0;
            var definition=new SummonDefinition{SpecId=1,Lifetime=10,OnRemoved=new IEffect[]{new CallbackEffect(()=>removed++)}};
            parent.GetComp<SummonComp>().Setup(owner.Id,definition);child.GetComp<SummonComp>().Setup(parent.Id,definition);
            var projectile=Spawn(world,"projectile");projectile.GetComp<ProjectileComp>().Setup(new ProjectileDefinition{SpecId=2},child.Id,1);
            var area=Spawn(world,"aoe");area.GetComp<AoeComp>().Setup(new AoeDefinition{SpecId=3,Duration=10},child.Id,1,0);
            world.RequestDespawn(owner.Id);Assert.AreEqual(0,world.RegistryActive().Count);Assert.AreEqual(2,removed);
            world.Tick(0);Assert.IsFalse(world.TryGetActor(child.Id,out _));Assert.IsFalse(world.TryGetActor(projectile.Id,out _));
        }

        sealed class RemoveAgain : IEffect
        {public int Count;public void Apply(ref EffectContext context){Count++;context.World.RequestDespawn(context.Target.Id);}}
        [Test]
        public void RemovalIsReentrantAndMalformedOwnerCyclesCannotLoopCleanupForever()
        {
            var world=new CombatWorld(new Factory());var first=Spawn(world,"pet");var second=Spawn(world,"pet");var callback=new RemoveAgain();
            var definition=new SummonDefinition{SpecId=1,Lifetime=10,OnRemoved=new IEffect[]{callback}};
            first.GetComp<SummonComp>().Setup(second.Id,definition);second.GetComp<SummonComp>().Setup(first.Id,definition);
            world.RequestDespawn(first.Id);world.Tick(0);Assert.AreEqual(2,callback.Count);Assert.AreEqual(0,world.RegistryActive().Count);
        }

        [Test]
        public void WorldAliveLimitBoundsSeveralDifferentSummonNodes()
        {
            var project=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json")));
            project.Preview.Targets=Array.Empty<PreviewActorDocument>();var skill=project.Skills.First(s=>s.Id=="summon-gunners");var prototype=skill.Nodes.First(n=>n.Kind=="delivery");
            var nodes=skill.Nodes.Where(n=>n.Kind!="delivery").ToList();var edges=skill.Edges.Where(e=>e.Source=="target").ToList();
            for(int i=0;i<9;i++)
            {
                var node=JObject.FromObject(prototype).ToObject<SkillNodeDocument>();node.Id="summon-"+i;node.RuntimeId=9000+i;
                node.Parameters["count"]=16;node.Parameters["maxAlive"]=64;node.Parameters["duration"]=10;node.Parameters["spacing"]=0;
                nodes.Add(node);edges.Add(new SkillEdgeDocument{Id=node.Id,Source="cast",SourcePort="cast",Target=node.Id,TargetPort="execute",Order=i});
            }
            skill.Nodes=nodes.ToArray();skill.Edges=edges.ToArray();
            using(var simulation=new SkillSimulation(project,CombatContentCompiler.Compile(project)))
            {
                simulation.Cast("summon-gunners",0,0);simulation.Advance(10);
                Assert.AreEqual(128,simulation.Snapshot().Actors.Count(a=>a.Kind=="summon"));Assert.AreEqual(128,simulation.Snapshot().Statistics.Summons);
                Assert.IsTrue(simulation.Snapshot().Events.Any(e=>e.Kind=="summon-rejected"&&e.Result=="WorldLimit"));
            }
        }
    }
}
