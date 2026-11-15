using System;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Core;
using NUnit.Framework;
using UnityEngine;

namespace Combat.Tests
{
    public sealed class TimelineControlTests
    {
        static SkillProjectDocument Project()=>SkillProjectFiles.Read(Path.Combine(Application.streamingAssetsPath,"Combat/project.json"));
        sealed class Fixture : IDisposable
        {
            public readonly CombatWorld World;
            public readonly Actor Player;
            public readonly CombatContentSnapshot Content;
            public SkillDirectorComp Director=>Player.GetComp<SkillDirectorComp>();
            public TagComp Tags=>Player.GetComp<TagComp>();
            public LocomotionComp Motor=>Player.GetComp<LocomotionComp>();
            public TransformComp Transform=>Player.GetComp<TransformComp>();
            public AttributeSet Attributes=>Player.GetComp<AttributeSet>();
            public SkillCastComp CastComp=>Player.GetComp<SkillCastComp>();
            public Fixture(int[] controls=null,float speed=1,bool scaled=true)
            {
                var p=Project();var timeline=p.Skills.Single(s=>s.Id=="arena-controlled-shot").Timeline;
                if(controls!=null)timeline.ControlTags=controls;timeline.ScaleWithActionSpeed=scaled;
                p.Runtime.Actors.Single(a=>a.Id=="arena-player").ActionSpeed=speed;Content=CombatContentCompiler.Compile(p);
                World=new CombatWorld(Content.Runtime.CreateFactory(Content.Skills),cues:Content.Runtime.Data.Cues,motor:Content.Runtime.Data.Motor);Content.Runtime.Data.Install(World);
                World.TryGetActor(World.SpawnActor(new ActorSpawnSpec("arena-player")),out var player);Player=player;
            }
            public bool Cast(float yaw=0)=>CastComp.TryCast(new SkillNodeId(2015),new SkillAim(EntityId.Invalid,new SimVec3(4,0,0),yaw));
            public void Tick(int count=1){for(int i=0;i<count;i++)World.Tick(.02f);}
            public void Dispose()=>World.Shutdown();
        }
        [Test]
        public void WalkAndJumpRestrictionsStillAllowTheTimelineOwnedMovement()
        {
            using(var f=new Fixture(new[]{1011}))
            {
                f.Motor.RequestMoveIntent(0,1);f.Motor.RequestAimYaw(0);Assert.IsTrue(f.Cast());f.Motor.ImpulseJump();Assert.IsTrue(f.Motor.IsGrounded);f.Tick(10);
                Assert.Greater(f.Transform.Position.X,0);Assert.AreEqual(0,f.Transform.Position.Z,.0001f);Assert.AreEqual(0,f.Transform.Position.Y,.0001f);
                f.Director.Stop(DirectorStopReason.Manual);f.Tick();Assert.Greater(f.Transform.Position.Z,0);f.Motor.ImpulseJump();Assert.IsFalse(f.Motor.IsGrounded);
            }
        }
        [Test]
        public void TheNewRotateLeaseStartsAfterInitialSnapAndBlocksSubsequentAimAndSnaps()
        {
            using(var f=new Fixture(new[]{1012}))
            {
                Assert.IsTrue(f.Cast(90));Assert.AreEqual(90,f.Transform.YawDegrees,.0001);
                f.Motor.RequestAimYaw(180);f.Motor.RequestSnapYawDegrees(180);f.Tick(5);Assert.AreEqual(90,f.Transform.YawDegrees,.0001);
                f.Director.Stop(DirectorStopReason.Manual);f.Motor.RequestAimYaw(180);f.Tick();Assert.AreEqual(180,f.Transform.YawDegrees,.0001);
            }
        }
        [Test]
        public void ExistingExternalRotateBlockAlsoPreventsInitialCastSnap()
        {
            using(var f=new Fixture(Array.Empty<int>()))using(f.Tags.Acquire(CommonTags.BlockRotate))
            {Assert.IsTrue(f.Cast(90));f.Tick(5);Assert.AreEqual(0,f.Transform.YawDegrees,.0001);}
        }
        [Test]
        public void ExplicitFacingAimRotatesBothAuthoredMovementAndItsProjectile()
        {
            using(var f=new Fixture(new[]{1011,1012}))
            {
                Assert.IsTrue(f.Cast(90));f.Tick(10);Assert.AreEqual(0,f.Transform.Position.X,.0001);Assert.AreEqual(1,f.Transform.Position.Z,.0001);
                var projectile=f.World.RegistryActive().Single(a=>a.TryGetComp<ProjectileComp>(out _));Assert.AreEqual(90,projectile.GetComp<TransformComp>().YawDegrees,.0001);
            }
        }
        [Test]
        public void NonfiniteFacingYawIsRejectedBeforeAcquiringControlsOrPayingResources()
        {
            using(var f=new Fixture())
            {Assert.IsFalse(f.CastComp.TryCast(new SkillNodeId(2015),new SkillAim(EntityId.Invalid,default,float.NaN,false)));Assert.AreEqual(100,f.Player.GetComp<EnergyComp>().Current);Assert.IsFalse(f.Tags.Has(CommonTags.BlockMove));}
        }
        [Test]
        public void OwnSkillBlockRejectsNewCastsWithoutCancellingItsOwnTimeline()
        {
            using(var f=new Fixture(new[]{1013}))
            {
                Assert.IsTrue(f.Cast());using(f.Tags.Acquire(CommonTags.Cancel))Assert.IsFalse(f.CastComp.TryCast(SkillNodeId.G1,new SkillAim(EntityId.Invalid,default,0)));
                Assert.AreEqual(90,f.Player.GetComp<EnergyComp>().Current);f.Tick(10);Assert.Greater(f.Director.TimelineTime,0);Assert.IsTrue(f.Director.IsPlaying);
                f.Tick(20);Assert.IsFalse(f.Tags.Has(CommonTags.BlockSkill));Assert.IsFalse(f.Director.IsPlaying);Assert.IsTrue(f.CastComp.TryCast(SkillNodeId.G1,new SkillAim(EntityId.Invalid,default,0)));
            }
        }
        [Test]
        public void BlockedSkillIncrementsAreDiscardedAndNeverCatchUpAfterRelease()
        {
            using(var f=new Fixture(new[]{1014}))
            {
                Assert.IsTrue(f.Cast());f.Tick(20);Assert.AreEqual(0,f.Transform.Position.X,.0001);f.Tick(10);Assert.IsFalse(f.Director.IsPlaying);
                f.Tick(5);Assert.AreEqual(0,f.Transform.Position.X,.0001);
            }
        }
        [Test]
        public void SuppressedDashCannotSweepDamageThroughTheUntravelledPath()
        {
            using(var f=new Fixture(Array.Empty<int>()))using(f.Tags.Acquire(CommonTags.BlockSkillMotion))
            {
                f.World.TryGetActor(f.World.SpawnActor(new ActorSpawnSpec("stake")),out var target);target.GetComp<TransformComp>().Position=new SimVec3(2,0,0);float before=target.GetComp<AttributeSet>().GetBase(AttrId.Hp);
                f.Content.Timelines.Register(new TimelineDefinition{Id=new TimelineId(9999),Duration=.4f,Clips=new[]{new TimelineClip{Kind=ClipKind.Dash,Start=0,End=.4f,Length=4,Width=1,OnHit=new IEffect[]{new DamageEffect{Coeff=0,Flat=10}}}}});
                Assert.IsTrue(f.Director.Play(new SkillNodeId(9999),new TimelineId(9999)));f.Tick(20);Assert.AreEqual(0,f.Transform.Position.X);Assert.AreEqual(before,target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            }
        }
        [Test]
        public void BlockedHitMotionDoesNotBlockWalkAndCannotAccumulateHiddenKnockback()
        {
            using(var f=new Fixture())
            {
                using(f.Tags.Acquire(CommonTags.BlockHitMotion))
                {f.Motor.RequestMoveIntent(0,1);f.Motor.RequestHitDelta(3,0,0);f.Tick();Assert.AreEqual(0,f.Transform.Position.X);Assert.Greater(f.Transform.Position.Z,0);}
                f.Tick();Assert.AreEqual(0,f.Transform.Position.X);f.Motor.RequestHitDelta(1,0,0);f.Tick();Assert.AreEqual(1,f.Transform.Position.X,.0001);
            }
        }
        [Test]
        public void GravityRestrictionPreservesTheAirborneStateAndVelocityUntilReleased()
        {
            using(var f=new Fixture())
            {
                f.Motor.ImpulseVertical(5);f.Tick();float height=f.Transform.Position.Y;Assert.Greater(height,0);
                using(f.Tags.Acquire(CommonTags.BlockGravity)){f.Tick(10);Assert.AreEqual(height,f.Transform.Position.Y);Assert.IsFalse(f.Motor.IsGrounded);}
                f.Tick();Assert.Greater(f.Transform.Position.Y,height);
            }
        }
        [Test]
        public void SkillMotionRestrictionDoesNotReplaceTheExplicitTeleportLandingContract()
        {
            using(var f=new Fixture())using(f.Tags.Acquire(CommonTags.BlockSkillMotion))
            {Assert.IsTrue(f.Motor.RequestTeleport(new SimVec3(2,0,0)));f.Tick();Assert.AreEqual(2,f.Transform.Position.X);}
        }
        [TestCase(DirectorStopReason.Manual)] [TestCase(DirectorStopReason.InputCancelled)] [TestCase(DirectorStopReason.Hit)] [TestCase(DirectorStopReason.Dead)] [TestCase(DirectorStopReason.Detach)] [TestCase(DirectorStopReason.Released)]
        public void StopPathsReleaseOnlyTheTimelinesOwnControlLeases(DirectorStopReason reason)
        {
            using(var f=new Fixture())using(f.Tags.Acquire(CommonTags.BlockMove))
            {Assert.IsTrue(f.Cast());Assert.AreEqual(2,f.Tags.Stack(CommonTags.BlockMove));f.Director.Stop(reason);Assert.AreEqual(1,f.Tags.Stack(CommonTags.BlockMove));Assert.IsFalse(f.Tags.Has(CommonTags.BlockRotate));Assert.IsFalse(f.Tags.Has(CommonTags.BlockSkill));}
        }
        [Test]
        public void SuccessfulReplacementReleasesTheOldControlsAndFailedStartAcquiresNone()
        {
            using(var f=new Fixture(new[]{1011}))
            {
                Assert.IsTrue(f.Cast());using(f.Tags.Acquire(CommonTags.Cancel))Assert.IsTrue(f.CastComp.TryCast(SkillNodeId.G1,new SkillAim(EntityId.Invalid,default,0)));
                Assert.IsFalse(f.Tags.Has(CommonTags.BlockMove));
            }
            using(var f=new Fixture())using(f.Tags.Acquire(CommonTags.BlockSkill))
            {Assert.IsFalse(f.Cast());Assert.IsFalse(f.Tags.Has(CommonTags.BlockMove));Assert.IsFalse(f.Tags.Has(CommonTags.BlockRotate));Assert.AreEqual(100,f.Player.GetComp<EnergyComp>().Current);}
        }
        [TestCase(.5f)] [TestCase(1f)] [TestCase(2f)] [TestCase(0f)]
        public void ScaleChangesScheduleAndCompletionButPreservesTotalAuthoredDisplacement(float speed)
        {
            using(var f=new Fixture(Array.Empty<int>(),speed))
            {
                Assert.IsTrue(f.Cast());f.Tick(10);float rate=Math.Max(.1f,speed);Assert.AreEqual(.2f*rate,f.Director.TimelineTime,.00001f);Assert.AreEqual(rate,f.Director.ActionRate,.00001f);
                f.Tick((int)Math.Ceiling(.6f/(.02f*rate))+2);Assert.IsFalse(f.Director.IsPlaying);Assert.AreEqual(2,f.Transform.Position.X,.0001f);
            }
        }
        [Test]
        public void DisablingScaleUsesTheAuthoredSecondsEvenOnAFastActor()
        {
            using(var f=new Fixture(Array.Empty<int>(),2,false)){Assert.IsTrue(f.Cast());f.Tick(10);Assert.AreEqual(.2f,f.Director.TimelineTime,.00001f);Assert.AreEqual(1,f.Director.ActionRate);}
        }
        [Test]
        public void BuffActionSpeedChangesTheNextTimelineStepAndDispelRestoresTheBaseRate()
        {
            using(var f=new Fixture(Array.Empty<int>()))
            {
                Assert.IsTrue(f.Cast());f.Tick(5);Assert.AreEqual(.1f,f.Director.TimelineTime,.00001f);
                var buff=new DurationSpec{BuffId=999,Duration=1,Modifiers=new[]{new Modifier{Attr=AttrId.ActionSpeed,Op=ModOp.Mul,Value=2}}};f.Player.GetComp<BuffComp>().Apply(buff,f.Player);
                f.Tick();Assert.AreEqual(.14f,f.Director.TimelineTime,.00001f);var id=f.Player.GetComp<BuffComp>().Snapshot().Single().InstanceId;f.Player.GetComp<BuffComp>().RemoveInstance(id);
                f.Tick();Assert.AreEqual(.16f,f.Director.TimelineTime,.00001f);Assert.AreEqual(1,f.Attributes.GetFinal(AttrId.ActionSpeed));
            }
        }
        [Test]
        public void CooldownsRecoveryBuffLifetimesAndExistingProjectilesKeepActorClockSeconds()
        {
            using(var f=new Fixture(Array.Empty<int>(),2))
            {
                var buff=new DurationSpec{BuffId=998,Duration=1};f.Player.GetComp<BuffComp>().Apply(buff,f.Player);Assert.IsTrue(f.Cast());f.Tick(10);
                Assert.AreEqual(.6f,f.CastComp.CooldownLeft(new SkillNodeId(2015)),.0001f);Assert.AreEqual(92,f.Player.GetComp<EnergyComp>().Current,.001f);
                Assert.AreEqual(.8f,f.Player.GetComp<BuffComp>().Snapshot().Single().Remaining.Value,.0001f);
                var projectile=f.World.RegistryActive().Single(a=>a.TryGetComp<ProjectileComp>(out _));float x=projectile.GetComp<TransformComp>().Position.X;double age=projectile.GetComp<ProjectileComp>().Age;
                f.Attributes.SetBase(AttrId.ActionSpeed,4);f.Tick(3);Assert.AreEqual(x+.36f,projectile.GetComp<TransformComp>().Position.X,.0001f);Assert.AreEqual(age+.06,projectile.GetComp<ProjectileComp>().Age,.00001);
            }
        }
        [Test]
        public void HitstopFreezesActionTimeAndKeepsItsControlsUntilPlaybackResumes()
        {
            using(var f=new Fixture(null,2))
            {
                Assert.IsTrue(f.Cast());f.Tick(5);float action=f.Director.TimelineTime;f.World.RequestActorHitstop(f.Player.Id,5);f.Tick(5);
                Assert.AreEqual(action,f.Director.TimelineTime);Assert.IsTrue(f.Tags.Has(CommonTags.BlockMove));Assert.IsTrue(f.Tags.Has(CommonTags.BlockSkill));f.Tick();Assert.AreEqual(action+.04f,f.Director.TimelineTime,.0001f);
            }
        }
        [TestCase("null")] [TestCase("duplicate")] [TestCase("state")] [TestCase("unknown")] [TestCase("speed")] [TestCase("preview-speed")]
        public void InvalidControlAndSpeedFieldsFailBeforeCreatingAWorld(string field)
        {
            var p=Project();var timeline=p.Skills.Single(s=>s.Id=="arena-controlled-shot").Timeline;
            if(field=="null")timeline.ControlTags=null;if(field=="duplicate")timeline.ControlTags=new[]{1011,1011};if(field=="state")timeline.ControlTags=new[]{1005};if(field=="unknown")timeline.ControlTags=new[]{99999};
            if(field=="speed")p.Runtime.Actors[0].ActionSpeed=float.NaN;if(field=="preview-speed")p.Preview.Caster.ActionSpeed=-1;
            Assert.Throws<ContentValidationException>(()=>CombatContentCompiler.Compile(p));
        }
        [Test]
        public void DefaultMetadataIsOmittedAndAnExistingSkillPublicationKeepsItsRevision()
        {
            var p=Project();var publication=SkillPublicationJson.Create(p,"g1");string text=SkillPublicationJson.Write(publication);
            Assert.IsFalse(text.Contains("scaleWithActionSpeed"));Assert.IsFalse(text.Contains("controlTags"));Assert.IsFalse(text.Contains("actionSpeed"));
            Assert.DoesNotThrow(()=>SkillPublicationJson.Read(text));
        }
    }
}
