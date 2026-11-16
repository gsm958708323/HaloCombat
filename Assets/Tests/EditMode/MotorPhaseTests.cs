using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class MotorPhaseTests
    {
        sealed class Factory:IActorFactory
        {
            readonly TimelineLibrary _library;
            public Factory(TimelineLibrary library)=>_library=library;
            public Actor Create(in ActorSpawnSpec spec)
            {
                var a=new Actor();a.AddComp(new TransformComp());a.AddComp(new TagComp());
                a.AddComp(new TeamComp(spec.BlueprintId.StartsWith("target")?2:1));
                var stats=new AttributeSet();stats.InitFighterDefaults();stats.SetBase(AttrId.MaxHp,1000);stats.SetBase(AttrId.Hp,1000);stats.SetBase(AttrId.MoveSpeed,10);a.AddComp(stats);
                a.AddComp(new HealthComp());a.AddComp(new StateMachineComp());a.AddComp(new LocomotionComp());a.AddComp(new HitboxComp());a.AddComp(new SkillDirectorComp(_library));return a;
            }
            public void Release(Actor a)=>a.ResetForPool();
        }
        sealed class Fixture:IDisposable
        {
            public readonly TimelineLibrary Library=new TimelineLibrary();
            public readonly CombatWorld World;
            public readonly Actor Source,Target;
            public LocomotionComp Motion=>Source.GetComp<LocomotionComp>();
            public TransformComp Transform=>Source.GetComp<TransformComp>();
            public SkillDirectorComp Director=>Source.GetComp<SkillDirectorComp>();
            public float Hp=>Target.GetComp<AttributeSet>().GetBase(AttrId.Hp);
            public Fixture(bool targetFirst=false,ObstacleDefinition[] obstacles=null)
            {
                World=new CombatWorld(new Factory(Library),obstacles:obstacles);
                if(targetFirst){Target=Spawn("target");Source=Spawn("source");}
                else{Source=Spawn("source");Target=Spawn("target");}
                Target.GetComp<TransformComp>().Position=new SimVec3(10,0,0);
            }
            public Actor Spawn(string key){World.TryGetActor(World.SpawnActor(new ActorSpawnSpec(key)),out var a);return a;}
            public void Play(params TimelineClip[] clips)
            {
                Library.Register(new TimelineDefinition{Id=new TimelineId(9901),Duration=.02f,Clips=clips});
                Assert.IsTrue(Director.Play(new SkillNodeId(9901),new TimelineId(9901)));
            }
            public void Tick(int count=1){for(int i=0;i<count;i++)World.Tick(.02f);}
            public void Dispose()=>World.Shutdown();
        }
        static IEffect[] Damage(float push=0)=>new IEffect[]{new DamageEffect{Flat=10,Coeff=0},new KnockbackEffect{Distance=push}};
        static TimelineClip Move(float distance)=>new TimelineClip{Kind=ClipKind.Move,Start=0,End=.02f,MoveX=distance};
        static TimelineClip Hitbox()=>new TimelineClip{Kind=ClipKind.Hitbox,Start=0,End=.02f,HitRadius=.3f,OnHit=Damage()};

        [Test]
        public void TheFinalAuthoredMoveAndHitboxStepHitsBeforeNaturalCompletion()
        {
            using(var f=new Fixture())
            {
                var order=new List<string>();f.World.Events.Subscribe<EvDamage>(e=>order.Add("damage"));f.World.Events.Subscribe<EvSkillEnded>(e=>order.Add("end"));
                f.Target.GetComp<TransformComp>().Position=new SimVec3(1.2f,0,0);
                f.Play(Move(1),Hitbox());f.Tick();
                Assert.AreEqual(1,f.Transform.Position.X,.00001);Assert.AreEqual(990,f.Hp);
                CollectionAssert.AreEqual(new[]{"damage","end"},order);Assert.IsFalse(f.Director.IsPlaying);Assert.IsFalse(f.Source.GetComp<HitboxComp>().IsOpen);
                f.Tick();Assert.AreEqual(1,f.Transform.Position.X,.00001);Assert.AreEqual(990,f.Hp);
            }
        }

        [TestCase(ClipKind.Hitbox)] [TestCase(ClipKind.Melee)]
        public void MerelyReachingAClipStartCannotProduceAZeroDurationContact(ClipKind kind)
        {
            using(var f=new Fixture())
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(.2f,0,0);
                var clip=Hitbox();clip.Kind=kind;clip.Start=.02f;clip.End=.04f;clip.Shape=HitShape.Circle;clip.Length=.3f;
                f.Library.Register(new TimelineDefinition{Id=new TimelineId(9901),Duration=.04f,Clips=new[]{clip}});
                f.Director.Play(new SkillNodeId(9901),new TimelineId(9901));f.Tick();Assert.AreEqual(1000,f.Hp);Assert.IsFalse(f.Source.GetComp<HitboxComp>().IsOpen);
                f.Tick();Assert.AreEqual(990,f.Hp);Assert.IsFalse(f.Director.IsPlaying);
            }
        }

        [Test]
        public void AnIFrameCoversTheLastOverlappingQueryAndClosesBeforeTheNextStep()
        {
            using(var f=new Fixture())
            {
                f.Library.Register(new TimelineDefinition{Id=new TimelineId(9901),Duration=.06f,Clips=new[]{new TimelineClip{Kind=ClipKind.IFrame,Start=.02f,End=.06f}}});
                var flags=new List<bool>();f.World.AddServicePhase(()=>flags.Add(f.Source.GetComp<TagComp>().Has(CommonTags.Invincible)));
                f.Director.Play(new SkillNodeId(9901),new TimelineId(9901));f.Tick(4);
                CollectionAssert.AreEqual(new[]{false,true,true,false},flags);Assert.IsFalse(f.Director.IsPlaying);
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void AllActorsMoveBeforeEitherSpawnOrderCanQueryHits(bool targetFirst)
        {
            using(var f=new Fixture(targetFirst))
            {
                var target=f.Target.GetComp<TransformComp>();target.Position=new SimVec3(.35f,0,0);
                f.Target.GetComp<LocomotionComp>().RequestMoveIntent(-1,0);
                f.Source.GetComp<HitboxComp>().Open(Damage(.5f),.2f,SimVec3.Zero);
                float atDamage=-1;f.World.Events.Subscribe<EvDamage>(e=>atDamage=target.Position.X);
                f.Tick();Assert.AreEqual(990,f.Hp);Assert.AreEqual(.15f,atDamage,.00001);
                Assert.AreEqual(.65f,target.Position.X,.00001,"Knockback must follow contact resolution in the same step.");
            }
        }

        [Test]
        public void MeleeVolumesAndHitsUseTheActualMovedOrigin()
        {
            using(var f=new Fixture())
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(2,0,0);var volumes=new List<EvSpatialVolume>();f.World.Events.Subscribe<EvSpatialVolume>(e=>volumes.Add(e));
                f.Play(Move(1),new TimelineClip{Kind=ClipKind.Melee,Start=0,End=.02f,Shape=HitShape.Thrust,Length=1.2f,Width=.4f,OnHit=Damage()});f.Tick();
                Assert.AreEqual(990,f.Hp);Assert.AreEqual(1,volumes.First(e=>!e.Closed).Origin.X,.00001);Assert.IsTrue(volumes.Last().Closed);
            }
        }

        [Test]
        public void AllContactQueriesSeeTheVictimBeforeAnyQueuedKnockback()
        {
            using(var f=new Fixture())
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(.4f,0,0);
                var other=f.Spawn("second-source");other.GetComp<TransformComp>().Position=new SimVec3(.6f,0,0);
                f.Source.GetComp<HitboxComp>().Open(Damage(5),.6f,SimVec3.Zero);other.GetComp<HitboxComp>().Open(Damage(),.6f,SimVec3.Zero);
                f.Tick();Assert.AreEqual(980,f.Hp);Assert.AreEqual(5.4f,f.Target.GetComp<TransformComp>().Position.X,.00001);
            }
        }

        [Test]
        public void GravityRunsOnceAfterQueriesAndPreservesItsConfiguredAcceleration()
        {
            using(var f=new Fixture())
            {
                float atQueries=-1;f.World.AddServicePhase(()=>atQueries=f.Transform.Position.Y);
                f.Motion.ImpulseVertical(5);f.Tick();
                Assert.AreEqual(0,atQueries);Assert.AreEqual((5+f.World.Motor.Gravity*.02f)*.02f,f.Transform.Position.Y,.00001);
            }
        }

        [Test]
        public void BothMovementPhasesUseTheSameCollisionConstraint()
        {
            using(var f=new Fixture(obstacles:new[]{new ObstacleDefinition("wall",2,0,.5f,4)}))
            {
                f.Source.GetComp<AttributeSet>().SetBase(AttrId.MoveSpeed,100);f.Motion.RequestMoveIntent(1,0);
                float beforeHit=-1;f.World.AddServicePhase(()=>{beforeHit=f.Transform.Position.X;f.Motion.RequestHitDelta(-.5f,0,0);});
                f.Tick();Assert.AreEqual(1.3499f,beforeHit,.0001);Assert.AreEqual(.8499f,f.Transform.Position.X,.0001);
                Assert.IsTrue(f.World.Collision.IsFree(f.Transform.Position,.4f));
            }
        }

        [Test]
        public void AnInterruptedQueuedSpatialQueryCannotDealDamageOrLeaveAVolume()
        {
            using(var f=new Fixture())
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(.5f,0,0);
                f.Play(new TimelineClip{Kind=ClipKind.Melee,Start=0,End=.02f,Shape=HitShape.Circle,Length=2,OnHit=Damage()});
                f.World.AddServicePhase(()=>f.Director.Stop(DirectorStopReason.Manual));f.Tick();Assert.AreEqual(1000,f.Hp);Assert.IsFalse(f.Director.IsPlaying);
                Assert.AreEqual(0,f.World.Intents.Count<ApplyEffectsIntent>());
            }
        }

        [Test]
        public void ARecycledSourceCannotExecuteItsFormerGenerationsSpatialQuery()
        {
            using(var f=new Fixture())
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(.5f,0,0);
                f.Play(new TimelineClip{Kind=ClipKind.Melee,Start=0,End=.02f,Shape=HitShape.Circle,Length=2,OnHit=Damage()});
                var old=f.Source.Id;Actor replacement=null;
                f.World.AddServicePhase(()=>{f.World.RequestDespawn(old);f.World.FlushCleanup();replacement=f.Spawn("replacement");});f.Tick();
                Assert.AreEqual(old.Index,replacement.Id.Index);Assert.AreNotEqual(old.Generation,replacement.Id.Generation);Assert.AreEqual(1000,f.Hp);
            }
        }

        [Test]
        public void IndependentHitstopFreezesBothPhasesAndResumesEachPendingRequestOnce()
        {
            using(var f=new Fixture())
            {
                f.Motion.RequestMoveIntent(1,0);f.Motion.RequestHitDelta(.3f,0,0);f.World.RequestActorHitstop(f.Source.Id,2);f.Tick(2);
                Assert.AreEqual(0,f.Transform.Position.X);Assert.AreEqual(0,f.Source.Time.Time);
                f.Tick();Assert.AreEqual(.5f,f.Transform.Position.X,.00001);f.Tick();Assert.AreEqual(.7f,f.Transform.Position.X,.00001);
            }
        }

        [Test]
        public void AttackAimDoesNotRequireAMovementClipToAllowRotation()
        {
            using(var f=new Fixture())
            {
                f.Library.Register(new TimelineDefinition{Id=new TimelineId(9901),Duration=1});f.Director.Play(new SkillNodeId(9901),new TimelineId(9901));
                f.Motion.RequestAimYaw(90);f.Tick();Assert.AreEqual(90,f.Transform.YawDegrees);
                using(f.Source.GetComp<TagComp>().Acquire(CommonTags.BlockRotate)){f.Motion.RequestAimYaw(180);f.Tick();Assert.AreEqual(90,f.Transform.YawDegrees);}
            }
        }

        [TestCase(false)] [TestCase(true)]
        public void ADashSweepsOnlyItsTravelAfterADiscreteTeleport(bool wallBehindLanding)
        {
            using(var f=new Fixture(obstacles:wallBehindLanding?new[]{new ObstacleDefinition("behind-landing",2,0,.5f,4)}:null))
            {
                f.Target.GetComp<TransformComp>().Position=new SimVec3(2,0,0);var destination=f.Spawn("target-end");destination.GetComp<TransformComp>().Position=new SimVec3(6,0,0);
                Assert.IsTrue(f.Motion.RequestTeleport(new SimVec3(4,0,0)));
                f.Play(new TimelineClip{Kind=ClipKind.Dash,Start=0,End=.02f,Length=4,Width=.4f,OnHit=Damage()});f.Tick();
                Assert.AreEqual(5.1999f,f.Transform.Position.X,.00001);Assert.AreEqual(1000,f.Hp);Assert.AreEqual(990,destination.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            }
        }
    }
}
