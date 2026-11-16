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
    public sealed class SharedTimelineTests
    {
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        static SkillProjectDocument Examples()=>SkillProjectFiles.Read(Path.Combine(Application.dataPath,"../Content/SkillLab/project.json"));
        static SharedTimelineDocument Shared(SkillProjectDocument p)=>p.Timelines.Single();
        static SkillGraphDocument Second(SkillProjectDocument p)=>p.Skills.Single(s=>s.Id=="arena-combo-second");
        static SkillGraphDocument Finish(SkillProjectDocument p)=>p.Skills.Single(s=>s.Id=="arena-combo-finisher");
        static SkillDefinition Skill(CombatContentSnapshot c,string id)=>c.Skills.All.Single(s=>s.Key==id);

        [Test]
        public void TwoOriginalSkillsResolveToOneSharedTimelineAndKeepTheirOwnRules()
        {
            var p=Formal();var c=CombatContentCompiler.Compile(p);var second=Skill(c,Second(p).Id);var finish=Skill(c,Finish(p).Id);
            Assert.AreEqual(2012,second.Id.Value);Assert.AreEqual(2013,finish.Id.Value);Assert.AreEqual(second.Timeline,finish.Timeline);Assert.AreEqual(3013,second.Timeline.Value);
            Assert.AreEqual(2,second.AmmoCost);Assert.AreEqual(3,finish.AmmoCost);Assert.AreEqual(HitFeedbackLevel.ComboConfirm,second.HitFeedback);Assert.AreEqual(HitFeedbackLevel.Finisher,finish.HitFeedback);
            Assert.IsTrue(c.Timelines.TryGet(second.Timeline,out var timeline));Assert.AreEqual(.65f,timeline.Duration);Assert.AreEqual(.22f,timeline.Payloads.Single().Time);Assert.AreEqual(.25f,timeline.Clips.Single().Start);Assert.AreEqual(.65f,timeline.Clips.Single().End);
            Assert.IsTrue(c.Projectiles.TryGet(4012,out var projectile));Assert.AreEqual(10,projectile.Speed);Assert.AreEqual(.16f,projectile.HitRadius);Assert.AreEqual(4,(((PierceEffect)System.Array.Find(projectile.OnHit,e=>e is PierceEffect))?.MaxHits??1));Assert.AreEqual(.15f,projectile.SameTargetDelay);
            Shared(p).Timeline.Duration=9;Assert.AreEqual(.65f,timeline.Duration,"Authoring DTO cannot mutate a compiled session.");
        }
        [Test]
        public void EditingOneSharedActionUpdatesEveryUserOnlyAfterRecompilation()
        {
            var p=Formal();var old=CombatContentCompiler.Compile(p);Shared(p).Timeline.Duration=.7f;Shared(p).Timeline.Payloads.Single().Time=.3f;
            var current=CombatContentCompiler.Compile(p);Assert.IsTrue(old.Timelines.TryGet(new TimelineId(3013),out var before));Assert.IsTrue(current.Timelines.TryGet(new TimelineId(3013),out var after));
            Assert.AreEqual(.65f,before.Duration);Assert.AreEqual(.22f,before.Payloads[0].Time);Assert.AreEqual(.7f,after.Duration);Assert.AreEqual(.3f,after.Payloads[0].Time);
            Assert.AreEqual(Skill(current,Second(p).Id).Timeline,Skill(current,Finish(p).Id).Timeline);
        }
        [TestCase("missing")] [TestCase("inline")] [TestCase("number")] [TestCase("duplicate")] [TestCase("null")] [TestCase("root")] [TestCase("entry")] [TestCase("cast-output")]
        public void SharedTimelineBoundaryRejectsAmbiguousOrMissingContent(string fault)
        {
            var p=Formal();var s=Second(p);var shared=Shared(p);
            if(fault=="missing")s.TimelineRef="missing";
            if(fault=="inline")s.Timeline=new TimelineDocument{RuntimeId=60000,Duration=.5f};
            if(fault=="number")shared.Timeline.RuntimeId=p.Skills[0].Timeline.RuntimeId;
            if(fault=="duplicate")p.Timelines=new[]{shared,shared};
            if(fault=="null")shared.Timeline=null;
            if(fault=="root")shared.Nodes=shared.Nodes.Concat(new[]{new SkillNodeDocument{Id="root",Kind="buff-events"}}).ToArray();
            if(fault=="entry")shared.Timeline.Payloads[0].Effects=new[]{"missing"};
            if(fault=="cast-output")
            {s.Nodes=s.Nodes.Concat(new[]{new SkillNodeDocument{Id="ammo",Kind="refill-ammo",Parameters=JObject.FromObject(new{amount=1})}}).ToArray();s.Edges=s.Edges.Concat(new[]{new SkillEdgeDocument{Id="direct",Source="cast",SourcePort="cast",Target="ammo",TargetPort="execute"}}).ToArray();}
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void SharedActionSummoningItsOwnSkillIsRejectedAsARealDependencyCycle()
        {
            var p=Examples();p.SummonBlueprints[0].SkillId="shared-shot-light";
            var shared=Shared(p);shared.Nodes=shared.Nodes.Concat(new[]{new SkillNodeDocument{Id="summon-cycle",Kind="delivery",RuntimeId=6504,Parameters=JObject.FromObject(new{mode="summon",blueprint=p.SummonBlueprints[0].Id})}}).ToArray();
            shared.Timeline.Payloads[0].Effects=shared.Timeline.Payloads[0].Effects.Concat(new[]{"summon-cycle"}).ToArray();
            StringAssert.Contains("Recursive",Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p)).Message);
        }
        [Test]
        public void PublicationContainsTheExactSharedActionBuffAndFallbackClosure()
        {
            var p=Formal();Shared(p).Nodes[0].Position.X=123;var pub=SkillPublicationJson.Create(p,"arena-combo-second");var read=SkillPublicationJson.Read(SkillPublicationJson.Write(pub));
            CollectionAssert.AreEquivalent(new[]{"arena-combo-second","arena-reload-ammo"},read.Document.Project.Skills.Select(s=>s.Id));
            Assert.AreEqual("arena-confirmed-shot",read.Document.Project.Timelines.Single().Id);Assert.AreEqual("arena-vulnerability",read.Document.Project.Buffs.Single().Id);
            Assert.IsTrue(read.Content.Projectiles.TryGet(4012,out _));Assert.IsFalse(read.Content.Projectiles.TryGet(4001,out _));Assert.AreEqual(2,read.Content.Skills.All.Count());
            Assert.IsTrue(pub.Project.Timelines.Single().Nodes.All(n=>n.Position.X==0&&n.Position.Y==0));Assert.AreEqual(123,Shared(p).Nodes[0].Position.X);
        }
        [Test]
        public void TimelineLayoutDoesNotChangeAPublicationRevisionButSemanticEditsDo()
        {
            var p=Formal();string revision=SkillPublicationJson.Create(p,"arena-combo-second").Revision;
            Shared(p).Nodes[0].Position.X=999;Assert.AreEqual(revision,SkillPublicationJson.Create(p,"arena-combo-second").Revision);
            Shared(p).Timeline.Payloads[0].Time=.3f;Assert.AreNotEqual(revision,SkillPublicationJson.Create(p,"arena-combo-second").Revision);
        }
        [Test]
        public void TwoSlotsReuseOneDefinitionAndConflictingSharedVersionsKeepTheRunningSession()
        {
            var p=Formal();var first=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-second"));var other=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-finisher"));
            using(var session=new PublishedSkillSession())
            {
                session.Replace(new Dictionary<string,string>{{"f6",first},{"f7",other}});var previous=session.Simulation;
                Assert.AreEqual(1,SkillPublicationJson.Read(other).Document.Project.Timelines.Length);
                Shared(p).Timeline.Duration=.7f;other=SkillPublicationJson.Write(SkillPublicationJson.Create(p,"arena-combo-finisher"));
                StringAssert.Contains("timeline versions",Assert.Throws<ContentValidationException>(()=>session.Replace(new Dictionary<string,string>{{"f6",first},{"f7",other}})).Message);Assert.AreSame(previous,session.Simulation);
                Assert.Throws<ContentValidationException>(()=>new ArenaProject(Formal(),new Dictionary<string,string>{{"f6",first},{"f7",other}}));
            }
        }
        [Test]
        public void SeparateDirectorsPlayOneDefinitionWithIndependentTimeCostsAndOrigins()
        {
            var p=Formal();var loaded=CombatContentCompiler.Compile(p);var world=new CombatWorld(loaded.Runtime.CreateFactory(loaded.Skills),random:new FixedRandom(.9f));
            try
            {
                Actor Spawn(){world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-player")),out var a);a.GetComp<TransformComp>().Position=new SimVec3(-10,0,0);return a;}
                var a=Spawn();var b=Spawn();var second=Skill(loaded,Second(p).Id);var finish=Skill(loaded,Finish(p).Id);
                Assert.IsTrue(a.GetComp<SkillCastComp>().TryCast(second.Id,new SkillAim(EntityId.Invalid,new SimVec3(10,0,0),0)));for(int i=0;i<5;i++)world.Tick(.02f);
                Assert.IsTrue(b.GetComp<SkillCastComp>().TryCast(finish.Id,new SkillAim(EntityId.Invalid,new SimVec3(10,0,0),0)));
                Assert.AreEqual(.1f,a.GetComp<SkillDirectorComp>().TimelineTime,.00001);Assert.AreEqual(0,b.GetComp<SkillDirectorComp>().TimelineTime);
                Assert.AreEqual(58,a.GetComp<AmmoComp>().Current);Assert.AreEqual(57,b.GetComp<AmmoComp>().Current);Assert.AreNotEqual(a.GetComp<SkillDirectorComp>().CurrentOrigin.CastId,b.GetComp<SkillDirectorComp>().CurrentOrigin.CastId);
                a.GetComp<SkillDirectorComp>().Stop(DirectorStopReason.Manual);Assert.IsTrue(b.GetComp<SkillDirectorComp>().IsPlaying);
            }
            finally{world.Shutdown();}
        }

        sealed class Chain : IDisposable
        {
            public readonly CombatWorld World;
            public readonly Actor Player,Target;
            public SkillDirectorComp Director=>Player.GetComp<SkillDirectorComp>();
            public readonly List<DamageResult> Damage=new List<DamageResult>();
            public readonly List<EvHurt> Hurt=new List<EvHurt>();
            public Chain(int ammo=60,float targetX=1)
            {
                var p=Formal();p.Runtime.Actors.Single(a=>a.Id=="arena-player").InitialAmmo=ammo;var c=CombatContentCompiler.Compile(p);
                World=new CombatWorld(c.Runtime.CreateFactory(c.Skills),random:new FixedRandom(.9f));World.ReplaceCatalogs(c.Projectiles,c.Aoes,c.Summons);
                World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-player")),out Player);World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-target")),out Target);
                Target.GetComp<TransformComp>().Position=new SimVec3(targetX,0,0);World.Events.Subscribe<EvDamage>(e=>Damage.Add(e.Result));World.Events.Subscribe<EvHurt>(e=>Hurt.Add(e));
            }
            public void Input(){Player.GetComp<InputBufferComp>().Push(new InputToken("ArenaPrimary"));Tick();}
            public void Tick(int n=1){for(int i=0;i<n;i++)World.Tick(.02f);}
            public void Ready(int skill)
            {int count=0;while(count++<100&&(!Director.HasComboConfirmation||!Player.GetComp<TagComp>().Has(CommonTags.Cancel)))Tick();Assert.AreEqual(skill,Director.CurrentSkill.Value);Assert.IsTrue(Director.HasComboConfirmation);Assert.IsTrue(Player.GetComp<TagComp>().Has(CommonTags.Cancel));}
            public void Dispose()=>World.Shutdown();
        }
        [Test]
        public void RealPrimaryInputsRunTheOriginalConfirmedThreeHitChainWithDistinctGradesAndCosts()
        {
            using(var f=new Chain())
            {
                f.Input();f.Ready(2001);f.Input();Assert.AreEqual(2012,f.Director.CurrentSkill.Value);f.Ready(2012);f.Input();Assert.AreEqual(2013,f.Director.CurrentSkill.Value);
                f.Tick(40);Assert.AreEqual(54,f.Player.GetComp<AmmoComp>().Current);CollectionAssert.AreEqual(new[]{50f,84.375f,84.375f},f.Damage.Select(d=>d.HpLost));
                CollectionAssert.AreEqual(new[]{HitFeedbackLevel.Light,HitFeedbackLevel.ComboConfirm,HitFeedbackLevel.Finisher},f.Damage.Select(d=>d.Feedback));
                CollectionAssert.AreEqual(new[]{HitFeedbackLevel.Light},f.Hurt.Select(d=>d.Feedback),"PD4012 has damage/Cue/Buff; the original asset has no HurtFeedback entry.");Assert.AreEqual(1.25f,f.Target.GetComp<AttributeSet>().GetFinal(AttrId.DmgTakenMul));
            }
        }
        [Test]
        public void ACancelledWindowWithoutContactCannotAdvanceTheConfirmedChain()
        {
            using(var f=new Chain(targetX:20))
            {f.Input();f.Tick(25);Assert.IsTrue(f.Player.GetComp<TagComp>().Has(CommonTags.Cancel));Assert.IsFalse(f.Director.HasComboConfirmation);f.Input();f.Tick(3);Assert.AreEqual(2001,f.Director.CurrentSkill.Value);Assert.AreEqual(59,f.Player.GetComp<AmmoComp>().Current);Assert.AreEqual(0,f.Damage.Count);}
        }
        [Test]
        public void ConfirmedSecondStageWithInsufficientAmmoUsesItsOwnReloadFallback()
        {
            using(var f=new Chain(1))
            {f.Input();f.Ready(2001);f.Input();Assert.AreEqual(2009,f.Director.CurrentSkill.Value);Assert.AreEqual(0,f.Player.GetComp<AmmoComp>().Current);Assert.IsFalse(f.Director.HasComboConfirmation);f.Tick(55);Assert.AreEqual(60,f.Player.GetComp<AmmoComp>().Current);}
        }
    }
}
