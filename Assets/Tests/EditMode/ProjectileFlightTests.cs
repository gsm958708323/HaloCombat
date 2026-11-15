using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class ProjectileFlightTests
    {
        static ProjectileDefinition Definition(ProjectileFlightClock clock=ProjectileFlightClock.Time,float interval=.2f)
        {
            var def=ProjectileMotionTestWorld.Definition();def.Flight=new ProjectileFlightDefinition{Clock=clock,Interval=interval};return def;
        }
        static List<EvProjectileFlight> Listen(ProjectileMotionTestWorld w)
        {var list=new List<EvProjectileFlight>();w.World.Events.Subscribe<EvProjectileFlight>(e=>list.Add(e));return list;}

        [TestCase(1)]
        [TestCase(100)]
        public void TimeTriggersUseActualPositionsAndNeverFireAtBirth(int parts)
        {
            var w=new ProjectileMotionTestWorld(Definition());var flights=Listen(w);w.Fire();w.World.Tick(0);Assert.IsEmpty(flights);
            w.Advance(1,parts);Assert.AreEqual(5,flights.Count);
            for(int i=0;i<flights.Count;i++)
            {Assert.AreEqual(i+1,flights[i].Index);Assert.AreEqual((i+1)*.2,flights[i].Age,.00001);Assert.AreEqual((i+1)*.8,flights[i].Point.X,.00001);}
            Assert.AreEqual(1,w.Body.GetComp<ProjectileComp>().Age,.00001);
        }

        [TestCase(1)]
        [TestCase(100)]
        public void DistanceTriggersInvertAcceleratedTravelRatherThanInterpolatingTime(int parts)
        {
            var def=Definition(ProjectileFlightClock.Distance,1);def.Speed=2;def.Motion=new ProjectileMotionDefinition{Acceleration=4,MaxSpeed=20};
            var w=new ProjectileMotionTestWorld(def);var flights=Listen(w);w.Fire();w.Advance(1,parts);Assert.AreEqual(4,flights.Count);
            for(int i=0;i<flights.Count;i++)
            {Assert.AreEqual(i+1,flights[i].Distance,.00001);Assert.AreEqual(i+1,flights[i].Point.X,.00001);Assert.AreEqual((Math.Sqrt(4+8*(i+1))-2)/4,flights[i].Age,.00001);}
        }

        [TestCase(10,2)]
        [TestCase(1000,200)]
        public void DistanceCadenceDoesNotSlipOneFixedStepBecauseOfFloatTimeRoundoff(float speed,float interval)
        {
            var def=Definition(ProjectileFlightClock.Distance,interval);def.Speed=speed;def.Range=0;
            var w=new ProjectileMotionTestWorld(def);var flights=Listen(w);w.Fire();w.Advance(1,50);
            Assert.AreEqual(5,flights.Count);Assert.AreEqual(1,flights.Last().Age,.00001);Assert.AreEqual(speed,flights.Last().Distance,.0001);
        }

        [TestCase(ProjectileFlightClock.Time,4)]
        [TestCase(ProjectileFlightClock.Distance,0)]
        public void AStationaryBallAdvancesTimeWithoutInventingDistance(ProjectileFlightClock clock,int count)
        {
            var def=Definition(clock);def.Speed=0;def.Lifetime=1;var w=new ProjectileMotionTestWorld(def);var flights=Listen(w);w.Fire();w.World.Tick(1);
            Assert.AreEqual(count,flights.Count);Assert.IsTrue(flights.All(e=>e.Distance==0&&e.Point.X==0));Assert.AreEqual(ProjectileEndReason.Lifetime,w.Ended.Single().Reason);
        }

        [Test]
        public void ReturnAtTheTriggerBoundaryChangesDirectionBeforeDeliveringTheEffect()
        {
            var def=Definition(interval:.25f);def.Motion=new ProjectileMotionDefinition{ReturnAfter=.5f,ReturnSpeed=4,CatchRadius=.1f};
            var w=new ProjectileMotionTestWorld(def);var order=new List<string>();
            w.World.Events.Subscribe<EvProjectileBehavior>(e=>order.Add(e.Kind));w.World.Events.Subscribe<EvProjectileFlight>(e=>order.Add("flight-"+e.Index));
            def.OnFlight=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};w.Fire();w.World.Tick(.75f);
            Assert.Less(order.IndexOf("returning"),order.IndexOf("flight-2"));Assert.AreEqual(3,w.Captures.Count);
            Assert.AreEqual(2,w.Captures[1].Point.X,.00001);Assert.AreEqual(-1,w.Captures[1].Dir.X,.00001);Assert.AreEqual(1,w.Captures[2].Point.X,.00001);
        }

        [Test]
        public void ReflectionAtTheTriggerBoundaryUsesTheReflectedDirection()
        {
            var def=Definition(ProjectileFlightClock.Distance,1.9f);def.Motion=new ProjectileMotionDefinition{WallBounces=1};
            var w=new ProjectileMotionTestWorld(def,new[]{new ObstacleDefinition("wall",2.5f,0,1,8)});def.OnFlight=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};
            var order=new List<string>();w.World.Events.Subscribe<EvProjectileBehavior>(e=>order.Add(e.Kind));w.World.Events.Subscribe<EvProjectileFlight>(e=>order.Add("flight"));
            w.Fire();w.World.Tick(.5f);Assert.AreEqual(1,w.Captures.Count);Assert.Less(order.IndexOf("wall-bounce"),order.IndexOf("flight"));Assert.AreEqual(-1,w.Captures[0].Dir.X,.00001);
        }

        [TestCase("range")]
        [TestCase("lifetime")]
        [TestCase("hit")]
        [TestCase("wall")]
        public void TerminationWinsOverAFlightTriggerAtTheSameBoundary(string reason)
        {
            var def=Definition(interval:.25f);ObstacleDefinition[] walls=null;
            if(reason=="range")def.Range=1;if(reason=="lifetime")def.Lifetime=.25f;
            if(reason=="wall")walls=new[]{new ObstacleDefinition("wall",1.6f,0,1,8)};
            var w=new ProjectileMotionTestWorld(def,walls);if(reason=="hit"){def.MaxHits=1;w.Spawn("target",1.1f);}
            var flights=Listen(w);w.Fire();w.World.Tick(.5f);Assert.IsEmpty(flights);Assert.AreEqual(1,w.Ended.Count);
        }

        [TestCase(ProjectileFlightClock.Time,5)]
        [TestCase(ProjectileFlightClock.Distance,1)]
        public void ABlockedBallKeepsItsTimeClockAndStopsItsDistanceClock(ProjectileFlightClock clock,int count)
        {
            var def=Definition(clock,clock==ProjectileFlightClock.Time?.2f:1);def.RemoveOnObstacle=false;
            var w=new ProjectileMotionTestWorld(def,new[]{new ObstacleDefinition("wall",2.5f,0,1,8)});var flights=Listen(w);w.Fire();w.World.Tick(1);
            Assert.AreEqual(count,flights.Count);Assert.AreEqual(1.9,w.Body.GetComp<TransformComp>().Position.X,.00001);Assert.AreEqual(1,w.Body.GetComp<ProjectileComp>().Age,.00001);
        }

        [Test]
        public void HitstopFreezesBothFlightClockAndTriggerSequence()
        {
            var w=new ProjectileMotionTestWorld(Definition());var flights=Listen(w);w.Fire();w.World.Tick(.2f);w.World.RequestHitstop(3);w.Advance(.6f,3);
            Assert.AreEqual(1,flights.Count);Assert.AreEqual(.2,w.Body.GetComp<ProjectileComp>().Age,.00001);w.World.Tick(.2f);Assert.AreEqual(2,flights.Count);Assert.AreEqual(2,flights[1].Index);
        }

        [Test]
        public void ThePerBallStepLimitSkipsBacklogAndAllowsTheNextStepToContinue()
        {
            var w=new ProjectileMotionTestWorld(Definition(interval:.001f));var flights=Listen(w);w.Fire();w.World.Tick(1);
            Assert.AreEqual(64,flights.Count);Assert.AreEqual(1,w.Behaviors.Count(e=>e.Kind=="flight-limit"));Assert.AreEqual(1,w.Body.GetComp<ProjectileComp>().Age,.00001);
            w.World.Tick(.001f);Assert.AreEqual(65,flights.Count);Assert.AreEqual(1001,flights.Last().Index);Assert.AreEqual(1.001,flights.Last().Age,.00001);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RemovingTheBallFromAnEventOrEffectStopsAllLaterTriggers(bool fromEvent)
        {
            var def=Definition();var w=new ProjectileMotionTestWorld(def);var flights=Listen(w);var body=EntityId.Invalid;
            if(fromEvent)w.World.Events.Subscribe<EvProjectileFlight>(e=>w.World.RequestDespawn(e.Projectile));
            else def.OnFlight=new IEffect[]{new CallbackEffect(()=>w.World.RequestDespawn(body))};
            w.Fire();w.World.Tick(0);body=w.Body.Id;w.World.Tick(1);Assert.AreEqual(1,flights.Count);Assert.IsNull(w.Body);
        }

        [Test]
        public void SeparateBallsHaveIndependentCadenceAndPooledSetupClearsTheLimit()
        {
            var w=new ProjectileMotionTestWorld(Definition(interval:.001f));var flights=Listen(w);w.Fire();w.Fire(90);w.World.Tick(1);
            Assert.AreEqual(128,flights.Count);Assert.AreEqual(2,flights.Select(e=>e.Projectile).Distinct().Count());
            var body=w.Body.GetComp<ProjectileComp>();body.Setup(Definition(),w.Owner,0);
            w.World.Tick(.2f);Assert.AreEqual(1,flights.Last(e=>e.Projectile==w.Body.Id).Index);
        }

        [Test]
        public void FlightEffectsReceiveTheOwnerPointDirectionAttackAndPowerWithoutAHitTarget()
        {
            var def=Definition();var w=new ProjectileMotionTestWorld(def);def.OnFlight=new IEffect[]{new ProjectileMotionTestWorld.Capture(w.Captures)};
            w.Fire();w.World.Tick(0);w.Body.GetComp<ProjectileComp>().Setup(def,w.Owner,123,power:3);w.World.Tick(.2f);
            var context=w.Captures.Single();Assert.AreEqual(w.Owner,context.Source.Id);Assert.IsNull(context.Target);Assert.IsTrue(context.HasPoint);Assert.IsTrue(context.HasDir);
            Assert.AreEqual(.8,context.Point.X,.00001);Assert.AreEqual(1,context.Dir.X,.00001);Assert.AreEqual(123,context.SnapshotAtk);Assert.AreEqual(3,context.Power);Assert.IsNull(context.ProjectileHit);
        }
    }
}
