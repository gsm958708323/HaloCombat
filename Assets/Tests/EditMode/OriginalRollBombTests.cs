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
    public sealed class OriginalRollBombTests
    {
        static SkillProjectDocument Formal()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        sealed class F:IDisposable
        {
            public readonly CombatWorld World;
            public readonly Actor Player;
            public readonly List<EvCue> Cues=new List<EvCue>();
            public readonly List<EvDamage> Damage=new List<EvDamage>();
            public readonly List<EvProjectileEnded> Ended=new List<EvProjectileEnded>();
            public F(ObstacleDefinition[] walls=null)
            {
                var content=CombatContentCompiler.Compile(Formal());World=new CombatWorld(content.Runtime.CreateFactory(content.Skills),random:new FixedRandom(.9f),obstacles:walls);
                World.ReplaceCatalogs(content.Projectiles,content.Aoes,content.Summons);World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-player")),out Player);
                World.Events.Subscribe<EvCue>(e=>Cues.Add(e));World.Events.Subscribe<EvDamage>(e=>Damage.Add(e));World.Events.Subscribe<EvProjectileEnded>(e=>Ended.Add(e));
            }
            public Actor Target(float x){World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-target")),out var target);target.GetComp<TransformComp>().Position=new SimVec3(x,0,0);return target;}
            public void Tick(int n=1){for(int i=0;i<n;i++)World.Tick(.02f);}
            public Actor[] Areas(int id)=>World.RegistryActive().Where(a=>a.TryGetComp<AoeComp>(out var b)&&b.Def.SpecId==id).ToArray();
            public bool Cast(int skill)=>Player.GetComp<SkillCastComp>().TryCast(new SkillNodeId(skill),new SkillAim(EntityId.Invalid,new SimVec3(20,0,0),0));
            public void Dispose()=>World.Shutdown();
        }
        [Test]
        public void OriginalRollPreservesInvulnerabilityReductionAndTwoMeterMovementWindows()
        {
            using(var f=new F())
            {
                Assert.IsTrue(f.Cast(2002));f.Tick(4);Assert.IsFalse(f.Player.GetComp<TagComp>().Has(CommonTags.Invincible));Assert.AreEqual(0,f.Player.GetComp<TransformComp>().Position.X,.00001);
                f.Tick();Assert.IsFalse(f.Player.GetComp<TagComp>().Has(CommonTags.Invincible),"Reaching .1s is not a nonempty interval yet.");Assert.AreEqual(.5f,f.Player.GetComp<AttributeSet>().GetFinal(AttrId.DmgTakenMul));
                f.Tick();Assert.IsTrue(f.Player.GetComp<TagComp>().Has(CommonTags.Invincible));
                f.Tick(4);Assert.AreEqual(0,f.Player.GetComp<TransformComp>().Position.X,.00001);f.Tick(30);Assert.AreEqual(2,f.Player.GetComp<TransformComp>().Position.X,.0001);
                Assert.IsFalse(f.Player.GetComp<TagComp>().Has(CommonTags.Invincible));Assert.AreEqual(1,f.Player.GetComp<AttributeSet>().GetFinal(AttrId.DmgTakenMul));
                f.Tick(5);Assert.IsFalse(f.Player.GetComp<SkillDirectorComp>().IsPlaying);Assert.IsFalse(f.Player.GetComp<TagComp>().Has(CommonTags.BlockMove));
                CollectionAssert.AreEquivalent(new[]{"roll_fire","buff_5104"},f.Cues.Where(c=>c.Playback==CuePlayback.Start).Select(c=>c.InstanceKey));
                CollectionAssert.AreEquivalent(new[]{"roll_fire","buff_5104"},f.Cues.Where(c=>c.Playback==CuePlayback.Stop).Select(c=>c.InstanceKey));Assert.AreEqual(1,f.Cues.Count(c=>c.CueId==4208));
            }
        }
        [Test]
        public void OriginalBombLifetimeLeavesOneDelayedBombThenOneExplosionAndBurningArea()
        {
            using(var f=new F())
            {
                Assert.IsTrue(f.Cast(2007));f.Tick(110);Assert.AreEqual(1,f.Ended.Count);Assert.AreEqual(ProjectileEndReason.Lifetime,f.Ended[0].Reason);Assert.AreEqual(1,f.Areas(4105).Length);
                Assert.AreEqual(1,f.Cues.Count(c=>c.CueId==4206));Assert.AreEqual(0,f.Areas(4106).Length);f.Tick(150);
                Assert.AreEqual(0,f.Areas(4105).Length);Assert.AreEqual(1,f.Areas(4106).Length);Assert.AreEqual(2,f.Cues.Count(c=>c.CueId==4206));
                f.Tick(160);Assert.AreEqual(0,f.Areas(4106).Length);Assert.AreEqual(0,f.World.RegistryActive().Count(a=>a.TryGetComp<ProjectileComp>(out _)));
            }
        }
        [Test]
        public void BombContactCreatesOnlyTheSharedExplosionAndPreservesTheOriginalCaster()
        {
            using(var f=new F())
            {
                var target=f.Target(1);Assert.IsTrue(f.Cast(2007));f.Tick(40);Assert.AreEqual(1,f.Damage.Count);Assert.AreEqual(5,f.Damage[0].Amount,.0001);Assert.AreEqual(f.Player.Id,f.Damage[0].Source);Assert.AreEqual(2007,f.Damage[0].Origin.Skill.Value);
                Assert.AreEqual(ProjectileEndReason.HitLimit,f.Ended.Single().Reason);Assert.AreEqual(0,f.Areas(4105).Length);Assert.AreEqual(0,f.Areas(4106).Length);Assert.AreEqual(1,f.Cues.Count(c=>c.CueId==4206));
            }
        }
        [Test]
        public void BombWallTerminationDoesNotRunItsLifetimeDelayedBranch()
        {
            using(var f=new F(new[]{new ObstacleDefinition("wall",1,0,.2f,4)}))
            {Assert.IsTrue(f.Cast(2007));f.Tick(110);Assert.AreEqual(ProjectileEndReason.Wall,f.Ended.Single().Reason);Assert.AreEqual(0,f.Areas(4105).Length);Assert.AreEqual(1,f.Cues.Count(c=>c.CueId==4206));}
        }
        [Test]
        public void ArenaRollAndFire3ResolveToTheOriginalSkillIds()
        {
            var p=Formal();Assert.AreEqual("arena-roll",p.Runtime.Combos.Single(c=>c.Id=="ArenaDodge").ToSkill);Assert.AreEqual("arena-bomb-shot",p.Runtime.Combos.Single(c=>c.Id=="ArenaAura").ToSkill);
            using(var session=new ArenaProject(p).Create(true))
            {session.Start();session.ApplyInput(new ArenaInputFrame{Pressed=new[]{"Fire3"},AimValid=true,Aim=new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),0)});session.Advance();session.World.TryGetActor(session.PlayerId,out var player);Assert.AreEqual(2007,player.GetComp<SkillDirectorComp>().CurrentSkill.Value);}
        }
        static void Confirmed(F f)
        {for(int i=0;i<100;i++){var director=f.Player.GetComp<SkillDirectorComp>();if(director.HasComboConfirmation&&f.Player.GetComp<TagComp>().Has(CommonTags.Cancel))return;f.Tick();}Assert.Fail("Missing original contact/cancel window.");}
        [Test]
        public void OriginalBombComboRequiresTheConfirmedSecondShotAndKeepsItsPriorityAndCost()
        {
            var p=Formal();var row=p.Runtime.Combos.Single(c=>c.Id=="ArenaSecond-confirm-bomb");CollectionAssert.AreEqual(new[]{"arena-combo-second"},row.PreSkills);Assert.AreEqual(5,row.Priority);
            using(var f=new F())
            {
                f.Target(1);var input=f.Player.GetComp<InputBufferComp>();input.Push(new InputToken("ArenaPrimary"));f.Tick();Confirmed(f);input.Push(new InputToken("ArenaPrimary"));f.Tick();Assert.AreEqual(2012,f.Player.GetComp<SkillDirectorComp>().CurrentSkill.Value);
                Confirmed(f);input.Push(new InputToken("ArenaAura"));f.Tick();Assert.AreEqual(2007,f.Player.GetComp<SkillDirectorComp>().CurrentSkill.Value);Assert.AreEqual(57,f.Player.GetComp<AmmoComp>().Current);
            }
        }
        [Test]
        public void AConfirmedFirstShotCannotSkipTheSecondStageIntoTheBomb()
        {
            using(var f=new F())
            {f.Target(1);Assert.IsTrue(f.Cast(2001));Confirmed(f);var input=f.Player.GetComp<InputBufferComp>();input.Push(new InputToken("ArenaAura"));f.Tick();Assert.AreEqual(2001,f.Player.GetComp<SkillDirectorComp>().CurrentSkill.Value);Assert.IsTrue(input.TryPeek(out _));}
        }
    }
}
