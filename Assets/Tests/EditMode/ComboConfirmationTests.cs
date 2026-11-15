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
    public sealed class ComboConfirmationTests
    {
        sealed class Fixture : IDisposable
        {
            public readonly RuntimeProjectLoader Loaded;
            public readonly CombatWorld World;
            public readonly Actor Player,Target;
            public SkillDirectorComp Director=>Player.GetComp<SkillDirectorComp>();
            public TagComp Tags=>Player.GetComp<TagComp>();
            public Fixture()
            {
                Loaded=new RuntimeProjectLoader(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));World=Loaded.CreateWorld(new FixedRandom(.9f));
                Player=Spawn("fighter",0);Target=Spawn("stake",8);
                Target.GetComp<AttributeSet>().SetBase(AttrId.MaxHp,1000);Target.GetComp<AttributeSet>().SetBase(AttrId.Hp,1000);
            }
            public Actor Spawn(string id,float x){World.TryGetActor(World.SpawnActor(new ActorSpawnSpec(id)),out var actor);actor.GetComp<TransformComp>().Position=new SimVec3(x,0,0);return actor;}
            public void Play()=>Assert.IsTrue(Player.GetComp<SkillCastComp>().TryCast(SkillNodeId.G1,new SkillAim(EntityId.Invalid,new SimVec3(8,0,0),0)));
            public void Contact(float flat=0,SkillOrigin? origin=null,Actor target=null)
            {World.Deliver(new IEffect[]{new DamageEffect{Flat=flat,Coeff=0,CanCrit=false}},Player,target??Target,0,origin:origin??Director.CurrentOrigin);}
            public void Tick(int steps=1){for(int i=0;i<steps;i++)World.Tick(.02f);}
            public void Dispose()=>World.Shutdown();
        }
        [TestCase("zero")] [TestCase("shield")] [TestCase("immune")]
        public void HostileContactConfirmsBeforeDamageEligibility(string kind)
        {
            using(var f=new Fixture())
            {
                f.Play();if(kind=="shield")f.Target.GetComp<AttributeSet>().SetBase(AttrId.Shield,100);
                if(kind=="immune")f.Target.GetComp<TagComp>().Add(CommonTags.Invincible,1,TagSource.Debug);
                f.Contact(kind=="zero"?0:10);Assert.IsTrue(f.Director.HasComboConfirmation);Assert.IsTrue(f.Tags.Has(CommonTags.ComboConfirm));Assert.AreEqual(1000,f.Target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
                f.Contact();Assert.AreEqual(1,f.Tags.Stack(CommonTags.ComboConfirm),"Repeated contacts share one owned confirmation lease.");
            }
        }
        [Test]
        public void FriendlyDeadAndStaleCasterIdentityCannotGrantConfirmation()
        {
            using(var f=new Fixture())
            {
                f.Play();f.Contact(target:f.Player);Assert.IsFalse(f.Director.HasComboConfirmation);
                var current=f.Director.CurrentOrigin;var fake=new SkillOrigin(current.Skill,current.CastId,caster:new EntityId(f.Player.Id.Index,f.Player.Id.Generation+1));
                f.Contact(origin:fake);Assert.IsFalse(f.Director.HasComboConfirmation);
                f.Target.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead,new ActivityEnterArgs{Reason="test"});f.Contact();Assert.IsFalse(f.Director.HasComboConfirmation);
            }
        }
        [Test]
        public void OldProjectilesCannotConfirmANewCastOrAnAlreadyFinishedCast()
        {
            using(var f=new Fixture())
            {
                f.Play();var old=f.Director.CurrentOrigin;f.Director.Stop(DirectorStopReason.Manual);f.Play();f.Contact(origin:old);Assert.IsFalse(f.Director.HasComboConfirmation);
                f.Contact();Assert.IsTrue(f.Director.HasComboConfirmation);var recent=f.Director.CurrentOrigin;f.Tick(30);Assert.IsFalse(f.Director.IsPlaying);Assert.IsFalse(f.Tags.Has(CommonTags.ComboConfirm));
                f.Contact(origin:recent);Assert.IsFalse(f.Director.HasComboConfirmation);
            }
        }
        [TestCase(DirectorStopReason.Manual)] [TestCase(DirectorStopReason.InputCancelled)] [TestCase(DirectorStopReason.Released)] [TestCase(DirectorStopReason.Hit)] [TestCase(DirectorStopReason.Dead)] [TestCase(DirectorStopReason.Knockdown)]
        public void EveryStopPathClearsOnlyTheDirectorsOwnedConfirmation(DirectorStopReason reason)
        {
            using(var f=new Fixture())
            {
                var external=f.Tags.Acquire(CommonTags.ComboConfirm);f.Play();f.Contact();Assert.AreEqual(2,f.Tags.Stack(CommonTags.ComboConfirm));
                f.Director.Stop(reason);Assert.IsFalse(f.Director.HasComboConfirmation);Assert.AreEqual(1,f.Tags.Stack(CommonTags.ComboConfirm));external.Dispose();Assert.IsFalse(f.Tags.Has(CommonTags.ComboConfirm));
            }
        }
        [Test]
        public void ReplacementClearsConfirmationBeforeStartingTheNextCast()
        {
            using(var f=new Fixture())
            {
                f.Play();f.Contact();using(f.Tags.Acquire(CommonTags.Cancel))Assert.IsTrue(f.Player.GetComp<SkillCastComp>().TryCast(SkillNodeId.G2,new SkillAim(EntityId.Invalid,default,0)));
                Assert.AreEqual(SkillNodeId.G2,f.Director.CurrentSkill);Assert.IsFalse(f.Director.HasComboConfirmation);Assert.IsFalse(f.Tags.Has(CommonTags.ComboConfirm));
            }
        }
        [TestCase(true)] [TestCase(false)]
        public void CompleteTableLookupConsumesConfirmationButDoesNotConsumeInput(bool matches)
        {
            using(var f=new Fixture())
            {
                f.Play();f.Contact();var buffer=f.Player.GetComp<InputBufferComp>();buffer.Push(new InputToken(matches?"ConfirmAttack":"Unmapped"));
                using(f.Tags.Acquire(CommonTags.Cancel))
                {bool resolved=f.Player.GetComp<ComboComp>().TryResolve(out var result);Assert.AreEqual(matches,resolved);if(matches)Assert.AreEqual(SkillNodeId.G2,result.ToSkill);}
                Assert.IsFalse(f.Director.HasComboConfirmation);Assert.IsTrue(buffer.TryPeek(out _));
            }
        }
        [Test]
        public void CancelClosedStoppedAndBlockedStateDoNotConsumeTheConfirmation()
        {
            using(var f=new Fixture())
            {
                f.Play();f.Contact();var combo=f.Player.GetComp<ComboComp>();var token=new InputToken("ConfirmAttack");Assert.IsFalse(combo.TryResolveCurrent(token,out _));Assert.IsTrue(f.Director.HasComboConfirmation);
                using(f.Tags.Acquire(CommonTags.Cancel))
                {
                    f.World.RequestActorHitstop(f.Player.Id,2);f.Tick();Assert.IsFalse(combo.TryResolveCurrent(token,out _));Assert.IsTrue(f.Director.HasComboConfirmation);f.Tick(2);
                    using(f.Tags.Acquire(CommonTags.Silence)){Assert.IsFalse(combo.TryResolveCurrent(token,out _));Assert.IsTrue(f.Director.HasComboConfirmation);}
                    Assert.IsTrue(combo.TryResolveCurrent(token,out _));Assert.IsFalse(f.Director.HasComboConfirmation);
                }
            }
        }
        [Test]
        public void FailedResourceCastKeepsTheInputButCannotRestoreConsumedConfirmation()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
            p.Skills.Single(s=>s.Id=="g2").Nodes.Single(n=>n.Kind=="cast").Parameters["cost"]=20;
            p.Runtime.Actors.Single(a=>a.Id=="arena-player").Recovery=0;
            using(var s=new ArenaProject(p).Create(true))
            {
                s.Start();s.Advance();s.World.TryGetActor(s.PlayerId,out var actor);var cast=actor.GetComp<SkillCastComp>();actor.GetComp<EnergyComp>().TrySpend(100);
                Assert.IsTrue(cast.TryCast(SkillNodeId.G1,new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)));var director=actor.GetComp<SkillDirectorComp>();
                var target=s.World.RegistryActive().First(a=>a.Id!=s.PlayerId);s.World.Deliver(new IEffect[]{new DamageEffect{Coeff=0,Flat=0}},actor,target,0,origin:director.CurrentOrigin);
                actor.GetComp<InputBufferComp>().Push(new InputToken("ConfirmAttack"));s.Advance(7);Assert.IsTrue(director.HasComboConfirmation,"The driver precedes the clip opening at the first nonempty cancel interval.");
                s.Advance();Assert.AreEqual(SkillNodeId.G1,director.CurrentSkill);Assert.IsFalse(director.HasComboConfirmation);Assert.IsTrue(actor.GetComp<InputBufferComp>().TryPeek(out _));
                s.Advance();Assert.IsTrue(actor.GetComp<InputBufferComp>().TryPeek(out _));Assert.AreEqual(0,actor.GetComp<EnergyComp>().Current,.001f);
            }
        }
        [Test]
        public void CurrentHitboxCarriesTheCapturedCastIntoPhysicalContact()
        {
            using(var f=new Fixture())
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(.55f,0,0);f.Play();var origin=f.Director.CurrentOrigin;var seen=new List<EvDamage>();f.World.Events.Subscribe<EvDamage>(e=>seen.Add(e));f.Tick(10);
                Assert.IsTrue(f.Director.HasComboConfirmation);Assert.IsTrue(seen.Any(e=>e.Origin.CastId==origin.CastId&&e.Origin.Skill==SkillNodeId.G1&&e.Origin.Caster==f.Player.Id));
            }
        }
        [Test]
        public void PureTableLookupDoesNotConsumeATagAndHighestPriorityWins()
        {
            using(var f=new Fixture())
            {
                f.Play();f.Contact();var table=new ComboTableDefinition{Entries=new[]{
                    new ComboEntry{Key="low",PreSkills=new[]{SkillNodeId.G1},Input=InputToken.Attack,Priority=1,ToSkill=SkillNodeId.G1},
                    new ComboEntry{Key="high",PreSkills=new[]{SkillNodeId.G1},Input=InputToken.Attack,RequiredTags=new[]{1017},Priority=2,ToSkill=SkillNodeId.G2}}};
                Assert.IsTrue(table.TryResolve(SkillNodeId.G1,InputToken.Attack,f.Tags,out var result));Assert.AreEqual("high",result.Key);Assert.IsTrue(f.Director.HasComboConfirmation);
            }
        }
        [Test]
        public void AmbiguousEqualPriorityPredecessorsAreRejectedEvenWhenTagsDiffer()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
            var original=p.Runtime.Combos.Single(c=>c.Id=="confirmed-g1-g2");p.Runtime.Combos=p.Runtime.Combos.Concat(new[]{new RuntimeComboDocument{Id="conflict",Input=original.Input,PreSkills=new[]{"g1","g2"},Priority=original.Priority,ToSkill="g1",RequiredTags=new[]{999}}}).ToArray();
            var error=Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));StringAssert.Contains("conflict",error.Path);StringAssert.Contains("priority",error.Message);
        }
        [Test]
        public void SharedPreviewInputUsesTheActualComboAndDoesNotRebuildTheSession()
        {
            var p=SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));p.Preview.Caster.X=0;p.Preview.Caster.Atk=10;p.Preview.Targets=new[]{new PreviewActorDocument{Id="target",X=.55f,Hp=1000}};
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                s.Input("ConfirmAttack",4,0);s.Advance(10);var first=s.Snapshot();Assert.IsTrue(first.Actors.Single(a=>a.Kind=="caster").ComboConfirmed);Assert.Greater(first.Statistics.Damage,0);
                s.Input("ConfirmAttack",4,0);s.Advance(4);var after=s.Snapshot();Assert.AreEqual(2,after.Statistics.Casts);Assert.AreEqual("g2",after.Actors.Single(a=>a.Kind=="caster").CurrentSkill);Assert.IsNull(after.Actors.Single(a=>a.Kind=="caster").BufferedInput);
                Assert.IsTrue(after.Events.Any(e=>e.Kind=="combo-resolved"&&e.Result.Contains("confirmed-g1-g2")));
            }
        }
    }
}
