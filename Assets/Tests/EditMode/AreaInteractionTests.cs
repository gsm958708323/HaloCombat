using System;
using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class AreaInteractionTests
    {
        sealed class Factory : IActorFactory
        {
            public Actor Create(in ActorSpawnSpec spec)
            {
                var actor=new Actor();actor.AddComp(new TransformComp());actor.AddComp(new TagComp());actor.AddComp(new TeamComp(spec.BlueprintId=="target"||spec.BlueprintId=="enemy-owner"?2:1));
                if(spec.BlueprintId=="aoe")actor.AddComp(new AoeComp());
                else if(spec.BlueprintId=="projectile")actor.AddComp(new ProjectileComp());
                else
                {
                    var attr=new AttributeSet();attr.InitFighterDefaults();actor.AddComp(attr);actor.AddComp(new HealthComp());
                    actor.AddComp(new StateMachineComp());actor.AddComp(new LocomotionComp());actor.AddComp(new TrackedProjectilesComp());
                }
                return actor;
            }
            public void Release(Actor actor)=>actor.ResetForPool();
        }
        sealed class F : IDisposable
        {
            public readonly CombatWorld World;
            public readonly Actor Owner;
            public F(ObstacleDefinition[] obstacles=null){World=new CombatWorld(new Factory(),random:new FixedRandom(.5f),obstacles:obstacles);Owner=Spawn("owner",-10);}
            public Actor Spawn(string id,float x=0,float z=0){World.TryGetActor(World.SpawnActor(new ActorSpawnSpec(id)),out var actor);actor.GetComp<TransformComp>().Position=new SimVec3(x,0,z);return actor;}
            public Actor Area(AoeDefinition def,float x=0,float z=0)
            {var a=Spawn("aoe",x,z);a.GetComp<AoeComp>().Setup(def,Owner.Id,50,World.Time.Frame);return a;}
            public Actor Shot(ProjectileDefinition def,float x=0,float z=0,Actor owner=null,float yaw=0)
            {var a=Spawn("projectile",x,z);a.GetComp<TransformComp>().YawDegrees=yaw;SpawnAoeEffect.CopyTeam(owner??Owner,a);a.GetComp<ProjectileComp>().Setup(def,(owner??Owner).Id,50);return a;}
            public void Tick(int count=1){for(int i=0;i<count;i++)World.Tick(.02f);}
            public void Dispose()=>World.Shutdown();
        }
        sealed class Count : IEffect {public int Value;public void Apply(ref EffectContext context)=>Value++;}
        static AoeDefinition Orb()=>new AoeDefinition{SpecId=1,Radius=.25f,Duration=100,MoveForward=true,MoveSpeed=.1f,AbsorbProjectiles=true,ProjectileAbsorbForce=.05f,ProjectileRadiusScale=.05f,PulseOnSpawn=false};
        static ProjectileDefinition Bullet(float speed=0)=>new ProjectileDefinition{SpecId=1,Speed=speed,Lifetime=100,HitRadius=.1f,OnHit=new IEffect[]{new PierceEffect(0)}};
        static DamageEffect Damage(float amount)=>new DamageEffect{Flat=amount,Coeff=0,CanCrit=false,FireOnHurted=false};

        [Test]
        public void AbsorptionAddsHorizontalMomentumAndGrowthWithoutChangingSharedDefinitions()
        {
            using(var f=new F())
            {
                var definition=Orb();var area=f.Area(definition,.5f);var body=area.GetComp<AoeComp>();var other=f.Area(definition,10);var shot=f.Shot(Bullet(6),.4f);
                f.Tick();Assert.IsFalse(shot.IsActive);Assert.AreEqual(1,body.AbsorbedProjectiles);Assert.AreEqual(.2625f,body.Radius,.00001);Assert.AreEqual(.3f,body.Velocity.X,.00001);Assert.AreEqual(0,body.Velocity.Y);
                Assert.AreEqual(.502f,area.GetComp<TransformComp>().Position.X,.00001);f.Tick();Assert.AreEqual(.51f,area.GetComp<TransformComp>().Position.X,.00001);
                Assert.AreEqual(.25f,definition.Radius);Assert.AreEqual(.25f,other.GetComp<AoeComp>().Radius);Assert.AreEqual(0,other.GetComp<AoeComp>().AbsorbedProjectiles);
            }
        }
        [Test]
        public void AbsorptionUsesSameTeamAndNeverConsumesEnemyProjectiles()
        {
            using(var f=new F())
            {
                var area=f.Area(Orb());var enemy=f.Spawn("enemy-owner",-20);var friendly=f.Shot(Bullet());var hostile=f.Shot(Bullet(),owner:enemy);
                f.Tick();Assert.IsFalse(friendly.IsActive);Assert.IsTrue(hostile.IsActive);Assert.AreEqual(1,area.GetComp<AoeComp>().AbsorbedProjectiles);
            }
        }
        [Test]
        public void FirstAreaWinsAndAbsorbedProjectilesDoNotExecuteTerminationPayloads()
        {
            using(var f=new F())
            {
                var first=f.Area(Orb());var second=f.Area(Orb());var effects=new Count();var definition=Bullet();definition.TrackingKey="feed";definition.OnEnd=definition.OnExpire=new IEffect[]{effects};var shot=f.Shot(definition);
                var tracker=f.Owner.GetComp<TrackedProjectilesComp>();tracker.Track("feed",shot.Id);
                int absorbed=0,ends=0;f.World.Events.Subscribe<EvAoeAbsorbed>(e=>{absorbed++;Assert.AreEqual(first.Id,e.Area);Assert.IsFalse(tracker.TryFind("feed",out _));});
                f.World.Events.Subscribe<EvProjectileEnded>(e=>{ends++;Assert.AreEqual(ProjectileEndReason.Absorbed,e.Reason);});
                f.Tick(3);Assert.AreEqual(1,absorbed);Assert.AreEqual(1,ends);Assert.AreEqual(0,effects.Value);Assert.AreEqual(1,first.GetComp<AoeComp>().AbsorbedProjectiles);Assert.AreEqual(0,second.GetComp<AoeComp>().AbsorbedProjectiles);Assert.IsFalse(shot.IsActive);
            }
        }
        [Test]
        public void ExpandedRadiusIsTheAuthorityForOccupancyAndPulseQueries()
        {
            using(var f=new F())
            {
                var def=Orb();def.MoveSpeed=0;def.ProjectileRadiusScale=1;def.TrackOccupancy=true;def.OnEnter=new IEffect[]{Damage(7)};def.OnPulse=new IEffect[]{Damage(3)};def.PulseInterval=0;
                var area=f.Area(def);var target=f.Spawn("target",.4f);f.Shot(Bullet());f.Tick();Assert.AreEqual(93,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
                AoePulse.PulseNow(f.World,area,area.GetComp<AoeComp>());Assert.AreEqual(90,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            }
        }
        [Test]
        public void HorizontalImpulseStillMovesAForwardAreaWhoseBaseSpeedIsZero()
        {
            using(var f=new F())
            {
                var definition=Orb();definition.MoveSpeed=0;var area=f.Area(definition);var shot=Bullet(2);shot.Motion=new ProjectileMotionDefinition{Bounce=new ProjectileBounceDefinition{VerticalSpeed=10,Gravity=20,Count=2}};
                f.Shot(shot,yaw:90);f.Tick(2);var body=area.GetComp<AoeComp>();Assert.AreEqual(1,body.AbsorbedProjectiles);Assert.AreEqual(0,body.Velocity.Y);Assert.Greater(area.GetComp<TransformComp>().Position.Z,0);Assert.AreEqual(0,area.GetComp<TransformComp>().Position.Y);
            }
        }
        [TestCase(true)] [TestCase(false)]
        public void ObstaclePolicyUsesTheExpandedCollisionRadius(bool remove)
        {
            using(var f=new F(new[]{new ObstacleDefinition("wall",.55f,0,.1f,3)}))
            {
                var definition=Orb();definition.MoveSpeed=0;definition.ProjectileRadiusScale=2;definition.RemoveOnObstacle=remove;
                var area=f.Area(definition);f.Shot(Bullet());f.Tick();Assert.AreEqual(.75f,area.GetComp<AoeComp>().Radius);f.Tick();Assert.AreEqual(!remove,area.IsActive);
                if(!remove)Assert.AreEqual(0,area.GetComp<TransformComp>().Position.X);
            }
        }
        [Test]
        public void AreaHitstopPausesMovementAbsorptionAndAgeButOwnerHitstopDoesNot()
        {
            using(var f=new F())
            {
                var area=f.Area(Orb());var body=area.GetComp<AoeComp>();var shot=f.Shot(Bullet());f.World.RequestActorHitstop(area.Id,3);f.Tick(3);
                Assert.AreEqual(0,body.Age);Assert.AreEqual(0,body.AbsorbedProjectiles);Assert.IsTrue(shot.IsActive);Assert.Greater(shot.GetComp<ProjectileComp>().Age,0);
                f.World.RequestActorHitstop(f.Owner.Id,3);f.Tick();Assert.AreEqual(1,body.AbsorbedProjectiles);Assert.Greater(body.Age,0);Assert.Greater(area.GetComp<TransformComp>().Position.X,0);
            }
        }
        [Test]
        public void BirthStepIsSkippedAndCleanupClearsAllAbsorptionState()
        {
            using(var f=new F())
            {
                var area=f.Area(Orb());var body=area.GetComp<AoeComp>();body.Setup(Orb(),f.Owner.Id,50,1);var shot=f.Shot(Bullet());f.Tick();Assert.AreEqual(0,body.AbsorbedProjectiles);Assert.AreEqual(0,body.Age);
                f.Tick();Assert.AreEqual(1,body.AbsorbedProjectiles);f.World.RequestDespawn(area.Id);f.Tick();Assert.IsNull(body.Def);Assert.AreEqual(0,body.AbsorbedProjectiles);Assert.AreEqual(0,body.Radius);Assert.AreEqual(0,body.Velocity.X);
            }
        }
        [Test]
        public void AbsorbEffectRunsAfterConsumptionAndCanSafelyRemoveItsOwnOwner()
        {
            using(var f=new F())
            {
                var definition=Orb();definition.OnAbsorb=new IEffect[]{new RemoveOwner()};var area=f.Area(definition);f.Shot(Bullet());f.Shot(Bullet());int count=0;f.World.Events.Subscribe<EvAoeAbsorbed>(e=>count++);
                f.Tick();Assert.AreEqual(1,count);Assert.IsFalse(area.IsActive);Assert.IsFalse(f.Owner.IsActive);
            }
        }
        sealed class RemoveOwner : IEffect {public void Apply(ref EffectContext ctx)=>ctx.World.RequestDespawn(ctx.Source.Id);}

        [TestCase(0,1)] [TestCase(.1f,2)]
        public void SameTargetDelaySelectsOnceOrRepeatedContacts(float delay,int expected)
        {
            using(var f=new F())
            {
                var target=f.Spawn("target");var def=Bullet();def.SameTargetDelay=delay;def.OnHit=new IEffect[]{Damage(1),new PierceEffect(0)};var shot=f.Shot(def);
                f.Tick(10);Assert.AreEqual(expected,shot.GetComp<ProjectileComp>().HitCount);Assert.AreEqual(100-expected,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
                if(delay>0){f.Tick();Assert.AreEqual(3,shot.GetComp<ProjectileComp>().HitCount);Assert.IsTrue(shot.GetComp<ProjectileComp>().HasHit(target.Id));}
            }
        }
        [TestCase(.08,false)] [TestCase(.09,true)]
        public void RepeatWindowIsEvaluatedAtTheExactSweptContactTime(double previousAge,bool hit)
        {
            using(var f=new F())
            {
                var target=f.Spawn("target");var def=Bullet(100);def.SameTargetDelay=.1f;def.OnHit=new IEffect[]{Damage(1),new PierceEffect(0)};var shot=f.Shot(def,-1.6f);var body=shot.GetComp<ProjectileComp>();body.TryRecord(target.Id);body.Age=previousAge;
                f.Tick();Assert.AreEqual(hit?99:100,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            }
        }
        [Test]
        public void HitstopFreezesTheProjectileContactCooldownAndHitBudgetStillTerminatesIt()
        {
            using(var f=new F())
            {
                var target=f.Spawn("target");var def=Bullet();def.SameTargetDelay=.1f;def.OnHit=new IEffect[]{Damage(1),new PierceEffect(3)};var shot=f.Shot(def);f.Tick();
                f.World.RequestActorHitstop(shot.Id,5);f.Tick(5);Assert.AreEqual(99,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));Assert.AreEqual(.02,shot.GetComp<ProjectileComp>().Age,.00001);
                f.Tick(10);Assert.AreEqual(97,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));Assert.IsFalse(shot.IsActive);
            }
        }
        [Test]
        public void SplitAncestorsRemainPermanentlyExcludedEvenWithAContactCooldown()
        {
            var target=new EntityId(5,1);var body=new ProjectileComp();var def=Bullet();def.SameTargetDelay=.1f;
            var seed=(ProjectileSeed)Activator.CreateInstance(typeof(ProjectileSeed),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new object[]{new ProjectileFamily(default),1,0f,true,new[]{target},null},null);
            body.Setup(def,EntityId.Invalid,0,seed:seed);body.Age=100;
            Assert.IsFalse(body.CanHit(target,100));Assert.IsFalse(body.TryRecord(target));Assert.IsTrue(body.TryRecord(new EntityId(5,2)));
        }
        [TestCase(true,0)] [TestCase(false,1)]
        public void PassingInvulnerableTargetsDoesNotConsumeTheHitBudget(bool pass,int hitCount)
        {
            using(var f=new F())
            {
                var target=f.Spawn("target");var lease=target.GetComp<TagComp>().Acquire(CommonTags.Invincible);var def=Bullet();def.PassInvulnerable=pass;def.OnHit=new IEffect[]{Damage(1),new PierceEffect(0)};var shot=f.Shot(def);f.Tick();
                Assert.AreEqual(hitCount,shot.GetComp<ProjectileComp>().HitCount);Assert.AreEqual(100,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));lease.Dispose();f.Tick();Assert.AreEqual(pass?99:100,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            }
        }
    }
}
