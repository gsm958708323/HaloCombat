using System;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    public sealed class CombatControlTests
    {
        CombatWorld _world;
        TimelineLibrary _timelines;
        SkillCatalog _skills;
        Actor _actor;
        sealed class Factory : IActorFactory
        {
            readonly TimelineLibrary _timelines;
            readonly SkillCatalog _skills;
            public Factory(TimelineLibrary timelines, SkillCatalog skills) { _timelines = timelines; _skills = skills; }
            public Actor Create(in ActorSpawnSpec spec)
            {
                var a = new Actor();
                a.AddComp(new TransformComp()); a.AddComp(new TagComp());
                a.AddComp(new TeamComp(spec.BlueprintId == "enemy" ? 2 : 1));
                if (spec.BlueprintId == "projectile") { a.AddComp(new ProjectileComp()); return a; }
                var attr = new AttributeSet(); attr.InitFighterDefaults();
                attr.SetBase(AttrId.Def, 0f); attr.SetBase(AttrId.Hp, 1000f);
                a.AddComp(attr); a.AddComp(new HealthComp()); a.AddComp(new StateMachineComp());
                a.AddComp(new LocomotionComp()); a.AddComp(new InputBufferComp()); a.AddComp(new HitboxComp());
                a.AddComp(new SkillDirectorComp(_timelines, _skills)); a.AddComp(new BuffComp());
                return a;
            }
            public void Release(Actor actor) => actor.ResetForPool();
        }
        [SetUp] public void Setup()
        {
            _timelines = new TimelineLibrary(); _skills = new SkillCatalog();
            _timelines.Register(new TimelineSO { Id = new TimelineId(9001), Duration = .1f });
            _timelines.Register(new TimelineSO { Id = new TimelineId(9002), Duration = .3f,
                ControlTags = new[] { CommonTags.BlockMove, CommonTags.BlockRotate, CommonTags.BlockSkill },
                Clips = new[] { new TimelineClip { Start = 0f, End = .3f, Kind = ClipKind.Move, MoveZ = 2f } } });
            _skills.Register(new SkillDefinition { Id = new SkillNodeId(9001), Timeline = new TimelineId(9001),
                Cooldown = .4f, CanUseInAir = true });
            var projectiles = new ProjectileCatalog();
            projectiles.Register(new ProjectileDefinition { SpecId = 9001, Speed = 2f, Lifetime = 10f });
            _world = new CombatWorld(new Factory(_timelines, _skills), new WorldInstall {
                Projectiles = projectiles, Random = new FixedRandom(1f) });
            _actor = Spawn("unit", 0f);
        }
        [TearDown] public void TearDown() => _world?.Shutdown();
        Actor Spawn(string bp, float x)
        {
            var id = _world.SpawnActor(new ActorSpawnSpec(bp)); _world.TryGetActor(id, out var actor);
            actor.GetComp<TransformComp>().Position = new SimVec3(x, 0f, 0f); return actor;
        }
        void Step(int n, float dt = .02f) { for (int i = 0; i < n; i++) _world.Tick(dt); }
        static void Near(float expected, float actual) => Assert.That(actual, Is.EqualTo(expected).Within(.0001f));

        [Test] public void QueueIsFifoDropsOldestAndKeepsRepeatedPresses()
        {
            var q = _actor.GetComp<InputBufferComp>();
            q.Push(InputToken.Skill1); q.Push(InputToken.Skill2); q.Push(InputToken.Skill2); q.Push(InputToken.Skill3);
            Assert.AreEqual(3, q.Count);
            foreach (var expected in new[] { InputToken.Skill2, InputToken.Skill2, InputToken.Skill3 })
            { Assert.IsTrue(q.TryPeek(out var token)); Assert.AreEqual(expected, token); Assert.IsTrue(q.Consume()); }
            Assert.IsFalse(q.Consume());
        }
        [Test] public void QueueExpiryAndBuffDurationPauseWithActor()
        {
            var q = _actor.GetComp<InputBufferComp>(); q.Push(InputToken.Attack);
            var buffs = _actor.GetComp<BuffComp>();
            buffs.Apply(new DurationSpec { BuffId = 1, Duration = .3f, GrantedTags = new[] { CommonTags.BlockRotate } }, _actor, 1);
            Step(5); float time = _actor.Time.Time;
            _actor.Time.RequestHitstop(50); Step(50);
            Near(time, _actor.Time.Time); Assert.AreEqual(1, buffs.Count); Assert.AreEqual(1, q.Count);
            Step(11); Assert.AreEqual(0, buffs.Count); Assert.AreEqual(1, q.Count);
            Step(26); Assert.IsFalse(q.HasBuffered);
        }
        [Test] public void LeasesAreIndependentIdempotentAndSurviveDirectRemoval()
        {
            var tags = _actor.GetComp<TagComp>();
            var a = tags.Acquire(CommonTags.BlockRotate); var b = tags.Acquire(CommonTags.BlockRotate);
            tags.Remove(CommonTags.BlockRotate, 100, default); a.Release(); a.Release();
            Assert.AreEqual(1, tags.Stack(CommonTags.BlockRotate));
            b.Release(); Assert.IsFalse(tags.Has(CommonTags.BlockRotate));
            var old = tags.Acquire(CommonTags.BlockRotate); tags.ClearAll();
            var current = tags.Acquire(CommonTags.BlockRotate); old.Release();
            Assert.AreEqual(1, tags.Stack(CommonTags.BlockRotate)); current.Release();
        }
        [Test] public void StatusBuffsImplyControlWithoutLeakingOnDispel()
        {
            var buffs = _actor.GetComp<BuffComp>(); var tags = _actor.GetComp<TagComp>();
            buffs.Apply(new DurationSpec { BuffId = 2, GrantedTags = new[] { CommonTags.Stunned } }, _actor, 1);
            Assert.IsTrue(tags.Has(CommonTags.BlockMove)); Assert.IsTrue(tags.Has(CommonTags.BlockSkill));
            buffs.ClearAllWithExpire(); Assert.IsFalse(tags.Has(CommonTags.BlockMove));
            Assert.IsTrue(_actor.GetComp<SkillDirectorComp>().CanStartSkill);
        }
        [Test] public void TimelineMovementAndLocksReleaseOnlyTheirOwnLayers()
        {
            var tags = _actor.GetComp<TagComp>(); var external = tags.Acquire(CommonTags.BlockRotate);
            var loco = _actor.GetComp<LocomotionComp>(); var tf = _actor.GetComp<TransformComp>();
            float yaw = tf.YawDegrees; loco.RequestAimYaw(180f);
            Assert.IsTrue(_actor.GetComp<SkillDirectorComp>().Play(new SkillNodeId(9002), new TimelineId(9002)));
            loco.RequestMoveIntent(1f, 0f); Step(16);
            Near(yaw, tf.YawDegrees); Near(2f, tf.Position.X);
            Assert.IsTrue(tags.Has(CommonTags.BlockRotate)); Assert.IsFalse(tags.Has(CommonTags.BlockMove));
            external.Release(); loco.RequestMoveIntent(0f, 0f);
            loco.RequestSnapYawDegrees(180f); Step(1); Near(180f, tf.YawDegrees);
        }
        [Test] public void FrozenTimelineRetainsLocksAndDefersCompletion()
        {
            var director = _actor.GetComp<SkillDirectorComp>();
            director.Play(new SkillNodeId(9002), new TimelineId(9002));
            Step(2); var position = _actor.GetComp<TransformComp>().Position;
            _actor.Time.RequestHitstop(30); Step(30);
            Assert.IsTrue(director.IsPlaying);
            Assert.IsTrue(_actor.GetComp<TagComp>().Has(CommonTags.BlockMove));
            Near(position.Z, _actor.GetComp<TransformComp>().Position.Z);
            Step(16); Assert.IsFalse(director.IsPlaying);
            Assert.IsFalse(_actor.GetComp<TagComp>().Has(CommonTags.BlockMove));
        }
        [Test] public void FrozenAttackerDefersHitboxQueriesUntilResume()
        {
            var target = Spawn("enemy", .1f);
            var attrs = target.GetComp<AttributeSet>();
            float hp = attrs.GetFinal(AttrId.Hp);
            _actor.GetComp<HitboxComp>().Open(new IEffect[] {
                new DamageEffect { Coeff = 0f, Flat = 10f } }, 2f, SimVec3.Zero);
            _actor.Time.RequestHitstop(3); Step(3);
            Near(hp, attrs.GetFinal(AttrId.Hp));
            Step(1); Near(hp - 10f, attrs.GetFinal(AttrId.Hp));
        }
        [Test] public void HitRefreshDoesNotMultiplyLeasesOrDiscardQueue()
        {
            var director = _actor.GetComp<SkillDirectorComp>();
            director.Play(new SkillNodeId(9002), new TimelineId(9002));
            var q = _actor.GetComp<InputBufferComp>(); q.Push(InputToken.Skill1);
            var sm = _actor.GetComp<StateMachineComp>();
            sm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { HitDuration = .1f });
            sm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { HitDuration = .1f });
            Assert.AreEqual(1, _actor.GetComp<TagComp>().Stack(CommonTags.BlockMove));
            Assert.IsFalse(director.IsPlaying); Assert.AreEqual(1, q.Count);
            Step(6); Assert.IsFalse(_actor.GetComp<TagComp>().Has(CommonTags.BlockMove));
            Assert.AreEqual(1, q.Count);
        }
        [Test] public void HitMotionIsDeferredDuringFreezeAndAppliedAfterResume()
        {
            _actor.GetComp<StateMachineComp>().TryEnter(ActivityId.Hit, new ActivityEnterArgs { HitDuration = 1f });
            var loco = _actor.GetComp<LocomotionComp>();
            _actor.Time.RequestHitstop(2); Step(1); loco.RequestHitDelta(1f, 0f, 0f);
            Step(1); Near(0f, _actor.GetComp<TransformComp>().Position.X);
            Step(1); Near(1f, _actor.GetComp<TransformComp>().Position.X);
        }
        [Test] public void LocalHitstopDoesNotStopOtherActorsOrProjectiles()
        {
            var other = Spawn("unit", 20f);
            _world.Intents.Post(new SpawnProjectileIntent(_actor.Id, 9001, new SimVec3(0,0,10), 0f, 1f));
            Step(1); Actor projectile = null;
            foreach (var a in _world.RegistryActive()) if (a.TryGetComp<ProjectileComp>(out _)) projectile = a;
            float z = projectile.GetComp<TransformComp>().Position.Z, t = _actor.Time.Time;
            _actor.Time.RequestHitstop(2); _actor.Time.RequestHitstop(4); Step(4);
            Near(t, _actor.Time.Time); Assert.IsTrue(_actor.Time.IsStopped); Assert.IsFalse(_world.InHitstop);
            Assert.Greater(other.Time.Time, t); Assert.Greater(projectile.GetComp<TransformComp>().Position.Z, z);
            Step(1); Assert.IsFalse(_actor.Time.IsStopped); Assert.Greater(_actor.Time.Time, t);
        }
        [Test] public void WorldStopDoesNotSpendActorHitstopFrames()
        {
            _actor.Time.RequestHitstop(3); _world.RequestHitstop(4); Step(4);
            Near(0f, _actor.Time.Time); Step(3); Near(0f, _actor.Time.Time);
            Step(1); Near(.02f, _actor.Time.Time);
        }
        [Test] public void CooldownUsesActorTime()
        {
            var d = _actor.GetComp<SkillDirectorComp>(); Assert.IsTrue(d.Play(new SkillNodeId(9001)));
            Step(6); _actor.Time.RequestHitstop(30); Step(30);
            Assert.IsFalse(d.Play(new SkillNodeId(9001))); Step(16); Assert.IsTrue(d.Play(new SkillNodeId(9001)));
        }
        [Test] public void DamageFreezesOnlyConfiguredActorAndStillKillsFrozenTargets()
        {
            var target = Spawn("enemy", 10f);
            _world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f, Flat = 1f, TargetHitstopFrames = 2 } }, _actor, target, 0f);
            Step(1); Assert.IsTrue(target.Time.IsStopped); Assert.IsFalse(_actor.Time.IsStopped);
            var q = target.GetComp<InputBufferComp>(); q.Push(InputToken.Skill1);
            _world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f, Flat = 9999f } }, _actor, target, 0f);
            Assert.IsTrue(target.GetComp<TagComp>().Has(CommonTags.Dead)); Assert.AreEqual(0, q.Count);
        }
        [Test] public void ZeroDamageAndImmuneHitsDoNotRequestHitstop()
        {
            var target = Spawn("enemy", 10f);
            var effect = new DamageEffect { Coeff = 0f, Flat = 0f, SourceHitstopFrames = 3, TargetHitstopFrames = 3 };
            _world.Deliver(new IEffect[] { effect }, _actor, target, 0f); Step(1);
            Assert.IsFalse(target.Time.IsStopped); Assert.IsFalse(_actor.Time.IsStopped);
            target.GetComp<HealthComp>().BeginIFrame(1f); effect.Flat = 10f;
            _world.Deliver(new IEffect[] { effect }, _actor, target, 0f); Step(1);
            Assert.IsFalse(target.Time.IsStopped);
        }
        [Test] public void ResetForPoolClearsTimeAndOldLeasesCannotReleaseNewOnes()
        {
            var tags = _actor.GetComp<TagComp>(); var old = tags.Acquire(CommonTags.BlockRotate);
            _actor.Time.RequestHitstop(8); Step(1); _world.RequestDespawn(_actor.Id); Step(1);
            Assert.IsFalse(_actor.Time.IsStopped); Near(0f, _actor.Time.Time);
            var lease = tags.Acquire(CommonTags.BlockRotate); old.Release();
            Assert.AreEqual(1, tags.Stack(CommonTags.BlockRotate)); lease.Release();
        }
        [TestCase(ProjectileMotionKind.Linear)]
        [TestCase(ProjectileMotionKind.ImmediateHoming)]
        [TestCase(ProjectileMotionKind.Accelerate)]
        [TestCase(ProjectileMotionKind.ReturnToOwner)]
        [TestCase(ProjectileMotionKind.Bounce)]
        public void MotionStrategiesMatchLegacyEquationsAtSeveralSteps(ProjectileMotionKind kind)
        {
            var d = new ProjectileDefinition { Motion = kind, Speed = 3f, MotionParam = 1f,
                BounceHeight = 1f, GroundPhaseAt = new[] { 1f, 1.6667f, 1.999f } };
            foreach (float dt in new[] { .02f, .07f, .2f })
            {
                var p = new SimVec3(0,0,0); float age = 0f;
                for (int i = 0; i < 10; i++)
                {
                    float next = age + dt;
                    var r = ProjectileMotions.Resolve(kind).Evaluate(new ProjectileMotionContext(d,p,0f,age,next,0f,dt,new SimVec3(0,0,-10)));
                    float scale = 1f;
                    if (kind == ProjectileMotionKind.Accelerate) scale = 2f * age / (age + 1f);
                    if (kind == ProjectileMotionKind.ReturnToOwner)
                        scale = age < 1f ? (float)Math.Sin(age * Math.PI)+.1f : -((float)Math.Sin(Math.Min((age-1f)*Math.PI,.5))+.1f);
                    Near(p.Z + 3f * scale * dt,r.Position.Z);
                    bool touchdown = false; foreach (float t in d.GroundPhaseAt) touchdown |= t > age && t <= next;
                    Assert.AreEqual(touchdown,r.Touchdown);
                    if (kind == ProjectileMotionKind.Bounce && touchdown) Near(0f,r.Position.Y);
                    p=r.Position; age=next;
                }
            }
        }
    }
}
