using System;
using Combat.Core;

namespace Combat.Demos
{
    public static class BuffArenaDemo
    {
        public static void Run()
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
            enemy.GetComp<TransformComp>().YawDegrees = 180f;
            player.GetComp<TransformComp>().YawDegrees = 90f;
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
            VerifyProjectileSkill(BuffArenaIds.Fire1, 3f, 1f, 1f, 120, true);
            VerifyProjectileSkill(BuffArenaIds.Fire2, 8f, 8f, 0f, 500, false);
            VerifyProjectileSkill(BuffArenaIds.Fire3, 8f, 8f, 0f, 140, false);
            VerifyHomingSkill();
            VerifyMonkeySkill();
            VerifyTeleportSkill();
            VerifyBarrelSkill();
            VerifyRollSkill();
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
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(4f, 0f, 3f), 0f,
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
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(1.9f, 0f, 1f), 0f,
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
            }
            finally
            {
                world.Shutdown();
            }
        }

        static void VerifyTeleportSkill()
        {
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(8f, 0f, 8f), 0f,
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
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(8f, 0f, 8f), 0f,
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
            var world = CreateTestWorld(new SimVec3(1f, 0f, 1f), new SimVec3(8f, 0f, 8f), 0f,
                out var player, out _);
            try
            {
                float start = player.GetComp<TransformComp>().Position.X;
                player.GetComp<InputBufferComp>().Push(BuffArenaIds.RollInput);
                Tick(world, 60);
                if (player.GetComp<TransformComp>().Position.X <= start + .2f)
                    throw new Exception("Roll skill did not move the player.");
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
