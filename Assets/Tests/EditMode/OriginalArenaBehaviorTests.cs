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
    public sealed class OriginalArenaBehaviorTests
    {
        sealed class F:IDisposable
        {
            public readonly CombatContentSnapshot Content;
            public readonly CombatWorld World;
            public readonly Actor Player,Target;
            public readonly List<EvCue> Cues=new List<EvCue>();
            public readonly List<EvDamage> Damage=new List<EvDamage>();
            public F(float targetX=1)
            {
                var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
                Content=CombatContentCompiler.Compile(p);World=new CombatWorld(Content.Runtime.CreateFactory(Content.Skills),random:new FixedRandom(.9f));Content.Runtime.Data.Install(World);
                World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-player")),out Player);World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-target")),out Target);
                Target.GetComp<TransformComp>().Position=new SimVec3(targetX,0,0);World.Events.Subscribe<EvCue>(e=>Cues.Add(e));World.Events.Subscribe<EvDamage>(e=>Damage.Add(e));
            }
            public void Tick(int n=1){for(int i=0;i<n;i++)World.Tick(.02f);}
            public bool Cast(int id)=>Player.GetComp<SkillCastComp>().TryCast(new SkillNodeId(id),new SkillAim(EntityId.Invalid,new SimVec3(20,0,0),0));
            public void Dispose()=>World.Shutdown();
        }
        [Test]
        public void GroundFireAndReturningBurnCoexistWithDifferentPeriodsAndUseLiveSourceAttack()
        {
            using(var f=new F())
            {
                Assert.IsTrue(f.Content.Buffs.TryGet(5102,out var returning));Assert.IsTrue(f.Content.Buffs.TryGet(5103,out var ground));
                var b=f.Target.GetComp<BuffComp>();b.Apply(returning,f.Player,3);b.Apply(ground,f.Player);f.Player.GetComp<AttributeSet>().SetBase(AttrId.Atk,100);
                f.Tick(19);Assert.AreEqual(0,f.Damage.Count);f.Tick();Assert.AreEqual(5,f.Damage.Single().Amount,.0001f);f.Tick(5);Assert.AreEqual(18,f.Damage.Last().Amount,.0001f);
                f.Tick(126);Assert.AreEqual(110,f.Damage.Sum(e=>e.Amount),.001f);Assert.AreEqual(0,b.Snapshot().Length);
                Assert.AreEqual(4,f.Damage.Count(e=>Math.Abs(e.Amount-5)<.001));Assert.AreEqual(5,f.Damage.Count(e=>Math.Abs(e.Amount-18)<.001));
            }
        }
        [Test]
        public void LayeredBurnUsesDistinctKeysAndRemovalStopsEveryPossibleTier()
        {
            using(var f=new F())
            {
                f.Content.Buffs.TryGet(5102,out var burn);var b=f.Target.GetComp<BuffComp>();
                for(int i=0;i<3;i++)b.Apply(burn,f.Player);
                CollectionAssert.AreEqual(new[]{"buff_5102_1","buff_5102_2","buff_5102_3"},f.Cues.Where(e=>e.Playback==CuePlayback.Start).Select(e=>e.InstanceKey));
                Assert.IsTrue(b.RemoveInstance(b.Snapshot().Single().InstanceId));
                CollectionAssert.AreEqual(new[]{"buff_5102_1","buff_5102_2","buff_5102_3"},f.Cues.Where(e=>e.Playback==CuePlayback.Stop).Select(e=>e.InstanceKey));
                Assert.IsTrue(f.Cues.All(e=>e.Source==f.Target.Id&&e.Anchor=="Body"));
            }
        }
        [Test]
        public void NormalContactCueAnchorsToTheVictimAndDoesNotRunTheLifetimeBranch()
        {
            using(var f=new F())
            {
                Assert.IsTrue(f.Cast(2001));f.Tick(30);var cue=f.Cues.Single(e=>e.CueId==4204);
                Assert.AreEqual(f.Target.Id,cue.Source);Assert.AreEqual("Body",cue.Anchor);Assert.AreEqual(1,f.Damage.Count);
            }
        }
        [Test]
        public void MissingTargetLetsTheOriginalLifetimeCueRunExactlyOnceAtTheTerminalPoint()
        {
            using(var f=new F(200))
            {
                f.Content.Projectiles.TryGet(4001,out var bullet);bullet.Lifetime=.1f;
                Assert.IsTrue(f.Cast(2001));f.Tick(12);var cue=f.Cues.Single(e=>e.CueId==4204);
                Assert.AreEqual(f.Player.Id,cue.Source);Assert.IsTrue(cue.HasPoint);Assert.Greater(cue.Point.X,.4f);Assert.AreEqual(0,f.Damage.Count);
            }
        }
        [Test]
        public void OrbRetainsHeavyFeedbackAndTheOriginalThreeFrameTargetStop()
        {
            using(var f=new F(.6f))
            {
                Assert.IsTrue(f.Cast(2003));f.Tick(6);Assert.AreEqual(HitFeedbackLevel.Heavy,f.Damage.Single().Result.Feedback);
                Assert.AreEqual(3,f.Target.Time.HitstopLeft);f.Tick();Assert.IsTrue(f.World.IsActorStopped(f.Target));Assert.AreEqual(2,f.Target.Time.HitstopLeft);
            }
        }
        [Test]
        public void FractionalDamageEventsKeepNumbersStructuredAndTargetLabelsCultureIndependent()
        {
            var p=SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
            using(var s=new SkillSimulation(p,CombatContentCompiler.Compile(p)))
            {
                s.Execute(new SkillSimulationCommand{Kind="cast",SkillId="arena-returning-shot",X=4,Z=0});s.Advance(300);
                var damage=s.Snapshot().Events.Where(e=>e.Kind=="damage"||e.Kind=="critical").ToArray();Assert.Greater(damage.Length,2);
                Assert.IsTrue(damage.All(e=>e.Result==e.Damage.Target));Assert.IsTrue(damage.Any(e=>e.Damage.HpLost>0&&e.Damage.HpLost<1));
                using(var run=new SkillReplayRun(s.CaptureReplay("arena-returning-shot")))Assert.IsTrue(run.Comparison.Matches);
            }
        }
    }
}
