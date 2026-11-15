using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class ActorHitstopTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly RuntimeProjectLoader Content=new RuntimeProjectLoader(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
            public readonly CombatWorld World;
            public readonly Actor Source,Target,Other;
            public Fixture(){World=Content.CreateWorld(new FixedRandom(.9f));Source=Spawn("arena-player",0);Target=Spawn("stake",4);Other=Spawn("fighter",-10);}
            public Actor Spawn(string blueprint,float x){World.TryGetActor(World.SpawnActor(new ActorSpawnSpec(blueprint)),out var a);a.GetComp<TransformComp>().Position=new SimVec3(x,0,0);return a;}
            public void Tick(int count=1){for(int i=0;i<count;i++)World.Tick(.02f);}
            public void Hit(int source,int target,float amount=10)
            {World.Deliver(new IEffect[]{new DamageEffect{Coeff=0,Flat=amount,CanCrit=false,SourceHitstopFrames=source,TargetHitstopFrames=target,Feedback=HitFeedbackLevel.Heavy}},Source,Target,0);}
            public void Dispose()=>World.Shutdown();
        }
        [Test]
        public void RequestsLatchTogetherAtTheNextStepAndFreezeOnlyTheirOwnMovement()
        {
            using(var f=new Fixture())
            {
                foreach(var a in new[]{f.Source,f.Target,f.Other})a.GetComp<LocomotionComp>().RequestMoveIntent(0,1);
                f.Hit(2,4);Assert.IsFalse(f.Source.Time.IsStopped);Assert.IsFalse(f.Target.Time.IsStopped);Assert.IsFalse(f.World.InHitstop);
                f.Tick(2);Assert.IsTrue(f.Source.Time.IsStopped);Assert.IsTrue(f.Target.Time.IsStopped);Assert.AreEqual(0,f.Source.GetComp<TransformComp>().Position.Z);Assert.Greater(f.Other.GetComp<TransformComp>().Position.Z,0);
                f.Tick();Assert.IsFalse(f.Source.Time.IsStopped);Assert.Greater(f.Source.GetComp<TransformComp>().Position.Z,0);Assert.IsTrue(f.Target.Time.IsStopped);
                f.Tick(2);Assert.IsFalse(f.Target.Time.IsStopped);Assert.Greater(f.Target.GetComp<TransformComp>().Position.Z,0);Assert.AreEqual(.02,f.Target.Time.Time,.00001);
            }
        }
        [Test]
        public void RepeatedRequestsUseMaximumAndSourceEqualsTargetDoesNotDoubleTheDuration()
        {
            using(var f=new Fixture())
            {
                f.World.RequestActorHitstop(f.Source.Id,2);f.World.RequestActorHitstop(f.Source.Id,5);f.Tick();Assert.AreEqual(4,f.Source.Time.HitstopLeft);
                f.World.RequestActorHitstop(f.Source.Id,2);f.Tick(4);Assert.IsTrue(f.Source.Time.IsStopped);Assert.AreEqual(0,f.Source.Time.HitstopLeft);f.Tick();Assert.IsFalse(f.Source.Time.IsStopped);
                f.World.Deliver(new IEffect[]{new DamageEffect{Flat=1,Coeff=0,SourceHitstopFrames=2,TargetHitstopFrames=3}},f.Source,f.Source,0);f.Tick(3);Assert.IsTrue(f.Source.Time.IsStopped);f.Tick();Assert.IsFalse(f.Source.Time.IsStopped);
            }
        }
        [Test]
        public void DamageDuringOneActorsTickDoesNotFreezeLaterActorsUntilTheNextStep()
        {
            using(var f=new Fixture())
            {
                f.Source.GetComp<TransformComp>().Position=new SimVec3(0,0,0);f.Target.GetComp<TransformComp>().Position=new SimVec3(.55f,0,0);
                f.Target.GetComp<LocomotionComp>().RequestMoveIntent(0,1);f.Source.GetComp<InputBufferComp>().Push(new InputToken("ArenaPrimary"));
                f.World.AddServicePhase(()=>{if(f.World.Time.Frame==1)f.Hit(0,2);});f.Tick();float z=f.Target.GetComp<TransformComp>().Position.Z;Assert.Greater(z,0);Assert.IsFalse(f.Target.Time.IsStopped);
                f.Tick(2);Assert.AreEqual(z,f.Target.GetComp<TransformComp>().Position.Z);f.Tick();Assert.Greater(f.Target.GetComp<TransformComp>().Position.Z,z);
            }
        }
        [Test]
        public void FrozenActorStillTakesDamageCanDieAndCanBeRecycledImmediately()
        {
            using(var f=new Fixture())
            {
                f.Hit(0,100);f.Tick();float hp=f.Target.GetComp<AttributeSet>().GetBase(AttrId.Hp);f.Hit(0,0,10);Assert.AreEqual(hp-10,f.Target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
                f.Hit(0,0,10000);Assert.AreEqual(ActivityId.Dead,f.Target.GetComp<StateMachineComp>().Current);var old=f.Target.Id;f.World.RequestDespawn(old);f.World.FlushCleanup();
                var next=f.Spawn("stake",4);Assert.AreEqual(old.Index,next.Id.Index);Assert.AreNotEqual(old.Generation,next.Id.Generation);Assert.AreEqual(0,next.Time.HitstopLeft);Assert.IsFalse(next.Time.IsStopped);Assert.IsFalse(f.World.RequestActorHitstop(old,3));
            }
        }
        [Test]
        public void FrozenOwnersDoNotStopExistingProjectilesAreasOrUnrelatedConstructs()
        {
            using(var f=new Fixture())
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(100,0,0);
                f.World.Deliver(new IEffect[]{new SpawnProjectileEffect(4004)},f.Source,null,0);f.Tick();var projectile=f.World.RegistryActive().Single(a=>a.TryGetComp<ProjectileComp>(out _));float x=projectile.GetComp<TransformComp>().Position.X;
                var area=new AoeDefinition{SpecId=999,Radius=1,Duration=1,PulseInterval=.1f,PulseOnSpawn=false};var areas=new AoeCatalog();areas.Register(area);f.World.ReplaceCatalogs(null,areas,null);
                f.World.Deliver(new IEffect[]{new SpawnAoeEffect(999)},f.Source,null,0);var aoe=f.World.RegistryActive().Single(a=>a.TryGetComp<AoeComp>(out _));
                var barrel=f.Spawn("construct",10);barrel.GetComp<ConstructComp>().Setup(new ConstructDefinition{SpecId=99,Hp=5,Group="test",DecayPeriod=.02f},f.Source.Id,1,default);
                f.World.RequestActorHitstop(f.Source.Id,4);f.Tick(2);Assert.Greater(projectile.GetComp<TransformComp>().Position.X,x);Assert.Greater(aoe.GetComp<AoeComp>().Age,0);Assert.AreEqual(3,barrel.GetComp<AttributeSet>().GetBase(AttrId.Hp));
                var position=projectile.GetComp<TransformComp>().Position.X;double age=aoe.GetComp<AoeComp>().Age;f.World.RequestActorHitstop(projectile.Id,2);f.World.RequestActorHitstop(aoe.Id,2);f.Tick(2);
                Assert.AreEqual(position,projectile.GetComp<TransformComp>().Position.X);Assert.AreEqual(age,aoe.GetComp<AoeComp>().Age);
            }
        }
        [Test]
        public void BuffAgeHealthImmunityEnergyCooldownAndInputWindowUseTheFrozenActorClock()
        {
            using(var f=new Fixture())
            {
                var input=f.Source.GetComp<InputBufferComp>();input.SetBufferWindow(.2f);input.Push(new InputToken("NoCombo"));var energy=f.Source.GetComp<EnergyComp>();energy.TrySpend(50);f.Source.GetComp<HealthComp>().BeginIFrame(.2f);
                var buff=new DurationSpec{BuffId=999,Duration=.3f,TickInterval=.1f,OnPeriod=Array.Empty<IEffect>()};f.Source.GetComp<BuffComp>().Apply(buff,f.Source);var before=f.Source.GetComp<BuffComp>().Snapshot().Single().Remaining;
                f.World.RequestActorHitstop(f.Source.Id,20);f.Tick(20);Assert.AreEqual(50,energy.Current);Assert.IsTrue(input.TryPeek(out _));Assert.IsTrue(f.Source.GetComp<HealthComp>().InIFrame);Assert.AreEqual(before,f.Source.GetComp<BuffComp>().Snapshot().Single().Remaining);
                f.Tick(11);Assert.IsFalse(input.TryPeek(out _));Assert.Greater(energy.Current,50);Assert.IsFalse(f.Source.GetComp<HealthComp>().InIFrame);
            }
        }
        [Test]
        public void HeldCastingPausesItsOwnEmissionCostAndCooldownAndCanBeReleasedWhileFrozen()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));var content=CombatContentCompiler.Compile(p);
            var player=content.Skills.All.Single(s=>s.Key=="held-spray");var world=new CombatWorld(new RuntimeActorFactory(content.Runtime?.Data??new BakedCombatData{Combo=new ComboTableDefinition{Entries=Array.Empty<ComboEntry>()},Timelines=content.Timelines},content.Skills,
                new Dictionary<string,RuntimeActorDefinition>{{"test",new RuntimeActorDefinition{Id="test",Role="player",Team=1,Hp=1000,MoveSpeed=3,DamageDealt=1,DamageTaken=1,Energy=100,InitialEnergy=100}}}));
            try
            {
                world.ReplaceCatalogs(content.Projectiles,content.Aoes,content.Summons);world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("test")),out var actor);var cast=actor.GetComp<SkillCastComp>();
                Assert.IsTrue(cast.TryCast(player.Id,new SkillAim(EntityId.Invalid,new SimVec3(10,0,0),0)));world.Tick(.02f);float energy=actor.GetComp<EnergyComp>().Current;float cooldown=cast.CooldownLeft(player.Id);int spawned=world.RegistryActive().Count(a=>a.TryGetComp<ProjectileComp>(out _));
                world.RequestActorHitstop(actor.Id,5);for(int i=0;i<5;i++)world.Tick(.02f);Assert.AreEqual(energy,actor.GetComp<EnergyComp>().Current);Assert.AreEqual(cooldown,cast.CooldownLeft(player.Id));Assert.AreEqual(spawned,world.RegistryActive().Count(a=>a.TryGetComp<ProjectileComp>(out _)));
                Assert.IsFalse(cast.TryCast(player.Id,new SkillAim(EntityId.Invalid,default,0)));cast.Release();Assert.IsFalse(cast.IsChanneling);
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void IndependentStopsDoNotConsumeTheirBudgetDuringAWorldStop()
        {
            using(var f=new Fixture())
            {
                f.World.RequestActorHitstop(f.Source.Id,2);f.World.RequestHitstop(3);f.Tick(3);Assert.IsTrue(f.World.InHitstop);Assert.AreEqual(2,f.Source.Time.HitstopLeft);Assert.AreEqual(0,f.Source.Time.Time);
                f.Tick(2);Assert.IsTrue(f.Source.Time.IsStopped);Assert.IsFalse(f.World.InHitstop);f.Tick();Assert.IsFalse(f.Source.Time.IsStopped);Assert.AreEqual(.02,f.Source.Time.Time,.00001);
            }
        }
        [Test]
        public void ShieldImmuneAndZeroDamageCannotRequestActorStopsOrActualHpHurtFeedback()
        {
            using(var f=new Fixture())
            {
                var events=new List<EvHitstop>();int hurts=0;f.World.Events.Subscribe<EvHitstop>(e=>events.Add(e));f.World.Events.Subscribe<EvHurt>(e=>hurts++);
                var effects=new IEffect[]{new DamageEffect{Coeff=0,Flat=10,SourceHitstopFrames=3,TargetHitstopFrames=5},new HurtFeedbackEffect(HitFeedbackLevel.Heavy)};
                f.Target.GetComp<AttributeSet>().SetBase(AttrId.Shield,20);f.World.Deliver(effects,f.Source,f.Target,0);Assert.AreEqual(0,events.Count);Assert.AreEqual(0,hurts);
                f.Target.GetComp<TagComp>().Add(CommonTags.Invincible,1,TagSource.Debug);f.World.Deliver(effects,f.Source,f.Target,0);Assert.AreEqual(0,events.Count);Assert.AreEqual(0,hurts);
                f.Target.GetComp<TagComp>().Remove(CommonTags.Invincible,1,TagSource.Debug);f.Target.GetComp<AttributeSet>().SetBase(AttrId.Shield,0);f.World.Deliver(effects,f.Source,f.Target,0);Assert.AreEqual(1,events.Count);Assert.AreEqual(1,hurts);Assert.AreEqual(HitstopScope.Actors,events[0].Scope);
                f.Target.GetComp<AttributeSet>().SetBase(AttrId.Hp,1);f.World.Deliver(effects,f.Source,f.Target,0);Assert.AreEqual(1,hurts,"Lethal hits use death presentation.");
            }
        }
        [Test]
        public void FeedbackIsGradedAndNeverChangesTheActivityState()
        {
            using(var f=new Fixture())
            {
                var origin=new SkillOrigin(new SkillNodeId(9),7);EvHurt hurt=default;f.World.Events.Subscribe<EvHurt>(e=>hurt=e);
                f.World.Deliver(new IEffect[]{new DamageEffect{Flat=1,Coeff=0,Feedback=HitFeedbackLevel.Finisher},new HurtFeedbackEffect(HitFeedbackLevel.Finisher)},f.Source,f.Target,0,origin:origin);
                Assert.AreEqual(HitFeedbackLevel.Finisher,hurt.Feedback);Assert.AreEqual(7,hurt.Origin.CastId);Assert.AreEqual(f.Target.Id,hurt.Target);Assert.AreEqual(ActivityId.Root,f.Target.GetComp<StateMachineComp>().Current);
            }
        }
        [TestCase(-1)] [TestCase(1001)]
        public void InvalidDirectActorRequestsAreRejected(int frames)
        {using(var f=new Fixture())Assert.Throws<ArgumentOutOfRangeException>(()=>f.World.RequestActorHitstop(f.Source.Id,frames));}
        [TestCase("source")] [TestCase("target")] [TestCase("feedback")] [TestCase("hurt")]
        public void InvalidJsonStopFramesAndFeedbackLevelsCannotBePublished(string field)
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));var graph=p.Skills.Single(s=>s.Id=="actor-hitstop");
            if(field=="source")graph.Nodes.Single(n=>n.Kind=="damage").Parameters["sourceHitstopFrames"]=-1;
            if(field=="target")graph.Nodes.Single(n=>n.Kind=="damage").Parameters["targetHitstopFrames"]=.5f;
            if(field=="feedback")graph.Nodes.Single(n=>n.Kind=="damage").Parameters["feedback"]="custom";
            if(field=="hurt")graph.Nodes.Single(n=>n.Kind=="hurt-feedback").Parameters["level"]="custom";
            Assert.Throws<ContentValidationException>(()=>SkillPublicationJson.Create(p,"actor-hitstop"));
        }
    }
}
