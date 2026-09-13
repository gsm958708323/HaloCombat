using System;
using Combat.Core;

namespace Combat.Demos
{
    public static class BuffArenaDemo
    {
        public static void Run()
        {
            try
            {
                RunInternal();
            }
            catch (Exception error)
            {
                // An escaped exception makes the process crash, which raises the
                // Windows JIT-debugger and application-error dialogs on every run.
                // Report the failure and return a clean exit instead.
                Console.WriteLine("BuffArenaDemo FAILED: " + error.Message);
                Console.WriteLine(error.StackTrace);
            }
        }

        static void RunInternal()
        {
            var data = BuffArenaContent.Build();
            var cells = new bool[8, 8];
            for (int x = 0; x < cells.GetLength(0); x++)
                for (int z = 0; z < cells.GetLength(1); z++)
                    cells[x, z] = true;
            cells[1, 0] = false;
            var navigation = new GridMovementConstraint(cells);
            var world = new CombatWorld(
                new BuffArenaActorFactory(data),
                new IntentQueue(),
                new EventBus(),
                new CombatTime(),
                new SeededRandom(7),
                data.Cues,
                data.Motor,
                navigation);
            world.ReplaceCatalogs(data.Projectiles, data.Aoes, new SummonCatalog());

            var playerId = world.SpawnActor(new ActorSpawnSpec("buff_player"));
            var enemyId = world.SpawnActor(new ActorSpawnSpec("buff_enemy"));
            world.TryGetActor(playerId, out var player);
            world.TryGetActor(enemyId, out var enemy);
            player.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 0f);
            enemy.GetComp<TransformComp>().Position = new SimVec3(0f, 0f, 2.5f);
            // The enemy sits on +Z, the player faces it; both headings are expressed as
            // directions so a future convention change cannot silently flip them.
            enemy.GetComp<TransformComp>().YawDegrees = YawOf(new SimVec3(-1f, 0f, 0f));
            player.GetComp<TransformComp>().YawDegrees = YawOf(new SimVec3(0f, 0f, 1f));
            enemy.GetComp<AttributeSet>().SetBase(AttrId.MoveSpeed, 0f);
            player.GetComp<AttributeSet>().SetBase(AttrId.MaxHp, 500f);
            player.GetComp<AttributeSet>().SetBase(AttrId.Hp, 500f);
            player.GetComp<AttributeSet>().SetBase(AttrId.Atk, 60f);
            player.GetComp<AmmoComp>().Set(60, 60);
            player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire1);

            for (int i = 0; i < 160; i++) world.Tick(.02f);
            if (player.GetComp<AmmoComp>().Current != 59)
                throw new Exception("Buff Arena fire did not consume one round.");
            if (enemy.GetComp<AttributeSet>().GetBase(AttrId.Hp) >= 50f)
                throw new Exception("Buff Arena projectile did not damage the enemy.");

