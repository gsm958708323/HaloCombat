using Combat.Core;

namespace Combat.Demos
{
    public static class DemoWorld
    {
        public static CombatWorld Create(
            out EventBus events,
            out CombatTime time,
            IRandom random = null)
        {
            var baked = new CodeCombatContent().Bake();
            events = new EventBus();
            time = new CombatTime();
            var world = new CombatWorld(
                new FighterActorFactory(baked),
                new IntentQueue(),
                events,
                time,
                random ?? new FixedRandom(0f),
                baked.Cues,
                baked.Motor);
            baked.Install(world);
            return world;
        }
    }
}
