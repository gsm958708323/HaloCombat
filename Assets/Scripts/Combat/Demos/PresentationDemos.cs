using System;
using Combat.Core;
using Combat.Presentation;

namespace Combat.Demos
{
    public static class ClockDemo
    {
        public static void Run()
        {
            var clock = new SimulationClock(0.02f, 4);
            int total = 0;
            for (int i = 0; i < 60; i++)
                total += clock.BeginFrame(1f / 60f);
            if (total < 48 || total > 50)
                throw new Exception("1s ~50 logic steps, got " + total);
            var spiral = new SimulationClock(0.02f, 4);
            if (spiral.BeginFrame(10f) != 4)
                throw new Exception("clamp");
            var report = CombatValidator.Validate(new CodeCombatContent().Bake());
            if (report.HasError) throw new Exception(report.ToString());
            Console.WriteLine("ClockDemo PASSED steps=" + total);
        }
    }

    public static class SpawnEventDemo
    {
        public static void Run()
        {
            var world = DemoWorld.Create(out var events, out _);
            int spawns = 0;
            string lastBlueprint = string.Empty;
            EntityId lastOwner = EntityId.Invalid;
            events.Subscribe<EvEntitySpawn>(e =>
            {
                spawns++;
                lastBlueprint = e.BlueprintId;
                lastOwner = e.Owner;
            });

            var playerId = world.SpawnActor(new ActorSpawnSpec("fighter"));
            if (!world.TryGetActor(playerId, out var player) || player == null)
                throw new Exception("fighter spawn");
            if (spawns < 1 || lastBlueprint != "fighter" || lastOwner.IsValid)
                throw new Exception("fighter spawn");

            world.Deliver(
                new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball) },
                player, null, player.GetComp<AttributeSet>().GetFinal(AttrId.Atk));
            world.Tick(0.02f);
            if (lastBlueprint != "projectile") throw new Exception("proj bp " + lastBlueprint);
            if (lastOwner != player.Id) throw new Exception("proj owner");
            Console.WriteLine("SpawnEventDemo PASSED count=" + spawns);
        }
    }
}
