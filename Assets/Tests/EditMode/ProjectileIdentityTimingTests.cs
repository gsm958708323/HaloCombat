using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class ProjectileIdentityTimingTests
    {
        sealed class Factory : IActorFactory
        {
            public Actor Create(in ActorSpawnSpec spec)
            {
                var actor=new Actor();actor.AddComp(new TransformComp());actor.AddComp(new TagComp());actor.AddComp(new TeamComp(spec.BlueprintId=="owner"?1:2));
                if(spec.BlueprintId=="projectile"){actor.AddComp(new ProjectileComp());return actor;}
                var attributes=new AttributeSet();attributes.InitFighterDefaults();attributes.SetBase(AttrId.MaxHp,1000);attributes.SetBase(AttrId.Hp,1000);attributes.SetBase(AttrId.Def,0);
                actor.AddComp(attributes);return actor;
            }
            public void Release(Actor actor)=>actor.ResetForPool();
        }
        static EntityId Spawn(CombatWorld world,string type,float x,float z=0)
        {
            var id=world.SpawnActor(new ActorSpawnSpec(type));world.TryGetActor(id,out var actor);actor.GetComp<TransformComp>().Position=new SimVec3(x,0,z);return id;
        }
        static CombatWorld World(ProjectileDefinition definition)
        {
            var world=new CombatWorld(new Factory());var catalog=new ProjectileCatalog();catalog.Register(definition);world.ReplaceCatalogs(catalog,null,null);return world;
        }

        [Test]
        public void OneLargeStepConsumesItsRemainingTimeAfterEachRedirect()
        {
            var def=new ProjectileDefinition{SpecId=1,SpawnForward=0,Speed=10,Lifetime=10,Range=100,HitRadius=.1f,
                Chain=new ProjectileChainDefinition{ExtraChains=1,Radius=10},OnHit=new IEffect[]{new DamageEffect{Flat=10,Coeff=0,FireOnHurted=false}}};
            foreach(int subdivisions in new[]{1,50})
            {
                var world=World(def);var owner=Spawn(world,"owner",0);Spawn(world,"target",2);var next=Spawn(world,"target",8);
                int chains=0;world.Events.Subscribe<EvProjectileBehavior>(e=>{if(e.Kind=="chain")chains++;});
                world.Intents.Post(new SpawnProjectileIntent(owner,1,default,0,0));
                for(int i=0;i<subdivisions;i++)world.Tick(1f/subdivisions);
                world.TryGetActor(next,out var actor);Assert.AreEqual(990,actor.GetComp<AttributeSet>().GetBase(AttrId.Hp));Assert.AreEqual(1,chains);
                Assert.IsFalse(world.RegistryActive().Any(a=>a.TryGetComp<ProjectileComp>(out _)));
            }
        }

        [Test]
        public void SameEntityIndexWithANewGenerationCanBeHitWhileAnOldIdentityCannot()
        {
            var def=new ProjectileDefinition{SpecId=1,SpawnForward=0,Speed=1,Lifetime=20,Range=20,HitRadius=.1f,MaxHits=0,
                OnHit=new IEffect[]{new DamageEffect{Flat=10,Coeff=0,FireOnHurted=false}}};
            var world=World(def);var owner=Spawn(world,"owner",0);var old=Spawn(world,"target",1);
            var victims=new List<EntityId>();world.Events.Subscribe<EvDamage>(e=>victims.Add(e.Target));
            world.Intents.Post(new SpawnProjectileIntent(owner,1,default,0,0));world.Tick(1);
            world.RequestDespawn(old);world.Tick(0);var replacement=Spawn(world,"target",2);
            Assert.AreEqual(old.Index,replacement.Index);Assert.AreNotEqual(old.Generation,replacement.Generation);
            world.Tick(1);world.Tick(0);CollectionAssert.AreEqual(new[]{old,replacement},victims);
        }

        [Test]
        public void OnHitDamageCanRemoveTheNextCandidateBeforeChainSearch()
        {
            var def=new ProjectileDefinition{SpecId=1,SpawnForward=0,Speed=10,Lifetime=10,Range=100,HitRadius=.1f,
                Chain=new ProjectileChainDefinition{ExtraChains=2,Radius=10}};
            var world=World(def);var owner=Spawn(world,"owner",0);Spawn(world,"target",2);var victim=Spawn(world,"target",4);
            def.OnHit=new IEffect[]{new CallbackEffect(()=>world.RequestDespawn(victim)),new DamageEffect{Flat=10,Coeff=0,FireOnHurted=false}};
            var ends=new List<ProjectileEndReason>();world.Events.Subscribe<EvProjectileEnded>(e=>ends.Add(e.Reason));
            world.Intents.Post(new SpawnProjectileIntent(owner,1,default,0,0));world.Tick(1);
            CollectionAssert.AreEqual(new[]{ProjectileEndReason.ChainExhausted},ends);
        }
    }
}
