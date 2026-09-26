using System;
using System.Collections;
using System.Collections.Generic;
using Combat.Core;
using Combat.Unity.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Combat.Tests
{
    /// <summary>
    /// Drives the real Arena session and asserts that every fire key produces the body it is
    /// supposed to. This is the guard for the class of bug where a baked effect lost its
    /// configuration (for example a projectile spec id of 0, or an effect that deserialised as
    /// null), so every skill cast but spawned nothing while the game reported no errors at all.
    /// </summary>
    public sealed partial class BuffArenaSkillSmokeTests
    {
        const float Step = 1f / 50f;
        const int TicksPerSkill = 70;
        const float EnemyTestHp = 100f;
        const int BombProjectileSpec = 4005;
        const int ExplosionAoeSpec = 4104;
        const int StayingBombAoeSpec = 4105;

        // The Arena takes the player's facing from the mouse ray, so a test that wants a
        // deterministic shot has to place the target on an aim yaw it can pin for the cast.
        float _aimYaw;

        BuffArenaBootstrap _bootstrap;
        BuffArenaSession _session;

        [UnitySetUp]
        public IEnumerator LoadArena()
        {
            // The editor throttles the player loop while its window is not focused, which would
            // stall an unattended run before the first frame.
            Application.runInBackground = true;

            SceneManager.LoadScene("Arena");
            yield return null;

            _bootstrap = UnityEngine.Object.FindObjectOfType<BuffArenaBootstrap>();
            Assert.IsNotNull(_bootstrap, "Arena scene has no BuffArenaBootstrap.");
            _session = _bootstrap.Session;
            Assert.IsNotNull(
                _session,
                "BuffArenaSession did not start; the generated content is unusable: "
                    + (_bootstrap.Database != null
                        ? _bootstrap.Database.LastContentError
                        : "the bootstrap has no database assigned"));
            ClearNonPlayerActors();
        }

        [UnityTest]
        public IEnumerator Arena_ContentBakes()
        {
            Assert.IsNotNull(_bootstrap.Database, "Arena bootstrap has no database assigned.");
            var content = _bootstrap.Database.Bake();
            Assert.IsNotNull(
                content,
                "The authored content did not bake: " + _bootstrap.Database.LastContentError);
            Assert.IsTrue(content.Timelines.TryGet(new TimelineId(3002), out var roll));
            CollectionAssert.AreEquivalent(new[] { CommonTags.BlockMove, CommonTags.BlockRotate }, roll.ControlTags);
            Assert.IsTrue(content.Timelines.TryGet(new TimelineId(3005), out var boomerang));
            CollectionAssert.Contains(boomerang.ControlTags, CommonTags.BlockSkill);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlayerHealthComesFromPlayerActorDefinition()
        {
            Assert.IsTrue(
                _session.World.TryGetActor(_session.LocalPlayerId, out var player) && player != null,
                "The local player did not spawn.");
            var attr = player.GetComp<AttributeSet>();
            float configuredMaxHp = _session.Data.RequireActor(BuffArenaIds.PlayerBlueprint).MaxHp;
            Assert.AreEqual(configuredMaxHp, attr.GetBase(AttrId.MaxHp), 0.0001f);
            Assert.AreEqual(configuredMaxHp, attr.GetBase(AttrId.Hp), 0.0001f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire1_SpawnsProjectile()
        {
            int ammoBefore = PlayerAmmo();
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire1Held = true; return f; });
            Assert.Greater(peak, 0, "Fire1 (skill 1) spawned no projectile.");
            Assert.Less(PlayerAmmo(), ammoBefore, "Fire1 did not spend ammo, so the skill never ran.");
            yield return null;
        }

        /// <summary>
        /// The regression this guards: the projectile spawned and flew, but its hitbox payload had
        /// been baked with null effects, so it passed straight through the enemy. The target goes on
        /// a pinned aim yaw, because the Arena follows the mouse and the shot would otherwise fly
        /// wherever the cursor happens to point.
        /// </summary>
        [UnityTest]
        public IEnumerator Fire1_DamagesEnemyInFront()
        {
            var enemy = SpawnEnemyInFront(1.2f);
            float before = Hp(enemy);
            Assert.Greater(before, 0f, "The test enemy spawned with no health, so damage cannot be observed.");
            int ammoBefore = PlayerAmmo();

            Press(f => { f.Fire1Held = true; f.AimValid = true; f.AimYaw = _aimYaw; return f; });

            Assert.Less(PlayerAmmo(), ammoBefore, "Fire1 did not spend ammo, so the skill never ran.");
            float after = Hp(enemy);
            Assert.Less(
                after, before,
                "Fire1 spawned a projectile that dealt no damage: the enemy's health stayed at " + before
                    + ". Its hitbox payload is baked with null effects, so the hit does nothing.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire2_SpawnsProjectile()
        {
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire2Pressed = true; return f; });
            Assert.Greater(peak, 0, "Fire2 (skill 2) spawned no projectile.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire3_SpawnsProjectile()
        {
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire3Pressed = true; return f; });
            Assert.Greater(peak, 0, "Fire3 (skill 3) spawned no projectile.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire3_BounceTrajectoryUsesAllConfiguredGroundPhases()
        {
            var data = _bootstrap.Database.Bake();
            Assert.IsNotNull(data, "The Arena content did not bake: " + _bootstrap.Database.LastContentError);
            Assert.IsTrue(
                data.Projectiles.TryGet(BombProjectileSpec, out var bomb),
                "The baked content has no projectile 4005.");
            Assert.AreEqual(ProjectileMotionKind.Bounce, bomb.Motion, "4005 is not using bounce motion.");
            Assert.AreEqual(1f, bomb.BounceHeight, 0.0001f, "4005 bounce height changed.");
            CollectionAssert.AreEqual(
                new[] { 1f, 1.6667f, 1.999f },
                bomb.GroundPhaseAt,
                "4005 touchdown phases changed.");

            // Drive the actual baked 4005 through the core at a deliberately coarse and
            // non-50Hz step. The trace proves that crossing a configured age still enters
            // the ground collision mode even when no frame lands exactly on that age.
            var movement = new TraceMovementConstraint();
            var world = new CombatWorld(
                new BuffArenaActorFactory(data),
                new WorldInstall
                {
                    Events = new EventBus(),
                    Time = new CombatTime(),
                    Random = new FixedRandom(0f),
                    Cues = data.Cues,
                    Motor = data.Motor,
                    Movement = movement,
                    Projectiles = data.Projectiles,
                    Aoes = data.Aoes,
                    Summons = new SummonCatalog()
                });
            try
            {
                world.Intents.Post(new SpawnProjectileIntent(
                    EntityId.Invalid, BombProjectileSpec, SimVec3.Zero, 0f, 0f));
                for (int i = 0; i < 10; i++)
                    world.Tick(0.2f);

                var touchdowns = new List<ResolveSample>();
                float peak = 0f;
                for (int i = 0; i < movement.Samples.Count; i++)
                {
                    var sample = movement.Samples[i];
                    if (sample.Target.Y > peak) peak = sample.Target.Y;
                    if (!sample.Flying) touchdowns.Add(sample);
                }

                Assert.Greater(peak, 0.8f, "4005 never rose above the ground.");
                Assert.AreEqual(3, touchdowns.Count, "A touchdown was lost when the frame crossed its configured age.");
                for (int i = 0; i < touchdowns.Count; i++)
                    Assert.That(touchdowns[i].Target.Y, Is.EqualTo(0f).Within(0.0001f), "Touchdown " + i + " was not on the ground.");

                Assert.That(touchdowns[0].Target.Z, Is.EqualTo(0.4f + 3f * 1f).Within(0.01f));
                Assert.That(touchdowns[1].Target.Z, Is.EqualTo(0.4f + 3f * 1.8f).Within(0.01f));
                Assert.That(touchdowns[2].Target.Z, Is.EqualTo(0.4f + 3f * 2f).Within(0.01f));
            }
            finally
            {
                world.Shutdown();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire3_HitSpawnsExplosionDamagesEnemyAndPublishesCues()
        {
            var enemy = SpawnEnemyInFront(1.2f);
            float before = Hp(enemy);
            var cues = new List<EvCue>();
            var damages = new List<EvDamage>();
            Action<EvCue> onCue = e =>
            {
                if (e.CueId == BuffArenaIds.CueHitValue || e.CueId == BuffArenaIds.CueExplosionValue)
                    cues.Add(e);
            };
            Action<EvDamage> onDamage = e =>
            {
                if (e.Target == enemy.Id) damages.Add(e);
            };
            bool sawExplosion = false;
            _session.World.Events.Subscribe(onCue);
            _session.World.Events.Subscribe(onDamage);
            try
            {
                _session.ApplyInput(Fire3Input(_aimYaw));
                for (int i = 0; i < 60; i++)
                {
                    _session.PumpLogic(Step);
                    if (CountAoeSpec(ExplosionAoeSpec) > 0) sawExplosion = true;
                }

                Assert.AreEqual(0, CountProjectileSpec(BombProjectileSpec), "4005 survived its enemy hit.");
                Assert.IsTrue(sawExplosion, "Enemy hit spawned no active 4104 explosion.");
                Assert.Less(Hp(enemy), before, "The 4104 explosion did not damage the enemy.");
                Assert.IsTrue(ContainsDamageFor(damages, enemy.Id), "No damage event was published for the 4104 pulse.");
                Assert.IsTrue(ContainsCue(cues, BuffArenaIds.CueHitValue), "4104 did not publish hit cue 4204.");
                Assert.IsTrue(ContainsCue(cues, BuffArenaIds.CueExplosionValue), "4104 did not publish explosion cue 4206.");
            }
            finally
            {
                _session.World.Events.Unsubscribe(onCue);
                _session.World.Events.Unsubscribe(onDamage);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire3_NaturalExpiryCreatesExplosionThenStayingBombAndSecondExplosion()
        {
            var data = _bootstrap.Database.Bake();
            Assert.IsNotNull(data, "The Arena content did not bake: " + _bootstrap.Database.LastContentError);
            var movement = new TraceMovementConstraint();
            var install = new WorldInstall
            {
                Events = new EventBus(),
                Time = new CombatTime(),
                Random = new FixedRandom(0f),
                Cues = data.Cues,
                Motor = data.Motor,
                Movement = movement,
                Projectiles = data.Projectiles,
                Aoes = data.Aoes,
                Summons = new SummonCatalog()
            };
            var world = new CombatWorld(new BuffArenaActorFactory(data), install);
            var aoeSpawns = new List<AoeSpawnObservation>();
            var cues = new List<EvCue>();
            bool firstExplosionWasActiveWhenResidualSpawned = false;
            Action<EvEntitySpawn> onSpawn = e =>
            {
                if (e.BlueprintId != "aoe" || !world.TryGetActor(e.Id, out var aoe) || aoe == null ||
                    !aoe.TryGetComp<AoeComp>(out var body) || body.Def == null) return;
                if (body.Def.SpecId != StayingBombAoeSpec) return;
                firstExplosionWasActiveWhenResidualSpawned = CountAoeSpec(world, ExplosionAoeSpec) > 0;
                aoeSpawns.Add(new AoeSpawnObservation
                {
                    SpecId = body.Def.SpecId,
                    ViewBlueprintId = e.ViewBlueprintId
                });
            };
            world.Events.Subscribe(onSpawn);
            Action<EvCue> onCue = e =>
            {
                if (e.CueId == BuffArenaIds.CueExplosionValue) cues.Add(e);
            };
            world.Events.Subscribe(onCue);
            try
            {
                var intent = new SpawnProjectileIntent(
                    EntityId.Invalid, BombProjectileSpec, SimVec3.Zero, 0f, 0f);
                world.Intents.Post(intent);
                for (int i = 0; i < 10; i++)
                    world.Tick(0.2f);

                Assert.Greater(CountAoeSpec(world, ExplosionAoeSpec), 0, "Natural expiry did not leave its first active 4104.");
                Assert.AreEqual(1, CountSpecSpawns(aoeSpawns, StayingBombAoeSpec), "Natural expiry did not create 4105.");
                Assert.IsTrue(firstExplosionWasActiveWhenResidualSpawned, "4105 was not created after the first 4104.");
                Assert.AreEqual(0, CountProjectileSpec(world, BombProjectileSpec), "4005 survived natural expiry.");
                Assert.AreEqual(StayingBombAoeSpec, aoeSpawns[0].SpecId, "Natural expiry did not create 4105 after 4104.");
                Assert.AreEqual(
                    "buff_aoe_stayingbomb_view",
                    aoeSpawns[0].ViewBlueprintId,
                    "4105 is not using the existing staying-bomb view.");

                for (int i = 0; i < 14; i++)
                    world.Tick(0.2f);
                Assert.AreEqual(1, CountAoeSpec(world, StayingBombAoeSpec), "4105 ended before three seconds.");

                world.Tick(0.2f);
                Assert.AreEqual(0, CountAoeSpec(world, StayingBombAoeSpec), "4105 did not end after about three seconds.");
                Assert.Greater(CountAoeSpec(world, ExplosionAoeSpec), 0, "4105 expiry did not create the second active 4104.");
                world.Tick(0.2f);
                Assert.AreEqual(2, cues.Count, "The initial and residual 4104 explosions did not both publish cue 4206.");
            }
            finally
            {
                world.Events.Unsubscribe(onSpawn);
                world.Events.Unsubscribe(onCue);
                world.Shutdown();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire3_TerrainImpactCreatesOnlyInitialExplosion()
        {
            float yaw = FindYawWithTerrainAtGroundPhase();
            var aoeSpawns = new List<AoeSpawnObservation>();
            Action<EvEntitySpawn> onSpawn = SubscribeAoeSpawns(aoeSpawns);
            bool sawExplosion = false;
            try
            {
                _session.ApplyInput(Fire3Input(yaw));
                for (int i = 0; i < 120; i++)
                {
                    _session.PumpLogic(Step);
                    if (CountAoeSpec(ExplosionAoeSpec) > 0) sawExplosion = true;
                }

                Assert.IsTrue(sawExplosion, "Terrain impact did not create an active 4104.");
                Assert.AreEqual(0, CountSpecSpawns(aoeSpawns, StayingBombAoeSpec), "Terrain impact incorrectly created residual 4105.");
                Assert.AreEqual(0, CountProjectileSpec(BombProjectileSpec), "4005 survived terrain impact.");
            }
            finally
            {
                _session.World.Events.Unsubscribe(onSpawn);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire4_SpawnsProjectile()
        {
            int peak = PressAndWatch<ProjectileComp>(f => { f.Fire4Pressed = true; return f; });
            Assert.Greater(peak, 0, "Fire4 (skill 4) spawned no projectile.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire4_HoldingThroughTeleportDoesNotLaunchAgain()
        {
            int launches = 0;
            Action<EvEntitySpawn> onSpawn = e =>
            {
                if (e.BlueprintId != "projectile" ||
                    !_session.World.TryGetActor(e.Id, out var projectile) || projectile == null ||
                    !projectile.TryGetComp<ProjectileComp>(out var body) || body.Def == null ||
                    body.Def.SpecId != 4004) return;
                launches++;
            };
            _session.World.Events.Subscribe(onSpawn);
            try
            {
                var input = new BuffArenaInputFrame { Fire4Pressed = true };
                for (int i = 0; i < TicksPerSkill; i++)
                {
                    _session.ApplyInput(input);
                    input.Fire4Pressed = false;
                    _session.PumpLogic(Step);
                }

                Assert.AreEqual(1, launches, "Holding Fire4 queued another launch after its first cast.");
            }
            finally
            {
                _session.World.Events.Unsubscribe(onSpawn);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator Fire5_SpawnsBarrel()
        {
            int peak = PressAndWatch<BarrelComp>(f => { f.Fire5Pressed = true; return f; });
            Assert.Greater(peak, 0, "Fire5 (skill 5) spawned no barrel.");
            yield return null;
        }

        /// <summary>
        /// Pushes one input frame and lets the logic advance past the timeline.
        /// BuffArenaInputFrame is a struct, so an Action&lt;T&gt; lambda would mutate a copy and
        /// send an all-empty frame — every press has to hand its frame back out.
        /// </summary>
        void Press(Func<BuffArenaInputFrame, BuffArenaInputFrame> configure)
        {
            _session.ApplyInput(configure(new BuffArenaInputFrame()));
            for (int i = 0; i < TicksPerSkill; i++)
                _session.PumpLogic(Step);
        }

        /// <summary>
        /// Presses a key and reports the highest number of bodies of the given comp seen while the
        /// timeline ran: a projectile that already flew off or expired still counts as spawned.
        /// </summary>
        int PressAndWatch<T>(Func<BuffArenaInputFrame, BuffArenaInputFrame> configure) where T : Comp
        {
            _session.ApplyInput(configure(new BuffArenaInputFrame()));
            int peak = CountBodies<T>();
            for (int i = 0; i < TicksPerSkill; i++)
            {
                _session.PumpLogic(Step);
                int now = CountBodies<T>();
                if (now > peak) peak = now;
            }
            return peak;
        }

        /// <summary>
        /// Every key in BuffArenaSession.ApplyInput has to cast the skill declaring the token it
        /// pushes. The key map is code-owned; the token -> skill half is authored on SK_*.asset.
        /// The CLI demo that used to cover roll / homing / monkey is gone, so this is their guard.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryKeyCastsItsSkill()
        {
            AssertKeyCasts("Fire1", f => { f.Fire1Held = true; return f; });
            AssertKeyCasts("Fire2", f => { f.Fire2Pressed = true; return f; });
            AssertKeyCasts("Fire3", f => { f.Fire3Pressed = true; return f; });
            AssertKeyCasts("Fire4", f => { f.Fire4Pressed = true; return f; });
            AssertKeyCasts("Fire5", f => { f.Fire5Pressed = true; return f; });
            AssertKeyCasts("Roll", f => { f.RollPressed = true; return f; });
            AssertKeyCasts("Homing", f => { f.HomingPressed = true; return f; });
            AssertKeyCasts("Monkey", f => { f.MonkeyPressed = true; return f; });
            yield return null;
        }

        void AssertKeyCasts(string token, Func<BuffArenaInputFrame, BuffArenaInputFrame> configure)
        {
            var data = _session.Data;
            Assert.IsNotNull(data, "The session has no baked data.");
            var expected = FindSkillByToken(data, new InputToken(token));
            Assert.IsNotNull(
                expected,
                "No skill asset declares InputToken '" + token + "', so the bound key would do nothing.");

            RefillPlayerAmmo();
            WaitForDirectorIdle();

            _session.ApplyInput(configure(new BuffArenaInputFrame()));
            for (int i = 0; i < 3; i++) _session.PumpLogic(Step);

            var director = PlayerDirector();
            Assert.IsNotNull(director, "The player has no SkillDirectorComp.");
            Assert.IsTrue(
                director.IsPlaying,
                "The key that pushes token '" + token + "' started no skill.");
            Assert.AreEqual(
                expected.Id.Value, director.CurrentSkill.Value,
                "The key that pushes token '" + token + "' started skill "
                    + director.CurrentSkill.Value + " instead of " + expected.Id.Value + ".");
            WaitForDirectorIdle();
        }

        static BuffArenaSkill FindSkillByToken(BuffArenaData data, InputToken token)
        {
            for (int i = 0; i < data.Skills.Count; i++)
                if (data.Skills[i].Input == token) return data.Skills[i];
            return null;
        }

        SkillDirectorComp PlayerDirector()
        {
            if (!_session.World.TryGetActor(_session.LocalPlayerId, out var player) || player == null)
                return null;
            return player.TryGetComp<SkillDirectorComp>(out var director) ? director : null;
        }

        void WaitForDirectorIdle()
        {
            _session.ApplyInput(default);
            for (int i = 0; i < 200; i++)
            {
                var director = PlayerDirector();
                if (director == null || !director.IsPlaying) return;
                _session.PumpLogic(Step);
            }
        }

        void RefillPlayerAmmo()
        {
            if (!_session.World.TryGetActor(_session.LocalPlayerId, out var player) || player == null) return;
            if (player.TryGetComp<AmmoComp>(out var ammo)) ammo.Refill(ammo.Capacity);
        }

        int CountBodies<T>() where T : Comp
        {
            int count = 0;
            var actors = _session.World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] == null) continue;
                if (actors[i].TryGetComp<T>(out _)) count++;
            }
            return count;
        }

        /// <summary>
        /// Spawns one enemy with a known health pool on the first direction from the player that the
        /// map accepts, pins the shot to that yaw, and freezes the target's AI so it cannot wander
        /// out of the line of fire. The player starts on a random free cell, so straight ahead can
        /// be a wall; without the sweep the projectile would stop short and the test would read as
        /// "the hit does nothing".
        /// </summary>
        Actor SpawnEnemyInFront(float distance)
        {
            Assert.IsTrue(
                _session.World.TryGetActor(_session.LocalPlayerId, out var player) && player != null,
                "The local player is gone, so no shot origin exists.");
            var playerTf = player.GetComp<TransformComp>();
            var origin = playerTf.Position;
            float radius = player.GetComp<CharacterRadiusComp>().Radius;

            var target = new SimVec3();
            bool placed = false;
            for (int i = 0; i < 12 && !placed; i++)
            {
                float yaw = playerTf.YawDegrees + i * 30f;
                var forward = LocomotionComp.ForwardFromYaw(yaw);
                var candidate = new SimVec3(
                    origin.X + forward.X * distance, origin.Y, origin.Z + forward.Z * distance);
                if (!_session.Map.CanPlace(candidate, radius, false)) continue;
                _aimYaw = yaw;
                target = candidate;
                placed = true;
            }
            Assert.IsTrue(placed, "No clear line of fire around the player for the damage test.");

            var id = _session.World.SpawnActor(
                new ActorSpawnSpec(BuffArenaIds.EnemyBlueprint), publishSpawn: false);
            Assert.IsTrue(
                _session.World.TryGetActor(id, out var enemy) && enemy != null,
                "The test enemy could not be spawned from the generated actor table.");
            enemy.GetComp<TransformComp>().Position = target;
            // This test is about the hit landing, not about enemy AI, so the target stands still.
            if (enemy.TryGetComp<BehaviorTreeComp>(out var ai))
                ai.SetEnabled(false);
            var attr = enemy.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, EnemyTestHp);
            attr.SetBase(AttrId.Hp, EnemyTestHp);
            return enemy;
        }

        float Hp(Actor actor)
        {
            if (actor == null) return 0f;
            return actor.TryGetComp<AttributeSet>(out var attr) ? attr.GetBase(AttrId.Hp) : 0f;
        }

        int PlayerAmmo()
        {
            if (!_session.World.TryGetActor(_session.LocalPlayerId, out var player) || player == null)
                return -1;
            return player.TryGetComp<AmmoComp>(out var ammo) ? ammo.Current : -1;
        }

        /// <summary>
        /// Removes the wandering enemies so only the player's own skills are counted.
        /// The session spawns its first wave on its first logic step, so the removal runs after a
        /// pump and is repeated once. Otherwise a leftover enemy standing point-blank could eat a
        /// projectile on the very frame it spawns, which per-tick sampling cannot observe.
        /// </summary>
        void ClearNonPlayerActors()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                var actors = _session.World.RegistryActive();
                for (int i = 0; i < actors.Count; i++)
                {
                    var actor = actors[i];
                    if (actor == null || actor.Id == _session.LocalPlayerId) continue;
                    _session.World.RequestDespawn(actor.Id);
                }
                _session.PumpLogic(Step);
            }
        }

        BuffArenaInputFrame Fire3Input(float yaw)
        {
            return new BuffArenaInputFrame
            {
                Fire3Pressed = true,
                AimValid = true,
                AimYaw = yaw
            };
        }

        Action<EvEntitySpawn> SubscribeAoeSpawns(List<AoeSpawnObservation> destination)
        {
            Action<EvEntitySpawn> handler = e =>
            {
                if (e.BlueprintId != "aoe" || _session == null || _session.World == null) return;
                if (!_session.World.TryGetActor(e.Id, out var aoe) || aoe == null ||
                    !aoe.TryGetComp<AoeComp>(out var body) || body.Def == null) return;
                destination.Add(new AoeSpawnObservation
                {
                    SpecId = body.Def.SpecId,
                    ViewBlueprintId = e.ViewBlueprintId
                });
            };
            _session.World.Events.Subscribe(handler);
            return handler;
        }

        int CountAoeSpec(int specId)
        {
            int count = 0;
            var actors = _session.World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null && actors[i].TryGetComp<AoeComp>(out var body) &&
                    body.Def != null && body.Def.SpecId == specId)
                    count++;
            }
            return count;
        }

        static int CountAoeSpec(CombatWorld world, int specId)
        {
            int count = 0;
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null && actors[i].TryGetComp<AoeComp>(out var body) &&
                    body.Def != null && body.Def.SpecId == specId)
                    count++;
            }
            return count;
        }

        int CountProjectileSpec(int specId)
        {
            int count = 0;
            var actors = _session.World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null && actors[i].TryGetComp<ProjectileComp>(out var body) &&
                    body.Def != null && body.Def.SpecId == specId)
                    count++;
            }
            return count;
        }

        static int CountProjectileSpec(CombatWorld world, int specId)
        {
            int count = 0;
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] != null && actors[i].TryGetComp<ProjectileComp>(out var body) &&
                    body.Def != null && body.Def.SpecId == specId)
                    count++;
            }
            return count;
        }

        static int CountSpecSpawns(List<AoeSpawnObservation> spawns, int specId)
        {
            int count = 0;
            for (int i = 0; i < spawns.Count; i++)
                if (spawns[i].SpecId == specId) count++;
            return count;
        }

        static bool ContainsCue(List<EvCue> cues, int cueId)
        {
            for (int i = 0; i < cues.Count; i++)
                if (cues[i].CueId == cueId) return true;
            return false;
        }

        static bool ContainsDamageFor(List<EvDamage> damages, EntityId target)
        {
            for (int i = 0; i < damages.Count; i++)
                if (damages[i].Target == target && damages[i].Amount > 0f) return true;
            return false;
        }

        float FindYawWithTerrainAtGroundPhase()
        {
            Assert.IsTrue(
                _session.World.TryGetActor(_session.LocalPlayerId, out var player) && player != null,
                "The local player is gone, so the bomb has no launch origin.");
            var origin = player.GetComp<TransformComp>().Position;
            Assert.IsTrue(
                _session.Data.Projectiles.TryGet(BombProjectileSpec, out var bomb),
                "The baked content has no projectile 4005.");

            for (int i = 0; i < 24; i++)
            {
                float yaw = i * 15f;
                if (BombPathHitsGroundObstacle(origin, yaw, bomb, out bool groundObstacle) && groundObstacle)
                    return yaw;
            }

            Assert.Fail("The Arena map has no terrain collision at a 4005 ground phase.");
            return 0f;
        }

        bool BombPathHitsGroundObstacle(
            SimVec3 origin,
            float yaw,
            ProjectileDefinition bomb,
            out bool groundObstacle)
        {
            groundObstacle = false;
            var forward = LocomotionComp.ForwardFromYaw(yaw);
            var position = new SimVec3(
                origin.X + forward.X * bomb.SpawnForward,
                origin.Y,
                origin.Z + forward.Z * bomb.SpawnForward);
            float age = 0f;
            for (int i = 0; i < 100 && age < bomb.Lifetime; i++)
            {
                float nextAge = Math.Min(age + Step, bomb.Lifetime);
                bool touchdown = CrossesGroundPhase(bomb, age, nextAge);
                var desired = new SimVec3(
                    position.X + forward.X * bomb.Speed * Step,
                    origin.Y,
                    position.Z + forward.Z * bomb.Speed * Step);
                bool blocked;
                var resolved = _session.Map.Resolve(
                    position,
                    desired,
                    bomb.HitRadius,
                    bomb.Flying && !touchdown,
                    false,
                    out blocked);
                if (blocked)
                {
                    groundObstacle = touchdown;
                    return true;
                }
                position = resolved;
                age = nextAge;
            }
            return false;
        }

        static bool CrossesGroundPhase(ProjectileDefinition bomb, float previousAge, float nextAge)
        {
            if (bomb.GroundPhaseAt == null) return false;
            for (int i = 0; i < bomb.GroundPhaseAt.Length; i++)
                if (bomb.GroundPhaseAt[i] > previousAge && bomb.GroundPhaseAt[i] <= nextAge)
                    return true;
            return false;
        }

        sealed class AoeSpawnObservation
        {
            public int SpecId;
            public string ViewBlueprintId;
        }

        sealed class ResolveSample
        {
            public SimVec3 Target;
            public bool Flying;
        }

        sealed class TraceMovementConstraint : IMovementConstraint
        {
            public readonly List<ResolveSample> Samples = new List<ResolveSample>();

            public bool CanPlace(in SimVec3 position, float radius, bool flying, bool ignoreBorder = false)
                => true;

            public SimVec3 Resolve(
                in SimVec3 pivot,
                in SimVec3 target,
                float radius,
                bool flying,
                bool ignoreBorder,
                out bool obstructed)
            {
                Samples.Add(new ResolveSample { Target = target, Flying = flying });
                obstructed = false;
                return target;
            }

            public bool TryGetRandomPosition(IRandom random, float radius, bool flying, out SimVec3 position)
            {
                position = SimVec3.Zero;
                return true;
            }
        }
    }
}
