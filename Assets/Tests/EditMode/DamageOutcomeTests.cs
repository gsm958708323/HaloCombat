using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class DamageOutcomeTests
    {
        sealed class Capture:IEffect
        {
            public EffectContext Context;
            public void Apply(ref EffectContext context)=>Context=context;
        }
        static Actor Actor(ProjectileMotionTestWorld w,EntityId id)
        {w.World.TryGetActor(id,out var actor);return actor;}
        static (ProjectileMotionTestWorld w,Actor source,Actor target) Setup()
        {
            var w=new ProjectileMotionTestWorld(ProjectileMotionTestWorld.Definition());var source=Actor(w,w.Owner);var target=Actor(w,w.Spawn("target",2));
            source.GetComp<AttributeSet>().SetBase(AttrId.Atk,100);source.GetComp<AttributeSet>().SetBase(AttrId.DmgDealMul,1.5f);
            var a=target.GetComp<AttributeSet>();a.SetBase(AttrId.Def,40);a.SetBase(AttrId.Hp,200);a.SetBase(AttrId.Shield,60);a.SetBase(AttrId.DmgTakenMul,.5f);
            return(w,source,target);
        }

        [TestCase(true,DamageKind.Physical,150,300,260,520,390,200,130)]
        [TestCase(false,DamageKind.Physical,50,100,60,120,60,0,0)]
        [TestCase(true,DamageKind.Elemental,150,300,300,600,450,200,190)]
        public void StagesSeparateBonusesDefenseCriticalShieldHealthAndOverkill(bool bonuses,DamageKind kind,float basis,float scaled,float defense,float critical,float final,float lost,float overkill)
        {
            var setup=Setup();var capture=new Capture();var damage=new DamageEffect{Flat=50,Coeff=1,CanCrit=true,CritChance=1,CritMul=2,Kind=kind,IncludeBonuses=bonuses};
            var origin=new SkillOrigin(new SkillNodeId(23),42);
            setup.w.World.Deliver(new IEffect[]{damage,capture},setup.source,setup.target,100,power:2,origin:origin);var result=capture.Context.Damage;
            Assert.AreEqual(basis,result.Base);Assert.AreEqual(scaled,result.Scaled);Assert.AreEqual(defense,result.AfterDefense);Assert.AreEqual(critical,result.AfterCritical);
            Assert.AreEqual(final,result.Final);Assert.AreEqual(60,result.Absorbed);Assert.AreEqual(lost,result.HpLost);Assert.AreEqual(overkill,result.Overkill);
            Assert.AreEqual(lost>0,result.Killed);Assert.AreEqual(42,result.Origin.CastId);Assert.AreEqual(1,result.HitIndex);
            Assert.AreEqual(200-lost,setup.target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
        }

        [Test]
        public void RepeatedDamageCannotKillOrReportOverkillOnAnAlreadyDeadTarget()
        {
            var setup=Setup();setup.target.GetComp<AttributeSet>().SetBase(AttrId.Shield,0);
            var results=new List<EvDamage>();setup.w.World.Events.Subscribe<EvDamage>(e=>results.Add(e));
            var damage=new DamageEffect{Flat=1000,Coeff=0,IgnoreDef=true};var capture=new Capture();
            setup.w.World.Deliver(new IEffect[]{damage,damage,capture},setup.source,setup.target,0);
            Assert.AreEqual(1,results.Count);Assert.AreEqual(200,results[0].Amount);Assert.IsTrue(results[0].IsKill);Assert.IsNull(capture.Context.Damage);
        }

        [Test]
        public void AfterDamageRequiresActualHealthLossAndTheSameFullSourceAndTarget()
        {
            var setup=Setup();int calls=0;var gate=new AfterDamageEffect(new IEffect[]{new CallbackEffect(()=>calls++)});
            var damage=new DamageEffect{Flat=50,Coeff=0,IgnoreDef=true,IncludeBonuses=false};
            setup.w.World.Deliver(new IEffect[]{damage,gate},setup.source,setup.target,0);Assert.AreEqual(0,calls);
            setup.target.GetComp<AttributeSet>().SetBase(AttrId.Shield,0);
            setup.w.World.Deliver(new IEffect[]{damage,new TargetEffects(new IEffect[]{gate},true),gate},setup.source,setup.target,0);Assert.AreEqual(1,calls);
            setup.target.GetComp<TagComp>().Add(CommonTags.Invincible,1,TagSource.Debug);
            setup.w.World.Deliver(new IEffect[]{damage,gate},setup.source,setup.target,0);Assert.AreEqual(1,calls);
            setup.target.GetComp<TagComp>().Remove(CommonTags.Invincible,1,TagSource.Debug);damage.Flat=0;
            setup.w.World.Deliver(new IEffect[]{damage,gate},setup.source,setup.target,0);Assert.AreEqual(1,calls);
        }

        [Test]
        public void HealingReportsTheActualRestorationAndDoesNotReviveZeroHealth()
        {
            var setup=Setup();var healed=new List<EvHeal>();setup.w.World.Events.Subscribe<EvHeal>(e=>healed.Add(e));
            setup.target.GetComp<AttributeSet>().SetBase(AttrId.Hp,990);
            setup.w.World.Deliver(new IEffect[]{new HealEffect{Amount=100}},setup.source,setup.target,0);Assert.AreEqual(10,healed.Single().Amount);
            setup.target.GetComp<AttributeSet>().SetBase(AttrId.Hp,0);
            setup.w.World.Deliver(new IEffect[]{new HealEffect{Amount=100}},setup.source,setup.target,0);Assert.AreEqual(0,setup.target.GetComp<AttributeSet>().GetBase(AttrId.Hp));Assert.AreEqual(1,healed.Count);
        }
    }
}
