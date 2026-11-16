using System;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class SampledProjectileMotionTests
    {
        sealed class Factory:IActorFactory
        {
            public Actor Create(in ActorSpawnSpec spec)
            {
                var a=new Actor();a.AddComp(new TransformComp());a.AddComp(new TagComp());a.AddComp(new TeamComp(1));
                if(spec.BlueprintId=="projectile")a.AddComp(new ProjectileComp());return a;
            }
            public void Release(Actor actor)=>actor.ResetForPool();
        }
        sealed class F:IDisposable
        {
            public readonly CombatWorld World;public readonly Actor Owner,Shot;public readonly ProjectileComp Body;
            public F(ProjectileDefinition def,ObstacleDefinition[] obstacles=null,float groundY=0)
            {
                World=new CombatWorld(new Factory(),obstacles:obstacles);World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("owner")),out Owner);Owner.GetComp<TransformComp>().Position=new SimVec3(-10,0,0);
                World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("projectile")),out Shot);Body=Shot.GetComp<ProjectileComp>();Body.Setup(def,Owner.Id,0,groundY:groundY);
            }
            public void Tick(int n=1){for(int i=0;i<n;i++)World.Tick(.02f);}
            public void Dispose()=>World.Shutdown();
        }
        sealed class Count:IEffect{public int Value;public void Apply(ref EffectContext ctx)=>Value++;}
        static ScheduledBounceDefinition Curve()=>new ScheduledBounceDefinition{Height=1,Phases=new[]{1f,1.6667f,1.999f}};
        [TestCase(0,.5)] [TestCase(.5,5.5)] [TestCase(1,.5)] [TestCase(1.5,2.89712769)]
        public void SineReturnSamplesTheOriginalSpeedFormula(double age,double speed)
        {Assert.AreEqual(speed,SampledProjectileMotion.ReturnSpeed(5,age,1),.000001);}
        [Test]
        public void StepSampleIsReusedByCollisionSubstepsAndSeparateBodiesKeepTheirOwnAge()
        {
            var d=new ProjectileDefinition{Speed=5,Motion=new ProjectileMotionDefinition{SineReturn=true,ReturnAfter=1}};var a=new ProjectileComp();var b=new ProjectileComp();a.Setup(d,EntityId.Invalid,0);b.Setup(d,EntityId.Invalid,0);a.Age=.5;b.Age=1.5;
            ProjectileKinematics.SampleStep(a);ProjectileKinematics.SampleStep(b);a.Age=.51;
            Assert.AreEqual(5.5,ProjectileKinematics.Speed(a),.000001);Assert.AreEqual(2.89712769,ProjectileKinematics.Speed(b),.000001);Assert.AreEqual(.055,ProjectileKinematics.Distance(a,.01),.000001);
        }
        [Test]
        public void ScheduledHeightsUseSegmentDurationSquaredAndReturnToTheLaunchPlane()
        {
            var d=Curve();Assert.AreEqual(1,SampledProjectileMotion.Height(d,.5),.00001);Assert.AreEqual(0,SampledProjectileMotion.Height(d,1),.00001);
            double duration=d.Phases[1]-d.Phases[0];Assert.AreEqual(duration*duration,SampledProjectileMotion.Height(d,1+duration*.5),.00001);Assert.AreEqual(0,SampledProjectileMotion.Height(d,2));
            var def=new ProjectileDefinition{Speed=3,Lifetime=2,Motion=new ProjectileMotionDefinition{ScheduledBounce=d}};using(var f=new F(def,groundY:2)){f.Tick(25);Assert.AreEqual(3,f.Shot.GetComp<TransformComp>().Position.Y,.0001);}
        }
        [Test]
        public void ScheduledLandingsFireOnceAtTheirTimesAndLifetimeStillEndsTheProjectile()
        {
            var effect=new Count();var def=new ProjectileDefinition{Speed=3,Lifetime=2,OnHit=new IEffect[]{new PierceEffect(0)},HitRadius=.1f,Motion=new ProjectileMotionDefinition{ScheduledBounce=Curve()},OnBounce=new IEffect[]{effect}};
            using(var f=new F(def))
            {
                int ended=0;f.World.Events.Subscribe<EvProjectileEnded>(e=>{ended++;Assert.AreEqual(ProjectileEndReason.Lifetime,e.Reason);});
                f.Tick(50);Assert.AreEqual(1,effect.Value);Assert.AreEqual(0,f.Shot.GetComp<TransformComp>().Position.Y,.0001);f.Tick(34);Assert.AreEqual(2,effect.Value);f.Tick(16);Assert.AreEqual(3,effect.Value);Assert.AreEqual(1,ended);Assert.IsFalse(f.Shot.IsActive);
            }
        }
        [Test]
        public void IndependentHitstopFreezesScheduledAgePositionAndTheNextGroundEvent()
        {
            var effect=new Count();var def=new ProjectileDefinition{Speed=3,Lifetime=2,Motion=new ProjectileMotionDefinition{ScheduledBounce=Curve()},OnBounce=new IEffect[]{effect}};
            using(var f=new F(def))
            {f.Tick(25);var p=f.Shot.GetComp<TransformComp>().Position;double age=f.Body.Age;f.World.RequestActorHitstop(f.Shot.Id,5);f.Tick(5);Assert.AreEqual(age,f.Body.Age);Assert.AreEqual(p.Y,f.Shot.GetComp<TransformComp>().Position.Y);Assert.AreEqual(0,effect.Value);f.Tick(25);Assert.AreEqual(1,effect.Value);}
        }
        [Test]
        public void AProjectileCanStopAtAWallAndContinueItsAgeWithoutWallTerminationEffects()
        {
            var wall=new Count();var end=new Count();var def=new ProjectileDefinition{Speed=10,Lifetime=.2f,HitRadius=.1f,RemoveOnObstacle=false,OnWall=new IEffect[]{wall},OnExpire=new IEffect[]{end}};
            using(var f=new F(def,new[]{new ObstacleDefinition("wall",1,0,.2f,4)}))
            {f.Tick(5);Assert.IsTrue(f.Shot.IsActive);Assert.LessOrEqual(f.Shot.GetComp<TransformComp>().Position.X,.8001);double age=f.Body.Age;f.Tick();Assert.Greater(f.Body.Age,age);Assert.AreEqual(0,f.Body.Velocity.X,.001);f.Tick(4);Assert.IsFalse(f.Shot.IsActive);Assert.AreEqual(0,wall.Value);Assert.AreEqual(1,end.Value);}
        }
        [Test]
        public void AStoppedScheduledProjectileStillPublishesEveryLandingAndExpires()
        {
            var effects=new Count();var def=new ProjectileDefinition{Speed=10,Lifetime=2,HitRadius=.1f,RemoveOnObstacle=false,Motion=new ProjectileMotionDefinition{ScheduledBounce=Curve()},OnBounce=new IEffect[]{effects}};
            using(var f=new F(def,new[]{new ObstacleDefinition("wall",1,0,.2f,4)})){f.Tick(100);Assert.AreEqual(3,effects.Value);Assert.IsFalse(f.Shot.IsActive);}
        }
        [TestCase(HitFeedbackLevel.None,1,0,0)] [TestCase(HitFeedbackLevel.Light,1,1,2)] [TestCase(HitFeedbackLevel.ComboConfirm,1,1,2)] [TestCase(HitFeedbackLevel.ComboConfirm,2,1,3)] [TestCase(HitFeedbackLevel.Heavy,1,0,3)] [TestCase(HitFeedbackLevel.Finisher,1,2,4)]
        public void FeedbackDerivedTimingIsGenericAndUsesTheProjectileContactIndex(HitFeedbackLevel level,int index,int source,int target)
        {Assert.AreEqual(source,HitFeedbackTiming.Source(level));Assert.AreEqual(target,HitFeedbackTiming.Target(level,index));}
    }
}
