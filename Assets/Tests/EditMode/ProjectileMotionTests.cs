using System;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class ProjectileMotionTests
    {
        [TestCase(1)]
        [TestCase(50)]
        public void AccelerationIntegratesToTheSpeedCapThenTravelsAtConstantSpeed(int parts)
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=2;def.Motion=new ProjectileMotionDefinition{Acceleration=4,MaxSpeed=4};
            var w=new ProjectileMotionTestWorld(def);w.Fire();w.Advance(1,parts);
            Assert.AreEqual(3.5,w.Body.GetComp<TransformComp>().Position.X,.00001);Assert.AreEqual(3.5,w.Body.GetComp<ProjectileComp>().Distance,.00001);
            Assert.AreEqual(4,ProjectileKinematics.Speed(w.Body.GetComp<ProjectileComp>()),.00001);
        }

        [TestCase(1)]
        [TestCase(100)]
        public void DecelerationStopsWithoutReversingAndStillExpires(int parts)
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.Lifetime=3;def.Motion=new ProjectileMotionDefinition{Acceleration=-4,MaxSpeed=10};
            var w=new ProjectileMotionTestWorld(def);w.Fire();w.Advance(2,parts);
            Assert.AreEqual(2,w.Body.GetComp<TransformComp>().Position.X,.00001);Assert.AreEqual(0,ProjectileKinematics.Speed(w.Body.GetComp<ProjectileComp>()));
            w.Advance(1,50);Assert.AreEqual(ProjectileEndReason.Lifetime,w.Ended.Single().Reason);
        }

        [Test]
        public void AcceleratedCollisionAndRangeUseInverseTravelTime()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=2;def.Range=4;def.Motion=new ProjectileMotionDefinition{Acceleration=4,MaxSpeed=20};
            var w=new ProjectileMotionTestWorld(def);def.OnRange=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};
            w.Fire();w.World.Tick(2);Assert.AreEqual(4,w.Captures.Single().Point.X,.00001);Assert.AreEqual(ProjectileEndReason.Range,w.Ended.Single().Reason);
            def.Range=100;def.MaxHits=1;w=new ProjectileMotionTestWorld(def);w.Spawn("target",4);
            def.OnHit=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};
            w.Fire();w.World.Tick(2);Assert.AreEqual(3.9,w.Captures.Single().Point.X,.00001);
        }

        [TestCase(1)]
        [TestCase(100)]
        public void ReturnSwitchesAtTheExactTimeAndCatchesTheOwnerWithoutDoubleHitting(int parts)
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.Motion=new ProjectileMotionDefinition{ReturnAfter=.5f,ReturnSpeed=8,CatchRadius=.1f};
            var w=new ProjectileMotionTestWorld(def);w.Spawn("target",1);
            def.OnReturned=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};
            w.Fire();w.Advance(1,parts);
            Assert.AreEqual(1,w.Behaviors.Count(e=>e.Kind=="returning"));Assert.AreEqual(1,w.DamageCount);
            Assert.AreEqual(ProjectileEndReason.Returned,w.Ended.Single().Reason);Assert.AreEqual(.1,w.Captures.Single().Point.X,.0001);
            Assert.IsNull(w.Body);
        }

        [Test]
        public void ReturningBodyFollowsTheCurrentOwnerPositionAndUsesTheSamePathBudget()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.Motion=new ProjectileMotionDefinition{ReturnAfter=.5f,ReturnSpeed=4,CatchRadius=.1f};
            var w=new ProjectileMotionTestWorld(def);w.Fire();w.Advance(.5f,25);
            w.World.TryGetActor(w.Owner,out var owner);owner.GetComp<TransformComp>().Position=new SimVec3(-2,0,1);
            w.Advance(2,100);Assert.AreEqual(ProjectileEndReason.Returned,w.Ended.Single().Reason);
            Assert.AreEqual(.1,Math.Sqrt(SpatialGeometry.DistanceSquared(w.Ended.Single().Point,owner.GetComp<TransformComp>().Position)),.0001);
            def.Range=3;w=new ProjectileMotionTestWorld(def);w.Fire();w.Advance(2,100);
            Assert.AreEqual(ProjectileEndReason.Range,w.Ended.Single().Reason);
        }

        [Test]
        public void OwnerRemovalCleansReturnWithoutBindingAReplacementAtTheSameIndex()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Motion=new ProjectileMotionDefinition{ReturnAfter=.1f};
            var w=new ProjectileMotionTestWorld(def);w.Fire();w.World.Tick(.04f);w.World.RequestDespawn(w.Owner);w.World.Tick(0);
            var replacement=w.Spawn("owner",20);Assert.AreNotEqual(w.Owner,replacement);
            w.World.Tick(2);Assert.IsNull(w.Body);Assert.AreEqual(0,w.Ended.Count);
        }

        [TestCase(1)]
        [TestCase(100)]
        public void GroundBouncesUseGravityRestitutionAndAllTheRemainingStep(int parts)
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.Motion=new ProjectileMotionDefinition{Bounce=new ProjectileBounceDefinition{LaunchHeight=0,VerticalSpeed=4,Gravity=8,Restitution=.5f,Count=1}};
            var w=new ProjectileMotionTestWorld(def);def.OnBounce=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};
            w.Fire();w.Advance(1.25f,parts);
            Assert.AreEqual(1,w.Behaviors.Count(e=>e.Kind=="ground-bounce"));Assert.AreEqual(5,w.Body.GetComp<TransformComp>().Position.X,.0001);
            Assert.AreEqual(.25,w.Body.GetComp<TransformComp>().Position.Y,.0001);Assert.AreEqual(0,w.Body.GetComp<ProjectileComp>().VerticalSpeed,.0001);
            Assert.AreEqual(0,w.Captures.Single().Point.Y,.0001);w.Advance(.25f,25);
            Assert.AreEqual(ProjectileEndReason.Ground,w.Ended.Single().Reason);Assert.AreEqual(6,w.Ended.Single().Point.X,.0001);
        }

        [Test]
        public void ZeroRestitutionAndZeroLaunchEndWithoutInfiniteZeroTimeBounces()
        {
            foreach(float vertical in new[]{0,4})
            {
                var def=ProjectileMotionTestWorld.Definition();def.Motion=new ProjectileMotionDefinition{Bounce=new ProjectileBounceDefinition{LaunchHeight=0,VerticalSpeed=vertical,Gravity=8,Restitution=0,Count=32}};
                var w=new ProjectileMotionTestWorld(def);w.Fire();w.World.Tick(3);Assert.AreEqual(1,w.Ended.Count);
                Assert.AreEqual(ProjectileEndReason.Ground,w.Ended[0].Reason);Assert.IsFalse(w.Behaviors.Any(e=>e.Kind=="ground-bounce"));
            }
        }

        [Test]
        public void SplitStartsAtImpactHeightAndInheritsInstantVerticalVelocity()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=4;def.MaxHits=1;
            def.Motion=new ProjectileMotionDefinition{Bounce=new ProjectileBounceDefinition{LaunchHeight=0,VerticalSpeed=4,Gravity=8,Count=1}};
            def.OnHit=new IEffect[]{new SplitProjectileEffect(1,0,1,.5f,false,20)};
            var w=new ProjectileMotionTestWorld(def);w.Spawn("target",1.1f);w.Fire();w.World.Tick(.25f);
            Assert.AreEqual(.75,w.SpawnPositions.Count==1?w.Ended.Single().Point.Y:0,.0001);
            w.World.Tick(.01f);Assert.AreEqual(2,w.SpawnPositions.Count);Assert.AreEqual(.75,w.SpawnPositions[1].Y,.0001);
            Assert.AreEqual(.7696,w.Body.GetComp<TransformComp>().Position.Y,.0001);
            Assert.AreEqual(1.92,w.Body.GetComp<ProjectileComp>().VerticalSpeed,.0001);
        }

        [Test]
        public void HitstopFreezesPlanarVerticalAndReturnClocksTogether()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Motion=new ProjectileMotionDefinition{ReturnAfter=.5f,Bounce=new ProjectileBounceDefinition()};
            var w=new ProjectileMotionTestWorld(def);w.Fire();w.World.Tick(.1f);
            var transform=w.Body.GetComp<TransformComp>();var body=w.Body.GetComp<ProjectileComp>();var position=transform.Position;double age=body.Age;
            w.World.RequestHitstop(5);w.Advance(1,5);
            Assert.AreEqual(position.X,transform.Position.X);Assert.AreEqual(position.Y,transform.Position.Y);Assert.AreEqual(age,body.Age);Assert.IsFalse(body.Returning);
        }
    }
}
