using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class BuffLifecycleTests
    {
        sealed class Factory : IActorFactory
        {
            public Actor Create(in ActorSpawnSpec spec)
            {
                var a=new Actor();a.AddComp(new TransformComp());a.AddComp(new TagComp());a.AddComp(new TeamComp(spec.BlueprintId=="source"?1:2));
                var attr=new AttributeSet();attr.InitFighterDefaults();a.AddComp(attr);a.AddComp(new BuffComp());a.AddComp(new HealthComp());
                a.AddComp(new StateMachineComp());a.AddComp(new LocomotionComp());a.AddComp(new SkillDirectorComp(new TimelineLibrary()));return a;
            }
            public void Release(Actor a)=>a.ResetForPool();
        }
        sealed class Capture : IEffect
        {
            public readonly List<EffectContext> Calls=new List<EffectContext>();
            public void Apply(ref EffectContext context)=>Calls.Add(context);
        }
        static Actor Spawn(CombatWorld world,string name="target")
        {var id=world.SpawnActor(new ActorSpawnSpec(name));world.TryGetActor(id,out var actor);return actor;}
        static DurationSpec Definition(StackPolicy policy=StackPolicy.AddStack)=>new DurationSpec
        {BuffId=1,Duration=1.1f,TickInterval=.25f,MaxStacks=3,Stack=policy,GrantedTags=new[]{new TagId(3101)},Modifiers=new[]{new Modifier{Attr=AttrId.MoveSpeed,Op=ModOp.Mul,Value=.5f}}};

        [TestCase(StackPolicy.AddStack,1,3)]
        [TestCase(StackPolicy.RefreshDuration,1,1)]
        [TestCase(StackPolicy.Independent,4,4)]
        [TestCase(StackPolicy.RejectIfExists,1,1)]
        public void PoliciesHaveSeparateStackAndModifierInstanceSemantics(StackPolicy policy,int count,int stacks)
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);var buffs=target.GetComp<BuffComp>();var spec=Definition(policy);
            for(int i=0;i<4;i++)buffs.Apply(spec,target);
            Assert.AreEqual(count,buffs.Count);Assert.AreEqual(stacks,buffs.StacksOf(1));Assert.AreEqual(count,target.GetComp<AttributeSet>().ModCount);
            Assert.AreEqual(5*Math.Pow(.5,count),target.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed),.00001f);
            Assert.AreEqual(count,target.GetComp<TagComp>().Stack(new TagId(3101)));
            buffs.ClearAll(BuffRemovalReason.Manual);Assert.AreEqual(0,target.GetComp<AttributeSet>().ModCount);Assert.AreEqual(5,target.GetComp<AttributeSet>().GetFinal(AttrId.MoveSpeed));
        }

        [Test]
        public void BirthFrameDoesNotTickAndLargeStepsCatchUpOnlyInsideTheLifetime()
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);int periods=0;var spec=Definition();spec.OnPeriod=new IEffect[]{new CallbackEffect(()=>periods++)};
            bool applied=false;world.AddServicePhase(()=>{if(!applied){applied=true;target.GetComp<BuffComp>().Apply(spec,target);}});
            world.Tick(.5f);Assert.AreEqual(0,periods);Assert.AreEqual(1.1f,target.GetComp<BuffComp>().Snapshot()[0].Remaining.Value,.00001f);
            world.Tick(1.5f);Assert.AreEqual(4,periods);Assert.AreEqual(0,target.GetComp<BuffComp>().Count);
        }

        [Test]
        public void TinyPeriodsAndHitstopUseBuffLogicalAgeRatherThanAdvancingWorldClock()
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);int periods=0;var spec=Definition();spec.Duration=.1f;spec.TickInterval=.001f;spec.OnPeriod=new IEffect[]{new CallbackEffect(()=>periods++)};
            target.GetComp<BuffComp>().Apply(spec,target);world.RequestHitstop(10);
            for(int i=0;i<10;i++)world.Tick(.02f);
            Assert.AreEqual(0,periods);Assert.AreEqual(.1f,target.GetComp<BuffComp>().Snapshot()[0].Remaining.Value,.00001f);
            for(int i=0;i<5;i++)world.Tick(.02f);
            Assert.AreEqual(99,periods);Assert.AreEqual(0,target.GetComp<BuffComp>().Count);
        }

        [Test]
        public void RefreshKeepsItsPeriodPhaseAndDoesNotFireAStackAtTheCap()
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);var buffs=target.GetComp<BuffComp>();var spec=Definition();int periods=0,stacked=0;
            spec.OnPeriod=new IEffect[]{new CallbackEffect(()=>periods++)};spec.OnStack=new IEffect[]{new CallbackEffect(()=>stacked++)};
            buffs.Apply(spec,target);world.Tick(.2f);buffs.Apply(spec,target,10);buffs.Apply(spec,target);
            Assert.AreEqual(1,stacked);Assert.AreEqual(3,buffs.StacksOf(1));world.Tick(.05f);Assert.AreEqual(1,periods);
            Assert.Greater(buffs.Snapshot()[0].Remaining.Value,1);
        }

        [Test]
        public void MutexDispelAndExpiryRemoveStateBeforeOnlyTheirOwnCallbacks()
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);var buffs=target.GetComp<BuffComp>();var first=Definition();first.MutexGroup=7;
            int expired=0,removed=0;first.OnExpire=new IEffect[]{new CallbackEffect(()=>expired++)};
            first.OnRemoved=new IEffect[]{new CallbackEffect(()=>{removed++;Assert.AreEqual(0,buffs.Count);Assert.AreEqual(0,target.GetComp<TagComp>().Stack(new TagId(3101)));Assert.AreEqual(0,target.GetComp<AttributeSet>().ModCount);})};
            buffs.Apply(first,target);buffs.Apply(new DurationSpec{BuffId=2,Duration=1,MutexGroup=7},target);
            Assert.AreEqual(0,expired);Assert.AreEqual(1,removed);Assert.AreEqual(0,buffs.StacksOf(1));
            buffs.Dispel(DispelMode.ByMutexGroup,7,default,0);buffs.Apply(first,target);world.Tick(2);
            Assert.AreEqual(1,expired);Assert.AreEqual(2,removed);
        }

        [Test]
        public void LeasedTagsCannotBeStolenOrReduceOtherBuffOrStateContributions()
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);var tags=target.GetComp<TagComp>();var buffs=target.GetComp<BuffComp>();var tag=new TagId(3101);
            tags.Add(tag,1,TagSource.Debug);buffs.Apply(Definition(StackPolicy.Independent),target);buffs.Apply(Definition(StackPolicy.Independent),target);
            tags.Remove(tag,100,TagSource.Debug);Assert.AreEqual(2,tags.Stack(tag));
            buffs.RemoveInstance(buffs.Snapshot()[0].InstanceId);Assert.AreEqual(1,tags.Stack(tag));
            tags.Add(tag,1,TagSource.Debug);buffs.ClearAll(BuffRemovalReason.Manual);Assert.AreEqual(1,tags.Stack(tag));
            var old=tags.Acquire(tag);tags.ClearAll();var fresh=tags.Acquire(tag);old.Dispose();Assert.AreEqual(1,tags.Stack(tag));fresh.Dispose();Assert.IsFalse(tags.Has(tag));
        }

        [Test]
        public void RemovedSourceUsesNoActorButKeepsSnapshotAttackAndPower()
        {
            var world=new CombatWorld(new Factory());var source=Spawn(world,"source");var target=Spawn(world);var capture=new Capture();var spec=Definition();
            spec.OnPeriod=new IEffect[]{capture,new DamageEffect{Coeff=1,CanCrit=false,FireOnHurted=false}};
            target.GetComp<BuffComp>().Apply(spec,source,snapshotAttack:10,power:2);var old=source.Id;source.GetComp<AttributeSet>().SetBase(AttrId.Atk,1000);
            world.RequestDespawn(source.Id);world.Tick(0);var replacement=Spawn(world,"source");Assert.AreEqual(old.Index,replacement.Id.Index);Assert.AreNotEqual(old.Generation,replacement.Id.Generation);
            world.Tick(.3f);Assert.AreEqual(80,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));Assert.IsNull(capture.Calls[0].Source);Assert.AreEqual(10,capture.Calls[0].SnapshotAtk);Assert.AreEqual(2,capture.Calls[0].Power);
        }

        [Test]
        public void DispelSourceUsesFullEntityIdentityAndFilterLimitsCountInstances()
        {
            var world=new CombatWorld(new Factory());var first=Spawn(world,"source");var second=Spawn(world,"source");var target=Spawn(world);var buffs=target.GetComp<BuffComp>();var spec=Definition(StackPolicy.Independent);
            buffs.Apply(spec,first);buffs.Apply(spec,second);buffs.Apply(spec,second);
            Assert.AreEqual(1,buffs.Dispel(DispelMode.BySource,0,default,1,second.Id));Assert.AreEqual(2,buffs.Count);
            var old=first.Id;world.RequestDespawn(old);world.Tick(0);var replacement=Spawn(world,"source");Assert.AreEqual(0,buffs.Dispel(DispelMode.BySource,0,default,0,replacement.Id));
            Assert.AreEqual(1,buffs.Dispel(DispelMode.BySource,0,default,0,old));Assert.AreEqual(1,buffs.Count);
            Assert.AreEqual(1,buffs.Dispel(DispelMode.ByTag,0,new TagId(3101),0));
        }

        [Test]
        public void CallbacksCanRemoveAndClearWithoutInvalidatingOrReenteringTheSnapshot()
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);var buffs=target.GetComp<BuffComp>();int calls=0;
            var first=Definition(StackPolicy.Independent);first.OnPeriod=new IEffect[]{new CallbackEffect(()=>{calls++;buffs.ClearAll(BuffRemovalReason.Manual);})};
            first.OnRemoved=new IEffect[]{new CallbackEffect(()=>buffs.ClearAll(BuffRemovalReason.Manual))};
            buffs.Apply(first,target);buffs.Apply(first,target);world.Tick(.3f);Assert.AreEqual(1,calls);Assert.AreEqual(0,buffs.Count);
            var respawn=Definition();respawn.OnRemoved=new IEffect[]{new CallbackEffect(()=>Assert.IsFalse(buffs.Apply(respawn,target)))};buffs.Apply(respawn,target);buffs.ClearAll(BuffRemovalReason.Manual);Assert.AreEqual(0,buffs.Count);
        }

        [Test]
        public void HurtAndCastHaveExplicitSourcesAndSelfDamageHooksCannotLoopForever()
        {
            var world=new CombatWorld(new Factory());var source=Spawn(world,"source");var target=Spawn(world);var hurt=new Capture();var cast=new Capture();var spec=Definition();
            spec.OnHurted=new IEffect[]{hurt,new DamageEffect{Flat=1,Coeff=0,CanCrit=false}};spec.OnOwnerCast=new IEffect[]{cast};
            target.GetComp<BuffComp>().Apply(spec,source);world.Deliver(new IEffect[]{new DamageEffect{Flat=10,Coeff=0,CanCrit=false}},source,target,10);Assert.AreEqual(0,hurt.Calls.Count);
            world.Tick(0);world.Deliver(new IEffect[]{new DamageEffect{Flat=10,Coeff=0,CanCrit=false}},source,target,10);
            Assert.AreEqual(1,hurt.Calls.Count);Assert.AreSame(source,hurt.Calls[0].Source);Assert.AreSame(target,hurt.Calls[0].Target);Assert.AreEqual(79,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            target.GetComp<BuffComp>().DispatchOnOwnerCast();Assert.AreSame(target,cast.Calls[0].Source);Assert.AreSame(target,cast.Calls[0].Target);
        }

        [Test]
        public void DeathAndDespawnReleaseAllStateWithoutNaturalExpiry()
        {
            var world=new CombatWorld(new Factory());var target=Spawn(world);int expired=0,removed=0;var spec=Definition();spec.OnExpire=new IEffect[]{new CallbackEffect(()=>expired++)};spec.OnRemoved=new IEffect[]{new CallbackEffect(()=>removed++)};
            target.GetComp<BuffComp>().Apply(spec,target);target.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead,new ActivityEnterArgs{Reason="test"});
            Assert.AreEqual(0,expired);Assert.AreEqual(1,removed);Assert.AreEqual(0,target.GetComp<AttributeSet>().ModCount);Assert.IsFalse(target.GetComp<TagComp>().Has(new TagId(3101)));
            var other=Spawn(world);other.GetComp<BuffComp>().Apply(spec,other);world.RequestDespawn(other.Id);world.Tick(0);Assert.AreEqual(1,removed,"Unload is silent.");
        }
    }
}
