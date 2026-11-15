using System;
using Combat.Core;
using Combat.Presentation;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class ArenaCameraMotionTests
    {
        [Test]
        public void InitialFocusSnapsToTheStandingEyeAndLaterMovementUsesTheOriginalExponentialSpring()
        {
            var motion=new ArenaCameraMotion(1.6f,.55f,8);motion.Sample(new SimVec3(2,0,3),false,0);
            Assert.AreEqual(2,motion.Focus.X);Assert.AreEqual(1.6,motion.Focus.Y,.00001);Assert.AreEqual(3,motion.Focus.Z);
            motion.Sample(new SimVec3(12,0,3),false,.1f);Assert.AreEqual(2+10*(1-Math.Exp(-.8)),motion.Focus.X,.00001);
        }
        [Test]
        public void SubdividingPresentationTimeKeepsTheSameFollowPosition()
        {
            var single=new ArenaCameraMotion(1.6f,.55f,8);var split=new ArenaCameraMotion(1.6f,.55f,8);
            single.Sample(default,false,0);split.Sample(default,false,0);var target=new SimVec3(10,2,4);
            single.Sample(target,false,.5f);for(int i=0;i<50;i++)split.Sample(target,false,.01f);
            Assert.AreEqual(single.Focus.X,split.Focus.X,.00001);Assert.AreEqual(single.Focus.Y,split.Focus.Y,.00001);Assert.AreEqual(single.Focus.Z,split.Focus.Z,.00001);
        }
        [Test]
        public void DownedAndDeadPostureLowersTheEyeWithoutChangingTheLogicalPosition()
        {
            var motion=new ArenaCameraMotion(1.6f,.55f,8);var position=new SimVec3(3,0,5);motion.Sample(position,false,0);motion.Sample(position,true,1);
            Assert.AreEqual(.55+(1.6-.55)*Math.Exp(-8),motion.Focus.Y,.00001);Assert.AreEqual(0,position.Y);Assert.AreEqual(3,motion.Focus.X);Assert.AreEqual(5,motion.Focus.Z);
        }
        [TestCase(HitFeedbackLevel.None,0)]
        [TestCase(HitFeedbackLevel.Light,.2f)]
        [TestCase(HitFeedbackLevel.ComboConfirm,.35f)]
        [TestCase(HitFeedbackLevel.Heavy,.55f)]
        [TestCase(HitFeedbackLevel.Finisher,.8f)]
        public void HitFeedbackKeepsTheOriginalGradedImpulseAndDecaysOnPresentationTime(HitFeedbackLevel level,float impulse)
        {
            var motion=new ArenaCameraMotion(1.6f,.55f,8);motion.NotifyHit(level);Assert.AreEqual(impulse,motion.Impulse);
            motion.Sample(default,false,.1f);Assert.AreEqual(Math.Max(0,impulse-.1f),motion.Impulse,.00001);motion.Sample(default,false,2);Assert.AreEqual(0,motion.Impulse);
        }
        [Test]
        public void WeakerFeedbackAndNegativeDeltaCannotIncreaseOrReplaceAnActiveStrongerImpulse()
        {
            var motion=new ArenaCameraMotion(1.6f,.55f,8);motion.Sample(default,false,0);motion.NotifyHit(HitFeedbackLevel.Heavy);motion.NotifyHit(HitFeedbackLevel.Light);
            motion.Sample(new SimVec3(100,0,0),false,-1);Assert.AreEqual(.55f,motion.Impulse);Assert.AreEqual(0,motion.Focus.X);
        }
        [Test]
        public void ZeroSpringProvidesExplicitImmediateTrackingAndInstancesDoNotShareState()
        {
            var motion=new ArenaCameraMotion(1.6f,.55f,0);motion.Sample(default,false,0);motion.Sample(new SimVec3(7,0,2),true,0);
            Assert.AreEqual(7,motion.Focus.X);Assert.AreEqual(.55,motion.Focus.Y,.00001);motion.NotifyHit(HitFeedbackLevel.Finisher);
            var other=new ArenaCameraMotion(1.6f,.55f,8);Assert.AreEqual(0,other.Impulse);other.Sample(default,false,0);Assert.AreEqual(0,other.Focus.X);
        }
    }
}
