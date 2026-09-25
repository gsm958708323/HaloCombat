using Combat.Core;

namespace Combat.Demos
{
    public static class DemoWorld
    {
        public static CombatWorld Create(
            out EventBus events,
            out CombatTime time,
            IRandom random = null,
            EventBus eventsOverride = null,
            CombatTime timeOverride = null)
        {
            var baked = new CodeCombatContent().Bake();
            events = eventsOverride ?? new EventBus();
            time = timeOverride ?? new CombatTime();
            // 一次装配：目录与 Cue 来自烘焙内容，其余用演示自己的实例。
            var install = baked.ToInstall();
            install.Intents = new IntentQueue();
            install.Events = events;
            install.Time = time;
            install.Random = random ?? new FixedRandom(0f);
            install.Motor = baked.Motor;
            return new CombatWorld(new FighterActorFactory(baked), install);
        }
    }
}
