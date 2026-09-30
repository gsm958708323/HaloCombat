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
        readonly ComboTableSO _comboTable = new ComboTableSO();
        sealed class Factory : IActorFactory
        {
            readonly TimelineLibrary _timelines;
            readonly SkillCatalog _skills;
            readonly ComboTableSO _combo;
            public Factory(TimelineLibrary timelines, SkillCatalog skills, ComboTableSO combo) { _timelines = timelines; _skills = skills; _combo = combo; }
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
                a.AddComp(new ComboComp(_combo));
                return a;
            }
            public void Release(Actor actor) => actor.ResetForPool();
        }
        [SetUp] public void Setup()
        {
            _comboTable.Entries = Array.Empty<ComboEntry>();
            _timelines = new TimelineLibrary(); _skills = new SkillCatalog();
            _timelines.Register(new TimelineSO { Id = new TimelineId(9001), Duration = .1f });
            _timelines.Register(new TimelineSO { Id = new TimelineId(9002), Duration = .3f,
                ControlTags = new[] { CommonTags.BlockMove, CommonTags.BlockRotate, CommonTags.BlockSkill },
                Clips = new[] { new TimelineClip { Start = 0f, End = .3f, Kind = ClipKind.Move, MoveZ = 2f } } });
            _skills.Register(new SkillDefinition { Id = new SkillNodeId(9001), Timeline = new TimelineId(9001),
                Cooldown = .4f, CanUseInAir = true });
            var projectiles = new ProjectileCatalog();
            projectiles.Register(new ProjectileDefinition { SpecId = 9001, Speed = 2f, Lifetime = 10f });
            _world = new CombatWorld(new Factory(_timelines, _skills, _comboTable), new WorldInstall {
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
        [Test] public void ComboResolutionWaitsForCancelAndNeverConsumesOnItsOwn()
        {
            var table = new ComboTableSO { Entries = new[] { new ComboEntry {
                PreSkills = new[] { new SkillNodeId(9001) }, Input = InputToken.Attack,
                RequiredTags = Array.Empty<int>(), ToSkill = new SkillNodeId(9002), Timeline = new TimelineId(9002) } } };
            _comboTable.Entries = table.Entries;
            var combo = _actor.GetComp<ComboComp>();
            var director = _actor.GetComp<SkillDirectorComp>();
            director.Play(new SkillNodeId(9001));
            var input = _actor.GetComp<InputBufferComp>(); input.Push(InputToken.Attack);
            Assert.IsFalse(combo.TryResolve(out _)); Assert.AreEqual(1, input.Count);
            var tags = _actor.GetComp<TagComp>(); var cancel = tags.Acquire(CommonTags.Cancel);
            Assert.IsTrue(combo.TryResolve(out var result)); Assert.AreEqual(1, input.Count);
            var block = tags.Acquire(CommonTags.BlockSkill);
            Assert.IsFalse(director.Play(result.ToSkill, result.Timeline)); Assert.AreEqual(1, input.Count);
            block.Release();
            _actor.Time.RequestHitstop(3); Step(3);
            Assert.IsFalse(combo.TryResolve(out _)); Assert.AreEqual(1, input.Count);
            Step(1); Assert.IsTrue(combo.TryResolve(out result));
            input.Push(InputToken.Skill1);
            Assert.IsTrue(director.Play(result.ToSkill, result.Timeline)); combo.ConsumeInput();
            Assert.AreEqual(1, input.Count); cancel.Release();
        }
        [TestCase(ActivityId.Hit)] [TestCase(ActivityId.Knockdown)] [TestCase(ActivityId.Dead)]
        public void HardInterruptClearsComboAndPreservesExternalLease(ActivityId activity)
        {
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9002), new TimelineId(9002));
            var q = _actor.GetComp<InputBufferComp>(); q.Push(InputToken.Attack); q.PrimaryHeld = true;
            var tags = _actor.GetComp<TagComp>(); var lease = tags.Acquire(CommonTags.BlockRotate);
            _actor.GetComp<HitboxComp>().Open(Array.Empty<IEffect>(), 1f, SimVec3.Zero);
            _actor.GetComp<StateMachineComp>().TryEnter(activity, default);
            Assert.IsFalse(d.IsPlaying); Assert.AreEqual(SkillNodeId.None, d.CurrentSkill);
            Assert.AreEqual(0, q.Count); Assert.IsFalse(q.PrimaryHeld);
            Assert.IsTrue(tags.Has(CommonTags.BlockRotate)); lease.Release();
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
        [Test] public void HitRefreshDoesNotMultiplyLeasesAndClearsQueue()
        {
            var director = _actor.GetComp<SkillDirectorComp>();
            director.Play(new SkillNodeId(9002), new TimelineId(9002));
            var q = _actor.GetComp<InputBufferComp>(); q.Push(InputToken.Skill1);
            var sm = _actor.GetComp<StateMachineComp>();
            sm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { HitDuration = .1f });
            sm.TryEnter(ActivityId.Hit, new ActivityEnterArgs { HitDuration = .1f });
            Assert.AreEqual(1, _actor.GetComp<TagComp>().Stack(CommonTags.BlockMove));
            Assert.IsFalse(director.IsPlaying); Assert.AreEqual(0, q.Count);
            Step(6); Assert.IsFalse(_actor.GetComp<TagComp>().Has(CommonTags.BlockMove));
            Assert.AreEqual(0, q.Count);
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
        [Test] public void HostileHitGrantsComboConfirmIndependentOfDamageAndComboCheckConsumesOnlyItsLease()
        {
            var target = Spawn("enemy", 10f);
            var director = _actor.GetComp<SkillDirectorComp>();
            Assert.IsTrue(director.Play(new SkillNodeId(9001)));
            var tags = _actor.GetComp<TagComp>();
            var external = tags.Acquire(CommonTags.ComboConfirm);

            target.GetComp<AttributeSet>().SetBase(AttrId.Shield, 100f);
            _world.Deliver(new IEffect[] { new DamageEffect { Flat = 0f, CanCrit = false } }, _actor, target, 0f);
            Assert.AreEqual(2, tags.Stack(CommonTags.ComboConfirm));
            director.Stop(DirectorStopReason.Finished);

            var input = _actor.GetComp<InputBufferComp>();
            input.Push(InputToken.Attack);
            Assert.IsFalse(_actor.GetComp<ComboComp>().TryResolve(out _));
            Assert.AreEqual(1, tags.Stack(CommonTags.ComboConfirm));
            Assert.AreEqual(1, input.Count);
            external.Release();
        }

        [Test] public void ComboConfirmRejectsFriendlyAndStaleCastButAcceptsInvulnerableHostileHit()
        {
            var hostile = Spawn("enemy", 10f);
            var friendly = Spawn("unit", 20f);
            var director = _actor.GetComp<SkillDirectorComp>();
            Assert.IsTrue(director.Play(new SkillNodeId(9001)));
            var cast = director.CurrentCastId;
            var tags = _actor.GetComp<TagComp>();
            _world.Deliver(new IEffect[] { new DamageEffect() }, _actor, friendly, 0f, castId: cast);
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));

            director.Stop(DirectorStopReason.Finished);
            Assert.IsTrue(director.Play(new SkillNodeId(9001), new TimelineId(9001)));
            _world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f } }, _actor, hostile, 0f, castId: cast);
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
            hostile.GetComp<TagComp>().Add(CommonTags.Invincible, 1, TagSource.Effect("test"));
            _world.Deliver(new IEffect[] { new DamageEffect() }, _actor, hostile, 0f, castId: director.CurrentCastId);
            Assert.IsTrue(tags.Has(CommonTags.ComboConfirm));
        }
        [TestCase("damage", true)] [TestCase("shield", false)] [TestCase("zero", false)]
        [TestCase("invincible", false)] [TestCase("iframe", false)]
        public void HitConfirmationDoesNotDependOnHpButAfterDamageStillDoes(string mode, bool hpLoss)
        {
            var target = Spawn("enemy", 10f);
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            var attr = target.GetComp<AttributeSet>(); float hp = attr.GetBase(AttrId.Hp);
            if (mode == "shield") attr.SetBase(AttrId.Shield, 100f);
            if (mode == "invincible") target.GetComp<TagComp>().Acquire(CommonTags.Invincible);
            if (mode == "iframe") target.GetComp<HealthComp>().BeginIFrame(1f);
            int afterDamage = 0;
            var bag = new IEffect[] {
                new DamageEffect { Coeff = 0f, Flat = mode == "zero" ? 0f : 10f, FireOnHurted = false },
                new AfterDamageEffect { Effects = new IEffect[] { new CallbackEffect(() => afterDamage++) } }
            };
            _world.Deliver(bag, _actor, target, 0f, castId: d.CurrentCastId);
            _world.Deliver(bag, _actor, target, 0f, castId: d.CurrentCastId);
            Assert.AreEqual(1, _actor.GetComp<TagComp>().Stack(CommonTags.ComboConfirm));
            Near(hpLoss ? hp - 20f : hp, attr.GetBase(AttrId.Hp));
            Assert.AreEqual(hpLoss ? 2 : 0, afterDamage);
            Assert.AreEqual(0, _actor.GetComp<BuffComp>().Count);
        }

        [Test] public void NonHitDeliveryAndInvalidSourceOrCastDoNotConfirm()
        {
            var target = Spawn("enemy", 10f);
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            _world.Deliver(new IEffect[] { new CallbackEffect(() => { }) }, _actor, target, 0f);
            var tags = _actor.GetComp<TagComp>(); Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
            var ctx = new EffectContext { World = _world, Source = _actor, Target = target };
            var damage = new DamageEffect { Coeff = 0f, FireOnHurted = false };
            damage.Apply(ref ctx);
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
            ctx.CastId = d.CurrentCastId; ctx.Source = null; damage.Apply(ref ctx);
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
            ctx.Source = _actor; ctx.Target = null; damage.Apply(ref ctx);
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
        }

        [TestCase(true)] [TestCase(false)]
        public void ComboCheckConsumesConfirmForBothMatchResultsButPreservesInput(bool matches)
        {
            var target = Spawn("enemy", 10f);
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            _world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f } }, _actor, target, 0f);
            _comboTable.Entries = new[] { new ComboEntry {
                PreSkills = new[] { new SkillNodeId(9001) }, Input = InputToken.Attack,
                RequiredTags = matches ? new[] { CommonTags.ComboConfirm.Value } : new[] { CommonTags.ComboConfirm.Value, 99999 },
                ToSkill = new SkillNodeId(9001), Timeline = new TimelineId(9001) } };
            var tags = _actor.GetComp<TagComp>(); tags.Acquire(CommonTags.Cancel);
            var input = _actor.GetComp<InputBufferComp>(); input.Push(InputToken.Attack);
            Assert.AreEqual(matches, _actor.GetComp<ComboComp>().TryResolve(out var result));
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm)); Assert.AreEqual(1, input.Count);
            if (matches)
            {
                Assert.IsFalse(d.Play(result.ToSkill), "Cooldown rejects the selected skill.");
                Assert.IsFalse(tags.Has(CommonTags.ComboConfirm), "Rejected casts must not restore confirmation.");
                Assert.AreEqual(1, input.Count);
            }
        }

        [Test] public void NaturalEndPreservesConfirmUntilAllComboCandidatesAndFallbackAreChecked()
        {
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            var target = Spawn("enemy", 10f);
            _world.Deliver(new IEffect[] { new DamageEffect { Coeff = 0f } }, _actor, target, 0f);
            Step(6);
            Assert.IsFalse(d.IsPlaying);
            Assert.AreEqual(new SkillNodeId(9001), d.ComboSourceSkill);
            var tags = _actor.GetComp<TagComp>(); Assert.IsTrue(tags.Has(CommonTags.ComboConfirm));
            _comboTable.Entries = new[] {
                new ComboEntry { PreSkills = new[] { new SkillNodeId(9001) }, Input = InputToken.Attack,
                    RequiredTags = new[] { 99999 }, ToSkill = new SkillNodeId(9001) },
                new ComboEntry { Input = InputToken.Attack, RequiredTags = new[] { CommonTags.ComboConfirm.Value },
                    Priority = 1, ToSkill = new SkillNodeId(9001) },
                new ComboEntry { Input = InputToken.Attack, RequiredTags = new[] { CommonTags.ComboConfirm.Value },
                    Priority = 2, ToSkill = new SkillNodeId(9002) }
            };
            Assert.IsTrue(_actor.GetComp<ComboComp>().TryResolveCurrent(InputToken.Attack, out var result));
            Assert.AreEqual(new SkillNodeId(9002), result.ToSkill);
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
        }

        [Test] public void WaitingForInputOrCancelDoesNotRunAComboCheck()
        {
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            var target = Spawn("enemy", 10f);
            var bag = new IEffect[] { new DamageEffect { Coeff = 0f } };
            _world.Deliver(bag, _actor, target, 0f);
            var combo = _actor.GetComp<ComboComp>(); var tags = _actor.GetComp<TagComp>();
            Assert.IsFalse(combo.TryResolve(out _)); Assert.IsTrue(tags.Has(CommonTags.ComboConfirm));
            _actor.GetComp<InputBufferComp>().Push(InputToken.Attack);
            Assert.IsFalse(combo.TryResolve(out _)); Assert.IsTrue(tags.Has(CommonTags.ComboConfirm));
            tags.Acquire(CommonTags.Cancel);
            Assert.IsFalse(combo.TryResolve(out _)); Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
            _world.Deliver(bag, _actor, target, 0f); // A later hit grants fresh confirmation.
            Assert.AreEqual(1, tags.Stack(CommonTags.ComboConfirm));
        }

        [TestCase(ActivityId.Hit)] [TestCase(ActivityId.Knockdown)] [TestCase(ActivityId.Dead)]
        public void HardInterruptClearsConfirmAndInvalidatesDelayedHits(ActivityId activity)
        {
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            var cast = d.CurrentCastId; var target = Spawn("enemy", 10f);
            var bag = new IEffect[] { new DamageEffect { Coeff = 0f } };
            _world.Deliver(bag, _actor, target, 0f, castId: cast);
            var tags = _actor.GetComp<TagComp>(); var external = tags.Acquire(CommonTags.ComboConfirm);
            _actor.GetComp<StateMachineComp>().TryEnter(activity, default);
            Assert.AreEqual(1, tags.Stack(CommonTags.ComboConfirm));
            _world.Deliver(bag, _actor, target, 0f, castId: cast);
            Assert.AreEqual(1, tags.Stack(CommonTags.ComboConfirm));
            external.Release(); Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
        }

        [Test] public void ManualStopAndDespawnReleaseConfirm()
        {
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            var target = Spawn("enemy", 10f); var tags = _actor.GetComp<TagComp>();
            var bag = new IEffect[] { new DamageEffect { Coeff = 0f } };
            _world.Deliver(bag, _actor, target, 0f);
            d.Stop(DirectorStopReason.Manual); Assert.IsFalse(tags.Has(CommonTags.ComboConfirm));
            d.Play(new SkillNodeId(9001), new TimelineId(9001));
            _world.Deliver(bag, _actor, target, 0f); Assert.IsTrue(tags.Has(CommonTags.ComboConfirm));
            _world.RequestDespawn(_actor.Id); Step(1);
            Assert.IsFalse(tags.Has(CommonTags.ComboConfirm)); Assert.IsFalse(d.LastCastId.IsValid);
        }

        [TestCase(false)] [TestCase(true)]
        public void ImmuneProjectileContactConfirmsOnlyLatestCastAndPreservesPassThrough(bool stale)
        {
            var d = _actor.GetComp<SkillDirectorComp>(); d.Play(new SkillNodeId(9001));
            var cast = d.CurrentCastId;
            if (stale) d.Play(new SkillNodeId(9001), new TimelineId(9001));
            var target = Spawn("enemy", 0f); target.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, .4f);
            target.GetComp<HealthComp>().BeginIFrame(1f);
            _world.Intents.Post(new SpawnProjectileIntent(_actor.Id, 9001, SimVec3.Zero, 0f, 0f, castId: cast));
            Step(1);
            Assert.AreEqual(!stale, _actor.GetComp<TagComp>().Has(CommonTags.ComboConfirm));
            Near(100f, target.GetComp<AttributeSet>().GetBase(AttrId.Hp));
            ProjectileComp body = null;
            foreach (var actor in _world.RegistryActive()) if (actor.TryGetComp<ProjectileComp>(out var p)) body = p;
            Assert.IsNotNull(body); Assert.AreEqual(0, body.HitCount); Assert.IsFalse(body.Exhausted);
        }

        [Test] public void FeedbackLevelsProvideTieredHitstopDefaults()
        {
            Assert.AreEqual(2, HitFeedbackRules.TargetFrames(HitFeedbackLevel.Light));
            Assert.AreEqual(3, HitFeedbackRules.TargetFrames(HitFeedbackLevel.ComboConfirm, 2));
            Assert.AreEqual(1, HitFeedbackRules.SourceFrames(HitFeedbackLevel.ComboConfirm));
            Assert.AreEqual(4, HitFeedbackRules.TargetFrames(HitFeedbackLevel.Finisher));
            Assert.AreEqual(0, HitFeedbackRules.TargetFrames(HitFeedbackLevel.None));
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
