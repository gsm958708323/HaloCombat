using System;
using Combat.Core;

namespace Combat.Demos
{
    internal static class SeasonTwoDemoSupport
    {
        public static CombatWorld NewWorld(EventBus events = null, CombatTime time = null, IRandom random = null)
        {
            var world = new CombatWorld(
                new FighterActorFactory(DemoTables.G1G2(), DemoTables.MakeLib()),
                new IntentQueue(),
                events ?? new EventBus(),
                time ?? new CombatTime(),
                random ?? new FixedRandom(0f));
            CombatCatalog.RegisterDefaults(world.Projectiles, world.Aoes, CombatCatalog.Burn(), world.Summons);
            return world;
        }

        public static Actor Spawn(CombatWorld world, string blueprint, float x, float z)
        {
            var id = world.SpawnActor(new ActorSpawnSpec(blueprint));
            if (!world.TryGetActor(id, out var actor) || actor == null)
                throw new Exception("spawn " + blueprint);
            actor.GetComp<TransformComp>().Position = new SimVec3(x, 0f, z);
            return actor;
        }

        public static void Step(CombatWorld world, float dt)
        {
            var actors = world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
                if (actors[i].TryGetComp<LocomotionComp>(out var loco))
                    loco.RequestMoveIntent(0f, 0f);
            world.Tick(dt);
        }

        public static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
