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
    public sealed class WanderGrowthTests
    {
        static SkillProjectDocument Formal()=>SkillProjectJson.Read(File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/project.json")));
        sealed class Draws:IRandom
        {
            readonly Queue<float> _values;
            public int Count;
            public Draws(params float[] values)=>_values=new Queue<float>(values);
            public float Next01(){Count++;return _values.Count>0?_values.Dequeue():.5f;}
        }
        [Test]
        public void SpawnRulesKeepIntegerRandomOffsetsCumulativeGrowthAndSourceDefinitionsIndependent()
        {
            var content=CombatContentCompiler.Compile(Formal());var definition=content.Runtime.Actors["arena-enemy"];
            var world=new CombatWorld(content.Runtime.CreateFactory(content.Skills));
            try
            {
                world.TryGetActor(world.SpawnActor(new ActorSpawnSpec("arena-enemy")),out var a);var random=new Draws(.9f,.25f);
                definition.SpawnStats.Apply(a,definition,10,random);var attributes=a.GetComp<AttributeSet>();
                Assert.AreEqual(70,attributes.GetBase(AttrId.MaxHp));Assert.AreEqual(70,attributes.GetBase(AttrId.Hp));Assert.AreEqual(38,attributes.GetBase(AttrId.Atk));Assert.AreEqual(55*5.6f/155+.2f,attributes.GetBase(AttrId.MoveSpeed),.00001);
                Assert.AreEqual(2,random.Count);Assert.AreEqual(50,definition.Hp);Assert.AreEqual(15,definition.Attack);
                var zero=new ActorSpawnStats();var unused=new Draws(.1f);zero.Apply(a,definition,0,unused);Assert.AreEqual(1,unused.Count);
                Assert.Throws<ArgumentOutOfRangeException>(()=>zero.Apply(a,definition,-1,unused));
            }
            finally{world.Shutdown();}
        }
        [Test]
        public void ArenaPublishesAlreadySampledStatsAndRefillsAtTheNextLifetimeOrdinal()
        {
            var p=Formal();p.Runtime.Arena.MaxEnemies=2;p.Runtime.Arena.SpawnPeriod=.1f;
            using(var session=new ArenaProject(p).Create())
            {
                var hitpoints=new List<float>();session.World.Events.Subscribe<EvEntitySpawn>(e=>{if(e.BlueprintId=="arena-enemy"){session.World.TryGetActor(e.Id,out var a);hitpoints.Add(a.GetComp<AttributeSet>().GetBase(AttrId.MaxHp));}});
                session.Start();session.World.TryGetActor(session.PlayerId,out var player);Assert.AreEqual(54,player.GetComp<AttributeSet>().GetBase(AttrId.Atk));session.Advance();
                CollectionAssert.AreEqual(new[]{50f,52f},hitpoints);var enemy=session.World.RegistryActive().First(a=>a.Id!=session.PlayerId);enemy.GetComp<StateMachineComp>().TryEnter(ActivityId.Dead,new ActivityEnterArgs{Reason="test"});session.Advance(5);
                CollectionAssert.AreEqual(new[]{50f,52f,54f},hitpoints);Assert.AreEqual(3,session.SpawnedEnemies);Assert.AreEqual(2,session.EnemyCount);
            }
            using(var reset=new ArenaProject(p).Create())
            {reset.Start();reset.Advance();Assert.AreEqual(2,reset.SpawnedEnemies);Assert.AreEqual(50,reset.World.RegistryActive().First(a=>a.Id!=reset.PlayerId).GetComp<AttributeSet>().GetBase(AttrId.MaxHp));}
        }
        [TestCase("range")] [TestCase("growth")] [TestCase("divisor")] [TestCase("curve")] [TestCase("interval")] [TestCase("missing")] [TestCase("wrong-ai")]
        public void InvalidSpawnAndWanderValuesAreRejectedAtTheProtocolBoundary(string fault)
        {
            var p=Formal();var enemy=p.Runtime.Actors.Single(a=>a.Id=="arena-enemy");
            if(fault=="range")enemy.SpawnStats.AttackRandomRange=-1;
            if(fault=="growth")enemy.SpawnStats.HpPerSpawn=float.NaN;
            if(fault=="divisor")enemy.SpawnStats.MoveSpeedCurve.Divisor=0;
            if(fault=="curve")enemy.SpawnStats.MoveSpeedCurve.Scale=float.PositiveInfinity;
            if(fault=="interval")enemy.Wander.ShotIntervalMax=enemy.Wander.ShotIntervalMin-.1f;
            if(fault=="missing")enemy.Wander=null;
            if(fault=="wrong-ai")enemy.Ai="ranged";
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void CompiledProfilesDoNotShareAuthoringObjects()
        {
            var p=Formal();var content=CombatContentCompiler.Compile(p);p.Runtime.Actors.Single(a=>a.Id=="arena-enemy").SpawnStats.MoveSpeedCurve.Scale=200;
            Assert.AreEqual(5.6f,content.Runtime.Actors["arena-enemy"].SpawnStats.MoveSpeedCurve.Scale);
            var old=File.ReadAllText(Path.Combine(Application.streamingAssetsPath,"Combat/f6.json"));Assert.DoesNotThrow(()=>SkillPublicationJson.Read(old));
        }
        sealed class F:IDisposable
        {
            public readonly CombatWorld World;
            public readonly Actor Enemy;
            public readonly Draws Random=new Draws(.75f,.5f,.5f);
            public readonly List<int> Starts=new List<int>();
            readonly CombatContentSnapshot _content;
            public F()
            {
                _content=CombatContentCompiler.Compile(Formal());World=new CombatWorld(_content.Runtime.CreateFactory(_content.Skills),random:Random);World.ReplaceCatalogs(_content.Projectiles,_content.Aoes,_content.Summons);
                Enemy=Spawn();World.Events.Subscribe<EvSkillStarted>(e=>Starts.Add(World.Time.Frame));
            }
            public Actor Spawn(){World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-enemy")),out var actor);actor.GetComp<AttributeSet>().SetBase(AttrId.MoveSpeed,0);return actor;}
            public void Tick(int n){for(int i=0;i<n;i++)World.Tick(.02f);}
            public void Dispose()=>World.Shutdown();
        }
        [Test]
        public void IndependentWanderAndShotTimersUseOriginalTwoAndThreeSecondBoundaries()
        {
            using(var f=new F())
            {
                f.Tick(99);Assert.AreEqual(0,f.Random.Count);Assert.AreEqual(0,f.Starts.Count);f.Tick(1);Assert.AreEqual(2,f.Random.Count);
                var intent=f.Enemy.GetComp<LocomotionComp>().MoveIntent;Assert.AreEqual(Math.Sqrt(.5),intent.X,.00001);Assert.AreEqual(Math.Sqrt(.5),intent.Z,.00001);
                f.Tick(49);Assert.AreEqual(0,f.Starts.Count);f.Tick(1);CollectionAssert.AreEqual(new[]{150},f.Starts);Assert.AreEqual(3,f.Random.Count);
                f.Tick(174);Assert.AreEqual(1,f.Starts.Count);f.Tick(1);CollectionAssert.AreEqual(new[]{150,325},f.Starts);
            }
        }
        [Test]
        public void StunAndIndependentHitstopFreezeTimersWithoutSharingOtherActorsState()
        {
            using(var f=new F())
            {
                f.Tick(75);var second=f.Spawn();f.World.RequestActorHitstop(f.Enemy.Id,50);f.Tick(50);Assert.AreEqual(0,f.Starts.Count);
                f.Enemy.GetComp<TagComp>().Add(CommonTags.Stunned,1,TagSource.Debug);f.Tick(75);Assert.AreEqual(0,f.Starts.Count);Assert.AreEqual(0,f.Enemy.GetComp<LocomotionComp>().MoveIntent.X);
                f.Enemy.GetComp<TagComp>().Remove(CommonTags.Stunned,1,TagSource.Debug);f.Tick(25);Assert.AreEqual(1,f.Starts.Count,"The second actor reaches its own150th step first.");
                f.Tick(50);Assert.AreEqual(2,f.Starts.Count);Assert.IsFalse(f.Enemy.TryGetComp<PerceptionComp>(out _));Assert.IsFalse(second.TryGetComp<PerceptionComp>(out _));
            }
        }
        [Test]
        public void PeriodicFireUsesTheNormalEnergyGuardAndRetriesWithoutConsumingRandomOnFailure()
        {
            using(var f=new F())
            {
                f.Enemy.GetComp<SkillCastComp>().TryCast(new SkillNodeId(2010),new SkillAim(EntityId.Invalid,default,0));f.Starts.Clear();
                var root=new ActPeriodicCast(new SkillNodeId(2010),0,2,5);var ctx=new BtTick(f.Enemy,f.World,f.Enemy.GetComp<BehaviorTreeComp>().Board,.02f);
                root.Tick(ctx);Assert.AreEqual(0,f.Random.Count);Assert.AreEqual(0,f.Starts.Count);
                f.Enemy.GetComp<SkillDirectorComp>().Stop(DirectorStopReason.Manual);f.Enemy.GetComp<TagComp>().Add(CommonTags.BlockSkill,1,TagSource.Debug);root.Tick(ctx);Assert.AreEqual(0,f.Random.Count);
                f.Enemy.GetComp<TagComp>().Remove(CommonTags.BlockSkill,1,TagSource.Debug);root.Tick(ctx);Assert.AreEqual(1,f.Random.Count);Assert.AreEqual(1,f.Starts.Count);
            }
        }
    }
}