            player.GetComp<AmmoComp>().Set(0, 60);
            player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire1);
            for (int i = 0; i < 70; i++) world.Tick(.02f);
            if (player.GetComp<AmmoComp>().Current != 60)
                throw new Exception("Buff Arena reload did not refill ammo.");

            var blocked = navigation.Resolve(
                new SimVec3(0f, 0f, 0f),
                new SimVec3(2f, 0f, 0f),
                .25f, false, false, out var obstructed);
            if (!obstructed || blocked.X >= 1f)
                throw new Exception("Buff Arena grid navigation did not stop at water.");

            world.Shutdown();
            VerifySkillsAndRuntimeCleanup();
            Console.WriteLine("BuffArenaDemo PASSED");
        }

        static void VerifySkillsAndRuntimeCleanup()
        {
            VerifyProjectileSkill(BuffArenaIds.Fire1, 3f, 1f, 89f, 120, true);
            VerifyProjectileSkill(BuffArenaIds.Fire2, 8f, 8f, YawOf(new SimVec3(1f, 0f, 0f)), 500, false);
            VerifyProjectileSkill(BuffArenaIds.Fire3, 8f, 8f, YawOf(new SimVec3(1f, 0f, 0f)), 140, false);
            VerifyHomingSkill();
            VerifyMonkeySkill();
            VerifyTeleportSkill();
            VerifyBarrelSkill();
            VerifyRollSkill();
            VerifyAimFacing();
            VerifyBoomerangOutbound();
            VerifyBoomerangReturn();
            VerifyBoomerangHitsTarget();
            VerifyBoomerangReArmsAfterDelay();
            VerifyBoomerangDoesNotKillSelf();
            VerifyProjectilePassesThroughInvulnerable();
            VerifyLethalDamageEntersDeadState();
            VerifyFloaterAnchorPolicy();
        }

        // Damage numbers must resolve the view's Head anchor first and only fall back to
        // a fixed offset above the logic position, otherwise they sit at the feet.
        static void VerifyFloaterAnchorPolicy()
        {
            var id = new EntityId(7, 1);
            SimVec3 body = new SimVec3(1f, 0f, 2f);
            SimVec3 head = new SimVec3(1f, 1.8f, 2.5f);
            int headCalls = 0;

            var anchored = FloaterAnchor.Resolve(body, (e, key) =>
            {
                headCalls++;
                return key == FloaterAnchor.HeadKey ? (SimVec3?)head : null;
            }, id);
            if (headCalls != 1)
                throw new Exception("Floater anchor did not query the Head key exactly once.");
            if (Math.Abs(anchored.X - head.X) > .001f || Math.Abs(anchored.Z - head.Z) > .001f ||
                Math.Abs(anchored.Y - (head.Y + FloaterAnchor.HeadOffsetY)) > .001f)
                throw new Exception("Floater did not use the Head anchor: (" +
                    anchored.X + "," + anchored.Y + "," + anchored.Z + ").");

            var fallback = FloaterAnchor.Resolve(body, (e, key) => null, id);
            if (Math.Abs(fallback.X - body.X) > .001f || Math.Abs(fallback.Z - body.Z) > .001f ||
                Math.Abs(fallback.Y - (body.Y + FloaterAnchor.BodyOffsetY)) > .001f)
                throw new Exception("Floater fallback offset is wrong: (" +
                    fallback.X + "," + fallback.Y + "," + fallback.Z + ").");

            var noResolver = FloaterAnchor.Resolve(body, null, id);
            if (Math.Abs(noResolver.Y - (body.Y + FloaterAnchor.BodyOffsetY)) > .001f)
                throw new Exception("Floater without a resolver must use the body offset.");
        }

        // Lethal damage has to raise the Dead tag, which is what keeps the corpse on
        // screen for the session's wall-time cleanup instead of hiding it instantly.
        static void VerifyLethalDamageEntersDeadState()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(9f, 0f, 9f), 0f,
                out var player, out _);
            try
            {
                var tags = player.GetComp<TagComp>();
                if (tags.Has(CommonTags.Dead))
                    throw new Exception("Fresh player already carries the Dead tag.");
                world.Deliver(new IEffect[]
                {
                    new DamageEffect { Flat = 99999f, CanCrit = false, DirectDamage = true, FireOnHurted = false }
                }, null, player, 0f);
                Tick(world, 2);
                if (!tags.Has(CommonTags.Dead))
                    throw new Exception("Lethal damage did not raise the Dead tag.");
            }
            finally
            {
                world.Shutdown();
            }
        }


        // Source BulletState.CanHit returns false while immuneTime > 0, so a bullet must
        // fly through an invulnerable body without being consumed.
        static void VerifyProjectilePassesThroughInvulnerable()
        {
            var world = CreateTestWorld(new SimVec3(0f, 0f, 0f), new SimVec3(8f, 0f, 0f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out var enemy);
            try
            {
                player.GetComp<AttributeSet>().SetBase(AttrId.MaxHp, 500f);
                player.GetComp<AttributeSet>().SetBase(AttrId.Hp, 500f);
                player.GetComp<HealthComp>().BeginIFrame(1f);
                if (!player.GetComp<HealthComp>().IsInvulnerable)
                    throw new Exception("Invulnerable flag did not engage.");

                enemy.GetComp<AttributeSet>().SetBase(AttrId.Atk, 40f);
                if (enemy.TryGetComp<BehaviorTreeComp>(out var bt)) bt.Board.Target = player.Id;
                Tick(world, 200);

                if (player.GetComp<AttributeSet>().GetBase(AttrId.Hp) < 499.9f)
                    throw new Exception("Invulnerable player lost HP: " +
                        player.GetComp<AttributeSet>().GetBase(AttrId.Hp) + ".");
            }
            finally
            {
                world.Shutdown();
            }
        }

        // Source CloakBoomerangTween outbound leg: speed * (sin(t / backTime * PI) + 0.1).
        static void VerifyBoomerangOutbound()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(40f, 0f, 40f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out _);
            try
            {
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire2);
                var projectile = TickUntilProjectile(world, 40);
                if (projectile == null)
                    throw new Exception("Boomerang outbound test never produced a projectile.");

                var body = projectile.GetComp<ProjectileComp>();
                float speed = body.Def.Speed;
                float backTime = body.Def.MotionParam;

                // The published velocity belongs to the age snapshotted at the start of
                // the tick, so the strict checks live where the sine curve is monotonic
                // and a one-tick sampling lag cannot cross the peak.
                float magnitude = LocomotionComp.StickMag(body.CurrentVelocity) / speed;
                if (Math.Abs(magnitude - .1f) > .02f)
                    throw new Exception("Boomerang launch magnitude " + magnitude + ", expected about 0.1.");

                // sin(t / backTime * PI) peaks at half of backTime, which is where the
                // source boomerang is fastest before it turns around.
                float peakAge = backTime * .5f;
                while (body.Age < peakAge - .03f)
                {
                    Tick(world, 1);
                    float current = LocomotionComp.StickMag(body.CurrentVelocity) / speed;
                    if (current < magnitude - .001f)
                        throw new Exception("Boomerang outbound speed is not rising: " +
                            magnitude + " -> " + current + ".");
                    magnitude = current;
                }

                while (body.Age < peakAge + .03f) Tick(world, 1);
                float sampledAge = body.Age;
                float peak = LocomotionComp.StickMag(body.CurrentVelocity) / speed;
                float expectedPeak = (float)Math.Sin(peakAge / backTime * Math.PI) + .1f;
                // The sampling loop can overshoot the peak by at most one logic tick.
                if (Math.Abs(sampledAge - peakAge) > .06f)
                    throw new Exception("Boomerang peak sampled at age " + sampledAge + ", expected " + peakAge + ".");
                if (Math.Abs(peak - expectedPeak) > .02f)
                    throw new Exception("Boomerang outbound peak magnitude " + peak + ", expected " + expectedPeak + ".");

                // Past backTime the body must decelerate as it turns for home.
                Tick(world, 3);
                float afterTurn = LocomotionComp.StickMag(body.CurrentVelocity) / speed;
                if (afterTurn > peak)
                    throw new Exception("Boomerang kept accelerating past backTime: " + afterTurn + ".");
            }
            finally
            {
                world.Shutdown();
            }
        }

        // Once backTime elapsed the boomerang with no target has to come home.
        static void VerifyBoomerangReturn()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(40f, 0f, 40f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out _);
            try
            {
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire2);
                var projectile = TickUntilProjectile(world, 40);
                if (projectile == null)
                    throw new Exception("Boomerang return test never produced a projectile.");

                Actor owner = player;
                var pTf = projectile.GetComp<TransformComp>();
                var oTf = owner.GetComp<TransformComp>();
                Tick(world, 50);
                float far = Distance2D(oTf.Position, pTf.Position);
                if (far < 1f)
                    throw new Exception("Boomerang did not travel away from the owner: " + far + ".");

                float previous = far;
                for (int i = 0; i < 100; i++)
                {
                    Tick(world, 1);
                    if (CountActive<ProjectileComp>(world) == 0) break;
                    float current = Distance2D(oTf.Position, pTf.Position);
                    if (current > previous + .05f)
                        throw new Exception("Boomerang return leg diverged: " + previous + " -> " + current + ".");
                    previous = current;
                }

                if (CountActive<ProjectileComp>(world) != 0)
                    throw new Exception("Boomerang never returned to the owner: " + previous + ".");
                // Removal uses the owner-contact check (HitRadius + owner radius). The last
                // sample is one tick early, so allow a conservative margin on top of that.
                float reach = 1f;
                if (previous > reach)
                    throw new Exception("Boomerang was removed outside its owner hit radius: " + previous + ".");
            }
            finally
            {
                world.Shutdown();
            }
        }

        // The boomerang reaches roughly 4.5m at its turning point, so the enemy has to
        // sit on the outbound path, not at the map edge.
        static void VerifyBoomerangHitsTarget()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(4.5f, 0f, 1f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out var enemy);
            try
            {
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire2);
                if (TickUntilProjectile(world, 40) == null)
                    throw new Exception("Boomerang hit test never produced a projectile.");

                var attr = enemy.GetComp<AttributeSet>();
                Tick(world, 120);
                if (attr.GetBase(AttrId.Hp) >= 50f)
                    throw new Exception("Boomerang never damaged the enemy on the outbound leg.");
            }
            finally
            {
                world.Shutdown();
            }
        }

        // Source MaxHits is effectively unlimited and sameTargetDelay (0.5s) is the only
        // re-arm gate, so the projectile must survive contact and be able to strike again.
        static void VerifyBoomerangReArmsAfterDelay()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(4.5f, 0f, 1f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out var enemy);
            try
            {
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire2);
                if (TickUntilProjectile(world, 40) == null)
                    throw new Exception("Boomerang re-arm test never produced a projectile.");

                var attr = enemy.GetComp<AttributeSet>();
                float previous = attr.GetBase(AttrId.Hp);
                bool sawHit = false;
                bool sawReHit = false;
                for (int i = 0; i < 320; i++)
                {
                    Tick(world, 1);
                    float hp = attr.GetBase(AttrId.Hp);
                    if (hp < previous - .01f)
                    {
                        if (!sawHit) sawHit = true;
                        else sawReHit = true;
                    }
                    previous = hp;
                    if (sawReHit) break;
                }

                if (!sawHit)
                    throw new Exception("Boomerang never damaged the enemy.");
                if (!sawReHit)
                    throw new Exception("Boomerang did not re-arm after sameTargetDelay.");
            }
            finally
            {
                world.Shutdown();
            }
        }

        // Source CloakBoomerangHit only damages a different side, so the catch must be harmless.
        static void VerifyBoomerangDoesNotKillSelf()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(40f, 0f, 40f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out _);
            try
            {
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire2);
                if (TickUntilProjectile(world, 40) == null)
                    throw new Exception("Boomerang self-damage test never produced a projectile.");
                Tick(world, 160);
                float hp = player.GetComp<AttributeSet>().GetBase(AttrId.Hp);
                if (hp < 499.9f)
                    throw new Exception("Boomerang damaged its own caster: " + hp + ".");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static Actor TickUntilProjectile(CombatWorld world, int maxTicks)
        {
            for (int i = 0; i < maxTicks; i++)
            {
                world.Tick(.02f);
                var body = FindActive<ProjectileComp>(world);
                if (body != null) return FindActor(world, body);
            }
            return null;
        }

        static float Distance2D(SimVec3 a, SimVec3 b)
        {
            float dx = a.X - b.X;
            float dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        // Locks the three aiming root causes: the cast snapshot must not fall back
        // to the move stick, an Attack must stay steerable by the mouse, and the
        // projectile must leave along the aim direction rather than the body yaw.
        static void VerifyAimFacing()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(20f, 0f, 20f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out _);
            try
            {
                // Aim at the 8 o'clock cursor position, on purpose far from the
                // forward axis and from any move intent, with no move input fed to
                // the locomotion.
                float aimYaw = YawOf(new SimVec3(-1f, 0f, -1f));
                float expectedYaw = aimYaw;

                player.GetComp<LocomotionComp>().RequestAimYaw(aimYaw);
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire1);

                Actor projectile = null;
                int ticks = 0;
                for (; ticks < 40; ticks++)
                {
                    player.GetComp<LocomotionComp>().RequestAimYaw(aimYaw);
                    float beforeY = player.GetComp<TransformComp>().Position.Y;
                    world.Tick(.02f);
                    if (Math.Abs(player.GetComp<TransformComp>().Position.Y - beforeY) > 1e-4f)
                        throw new Exception("Buff Arena player left the ground during the cast.");
                    var candidate = FindActive<ProjectileComp>(world);
                    if (candidate != null)
                    {
                        projectile = FindActor(world, candidate);
                        break;
                    }
                }

                if (projectile == null)
                    throw new Exception("Aim test never produced a projectile.");
                if (ticks >= 12)
                    throw new Exception("Aim test projectile spawned too late: " + ticks + " ticks.");

                float projectileYaw = projectile.GetComp<TransformComp>().YawDegrees;
                if (Math.Abs(NormalizeAngle(projectileYaw - expectedYaw)) > 2f)
                    throw new Exception("Projectile flew at yaw " + projectileYaw + ", expected " +
                        expectedYaw + ".");

                var playerPosition = player.GetComp<TransformComp>().Position;
                var projectilePosition = projectile.GetComp<TransformComp>().Position;
                float dx = projectilePosition.X - playerPosition.X;
                float dz = projectilePosition.Z - playerPosition.Z;
                float length = (float)Math.Sqrt(dx * dx + dz * dz);
                if (length <= .01f)
                    throw new Exception("Projectile spawned on top of the player.");
                var expectedFwd = LocomotionComp.ForwardFromYaw(expectedYaw);
                float expectedX = expectedFwd.X;
                float expectedZ = expectedFwd.Z;
                float dot = (dx / length) * expectedX + (dz / length) * expectedZ;
                if (dot < .99f)
                    throw new Exception("Projectile direction diverged from the aim (" + dot + ").");

                Tick(world, 30);
                float finalYaw = player.GetComp<TransformComp>().YawDegrees;
                if (Math.Abs(NormalizeAngle(finalYaw - expectedYaw)) > 2f)
                    throw new Exception("Player settled at yaw " + finalYaw + ", expected " + expectedYaw + ".");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static float YawOf(SimVec3 direction) => LocomotionComp.YawFromStick(direction);

        static float NormalizeAngle(float degrees)
        {
            while (degrees > 180f) degrees -= 360f;
            while (degrees < -180f) degrees += 360f;
            return degrees;
        }

        static void VerifyProjectileSkill(InputToken input, float enemyX, float enemyZ, float yaw,
            int ticks, bool requiresHit)
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(enemyX, 0f, enemyZ), yaw,
                out var player, out var enemy);
            try
            {
                float enemyHp = enemy.GetComp<AttributeSet>().GetBase(AttrId.Hp);
                player.GetComp<InputBufferComp>().Push(input);
                Tick(world, ticks);
                if (requiresHit && enemy.GetComp<AttributeSet>().GetBase(AttrId.Hp) >= enemyHp)
                    throw new Exception(input + " did not hit the target.");
                if (CountActive<ProjectileComp>(world) != 0)
                    throw new Exception(input + " projectile was not cleaned up.");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static void VerifyHomingSkill()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(4f, 0f, 3f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out var enemy);
            try
            {
                float enemyHp = enemy.GetComp<AttributeSet>().GetBase(AttrId.Hp);
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.HomingInput);
                Tick(world, 220);
                if (enemy.GetComp<AttributeSet>().GetBase(AttrId.Hp) >= enemyHp)
                    throw new Exception("Homing did not hit the target.");
                if (CountActive<ProjectileComp>(world) != 0)
                    throw new Exception("Homing projectile was not cleaned up.");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static void VerifyMonkeySkill()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(1.9f, 0f, 1f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out var enemy);
            try
            {
                float enemyHp = enemy.GetComp<AttributeSet>().GetBase(AttrId.Hp);
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.MonkeyInput);
                Tick(world, 30);
                if (CountActive<AoeComp>(world) == 0)
                    throw new Exception("Monkey skill did not create an AoE.");
                if (enemy.GetComp<AttributeSet>().GetBase(AttrId.Hp) >= enemyHp)
                    throw new Exception("Monkey skill did not hit the target.");

                // Source SpaceMonkeyBallHit: radius = 0.25f * (1 + absorbed * 0.05f).
                // Only *friendly* bullets are absorbed, so fire the pistol into the ball.
                var ball = FindActive<AoeComp>(world);
                int absorbedBefore = ball.AbsorbedProjectileCount;
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire1);
                Tick(world, 30);
                if (ball.AbsorbedProjectileCount <= absorbedBefore)
                    throw new Exception("Monkey ball did not absorb a friendly projectile.");
                float expectedRadius = .25f * (1f + ball.AbsorbedProjectileCount * .05f);
                if (Math.Abs(ball.Radius - expectedRadius) > 1e-3f)
                    throw new Exception("Monkey ball collision radius " + ball.Radius +
                        ", expected " + expectedRadius + ".");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static void VerifyTeleportSkill()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(8f, 0f, 8f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out _);
            try
            {
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire4);
                Tick(world, 40);
                var tracker = player.GetComp<ProjectileTrackerComp>();
                if (!tracker.HasProjectile || !world.TryGetActor(tracker.TrackedProjectile, out var projectile) ||
                    !projectile.TryGetComp<TransformComp>(out var projectileTf))
                    throw new Exception("Teleport skill did not create a tracked projectile.");
                var destination = projectileTf.Position;

                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire4);
                Tick(world, 2);
                var playerPosition = player.GetComp<TransformComp>().Position;
                if (tracker.HasProjectile || CountActive<ProjectileComp>(world) != 0 ||
                    Math.Abs(playerPosition.X - destination.X) > .001f ||
                    Math.Abs(playerPosition.Z - destination.Z) > .001f)
                    throw new Exception("Teleport skill did not consume the projectile and move the player.");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static void VerifyBarrelSkill()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(8f, 0f, 8f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out _);
            try
            {
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.Fire5);
                Tick(world, 35);
                var barrel = FindActive<BarrelComp>(world);
                if (barrel == null)
                    throw new Exception("Barrel skill did not create a barrel.");
                var barrelActor = FindActor(world, barrel);
                for (int i = 0; i < 5; i++)
                    world.Deliver(new IEffect[]
                    {
                        new DamageEffect { Flat = 9999f, CanCrit = false, DirectDamage = true }
                    }, player, barrelActor, 0f);
                Tick(world, 2);
                if (CountActive<BarrelComp>(world) != 0)
                    throw new Exception("Barrel skill did not clean up the exploded barrel.");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static void VerifyRollSkill()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(8f, 0f, 8f), YawOf(new SimVec3(1f, 0f, 0f)),
                out var player, out _);
            try
            {
                float start = player.GetComp<TransformComp>().Position.X;
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.RollInput);
                Tick(world, 60);
                float travelled = player.GetComp<TransformComp>().Position.X - start;
                // Source skill_roll: CasterForceMove(2.0f, 0.5f) over the 0.2s-0.8s window.
                if (travelled < 1.5f || travelled > 2.1f)
                    throw new Exception("Roll travelled " + travelled + "m, expected about 2.0m.");
            }
            finally
            {
                world.Shutdown();
            }
        }

        static CombatWorld CreateTestWorld(SimVec3 playerPosition, SimVec3 enemyPosition, float playerYaw,
            out Actor player, out Actor enemy)
        {
            var data = BuffArenaContent.Build();
            var cells = new bool[12, 12];
            for (int x = 0; x < cells.GetLength(0); x++)
                for (int z = 0; z < cells.GetLength(1); z++)
                    cells[x, z] = true;
            var world = new CombatWorld(
                new BuffArenaActorFactory(data),
                new IntentQueue(),
                new EventBus(),
                new CombatTime(),
                new SeededRandom(11),
                data.Cues,
                data.Motor,
                new GridMovementConstraint(cells));
            world.ReplaceCatalogs(data.Projectiles, data.Aoes, new SummonCatalog());

            var playerId = world.SpawnActor(new ActorSpawnSpec("buff_player"));
            var enemyId = world.SpawnActor(new ActorSpawnSpec("buff_enemy"));
            if (!world.TryGetActor(playerId, out player) || !world.TryGetActor(enemyId, out enemy))
                throw new Exception("Unable to create Buff Arena skill test actors.");
            player.GetComp<TransformComp>().Position = playerPosition;
            player.GetComp<TransformComp>().YawDegrees = playerYaw;
            enemy.GetComp<TransformComp>().Position = enemyPosition;
            enemy.GetComp<AttributeSet>().SetBase(AttrId.MoveSpeed, 0f);
            player.GetComp<AttributeSet>().SetBase(AttrId.MaxHp, 500f);
            player.GetComp<AttributeSet>().SetBase(AttrId.Hp, 500f);
            player.GetComp<AttributeSet>().SetBase(AttrId.Atk, 60f);
            player.GetComp<AmmoComp>().Set(60, 60);
            return world;
        }

        static void Tick(CombatWorld world, int count)
        {
            for (int i = 0; i < count; i++)
                world.Tick(.02f);
        }

        static int CountActive<T>(CombatWorld world) where T : Comp
        {
            var actors = world.RegistryActive();
            int count = 0;
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].TryGetComp<T>(out _)) count++;
            return count;
        }

        static T FindActive<T>(CombatWorld world) where T : Comp
        {
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].TryGetComp<T>(out var comp)) return comp;
            return null;
        }

        static Actor FindActor<T>(CombatWorld world, T component) where T : Comp
        {
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].TryGetComp<T>(out var match) && ReferenceEquals(match, component))
                    return actors[i];
            throw new Exception("Unable to resolve test component owner.");
        }
    }
}
