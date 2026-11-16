using System.Linq;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class ProjectileContactPolicyTests
    {
        [TestCase(ProjectileContactMode.Behavior,2,true)]
        [TestCase(ProjectileContactMode.End,1,true)]
        [TestCase(ProjectileContactMode.Continue,3,false)]
        [TestCase(ProjectileContactMode.Ignore,0,false)]
        public void ContactPolicyControlsDamageAndWhetherTheBallEnds(ProjectileContactMode mode,int hits,bool ends)
        {
            var def=ProjectileMotionTestWorld.Definition();def.ContactMode=mode;ProjectileMotionTestWorld.Pierce(def,2);
            var w=new ProjectileMotionTestWorld(def);w.Spawn("target",1);w.Spawn("target",2);w.Spawn("target",3);w.Fire();w.World.Tick(1);
            Assert.AreEqual(hits,w.DamageCount);Assert.AreEqual(ends,w.Body==null);if(ends)Assert.AreEqual(ProjectileEndReason.HitLimit,w.Ended.Single().Reason);
        }

        [TestCase(ProjectileContactMode.Behavior)]
        [TestCase(ProjectileContactMode.End)]
        public void PassingAnInvulnerableTargetPreservesTheContactBudget(ProjectileContactMode mode)
        {
            var def=ProjectileMotionTestWorld.Definition();def.ContactMode=mode;ProjectileMotionTestWorld.Pierce(def,1);def.PassInvulnerable=true;
            var w=new ProjectileMotionTestWorld(def);var invincible=w.Spawn("target",1);w.World.TryGetActor(invincible,out var actor);actor.GetComp<TagComp>().Add(CommonTags.Invincible,1,TagSource.Debug);
            var victim=w.Spawn("target",2);w.Fire();w.World.Tick(1);Assert.AreEqual(1,w.DamageCount);Assert.AreEqual(ProjectileEndReason.HitLimit,w.Ended.Single().Reason);
            w.World.TryGetActor(victim,out actor);Assert.AreEqual(990,actor.GetComp<AttributeSet>().GetBase(AttrId.Hp));
        }

        [TestCase(false,1)]
        [TestCase(true,2)]
        public void PerLegPolicyAllowsOneHitOnEachReturnLeg(bool perLeg,int hits)
        {
            var def=ProjectileMotionTestWorld.Definition();def.HitOncePerLeg=perLeg;def.Motion=new ProjectileMotionDefinition{ReturnAfter=.5f,ReturnSpeed=4,CatchRadius=.1f};
            var w=new ProjectileMotionTestWorld(def);w.Spawn("target",1);w.Fire();w.Advance(1,100);Assert.AreEqual(hits,w.DamageCount);Assert.AreEqual(ProjectileEndReason.Returned,w.Ended.Single().Reason);
        }

        [Test]
        public void IntervalPolicyOnlyHitsAgainAfterItsElapsedAge()
        {
            var def=ProjectileMotionTestWorld.Definition();def.Speed=0;def.SameTargetDelay=.2f;var w=new ProjectileMotionTestWorld(def);w.Spawn("target",0);w.Fire();
            w.World.Tick(.01f);Assert.AreEqual(1,w.DamageCount);w.World.Tick(.1f);Assert.AreEqual(1,w.DamageCount);w.World.Tick(.1f);w.World.Tick(.01f);Assert.AreEqual(2,w.DamageCount);
        }

        [Test]
        public void ReturningChildrenNeverRehitTheirAncestorsEvenWithPerLegPolicy()
        {
            var def=ProjectileMotionTestWorld.Definition();ProjectileMotionTestWorld.Pierce(def,1);def.HitOncePerLeg=true;def.Motion=new ProjectileMotionDefinition{ReturnAfter=.5f,ReturnSpeed=4,CatchRadius=.1f};
            def.OnHit=def.OnHit.Concat(new IEffect[]{new SplitProjectileEffect(1,0,1,1,false,20)}).ToArray();
            var w=new ProjectileMotionTestWorld(def);w.Spawn("target",1);w.Fire();w.Advance(2,200);
            Assert.AreEqual(1,w.DamageCount);Assert.AreEqual(2,w.SpawnPositions.Count);Assert.IsTrue(w.Ended.Any(e=>e.Reason==ProjectileEndReason.Returned));Assert.IsNull(w.Body);
        }
    }
}
