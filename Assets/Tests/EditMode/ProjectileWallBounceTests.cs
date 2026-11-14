using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class ProjectileWallBounceTests
    {
        static ObstacleDefinition[] Walls()=>new[]{new ObstacleDefinition("right",2.5f,0,1,8),new ObstacleDefinition("left",-2.5f,0,1,8)};

        [TestCase(1)]
        [TestCase(100)]
        public void WallReflectionConsumesResidualTimeAndOnlyEndsWhenItsQuotaIsExhausted(int parts)
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.Motion=new ProjectileMotionDefinition{WallBounces=2};
            var w=new ProjectileMotionTestWorld(def,Walls());def.OnWall=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};
            w.Fire();w.Advance(1,parts);Assert.AreEqual(-.2001,w.Body.GetComp<TransformComp>().Position.X,.001);
            Assert.AreEqual(1,w.Behaviors.Count(e=>e.Kind=="wall-bounce"));Assert.AreEqual(0,w.Captures.Count);
            w.Advance(2,100);Assert.AreEqual(2,w.Behaviors.Count(e=>e.Kind=="wall-bounce"));
            Assert.AreEqual(1,w.Captures.Count);Assert.AreEqual(ProjectileEndReason.Wall,w.Ended.Single().Reason);
        }

        [Test]
        public void AcceleratedReflectionUsesContactTimeInsteadOfDistanceFraction()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=2;def.Motion=new ProjectileMotionDefinition{Acceleration=4,MaxSpeed=20,WallBounces=1};
            var w=new ProjectileMotionTestWorld(def,Walls());w.Fire();w.World.Tick(1);
            Assert.AreEqual(-.2001,w.Body.GetComp<TransformComp>().Position.X,.001);
            Assert.AreEqual(4,w.Body.GetComp<ProjectileComp>().Distance,.0001);Assert.AreEqual(1,w.Body.GetComp<ProjectileComp>().Age,.0001);
        }

        [Test]
        public void ReflectedProjectileCanHitABehindTargetAndKeepsItsHitHistory()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.Motion=new ProjectileMotionDefinition{WallBounces=1};
            var w=new ProjectileMotionTestWorld(def,Walls());w.Spawn("target",1);w.Spawn("target",-1);
            w.Fire();w.Advance(2,100);Assert.AreEqual(2,w.DamageCount);Assert.AreEqual(1,w.Behaviors.Count(e=>e.Kind=="wall-bounce"));
            Assert.AreEqual(ProjectileEndReason.Wall,w.Ended.Single().Reason);
        }

        [Test]
        public void InitialOverlapWithoutAUsefulNormalTerminatesWithoutAnInfiniteLoop()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Motion=new ProjectileMotionDefinition{WallBounces=64};
            var w=new ProjectileMotionTestWorld(def,new[]{new ObstacleDefinition("overlap",0,0,1,1)});
            w.Fire();w.World.Tick(10);Assert.IsNull(w.Body);Assert.AreEqual(1,w.Ended.Count);
            Assert.AreEqual(0,w.Behaviors.Count(e=>e.Kind=="wall-bounce"));
        }

        [Test]
        public void RoundCornerUsesTheSweepNormalAndDoesNotTeleportAcrossTheObstacle()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.HitRadius=.5f;def.Motion=new ProjectileMotionDefinition{WallBounces=1};
            var w=new ProjectileMotionTestWorld(def,new[]{new ObstacleDefinition("corner",2,1,1,1)});
            w.Fire(15);w.Advance(1,100);var p=w.Body.GetComp<TransformComp>().Position;
            Assert.IsTrue(w.World.Collision.IsFree(p,.5f));Assert.AreEqual(1,w.Behaviors.Count(e=>e.Kind=="wall-bounce"));
        }
    }
}
