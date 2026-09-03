using System;
using Combat.Core;

namespace Combat.Demos
{
    // 第二季 Demo 的公共测试工具：统一创建世界、生成 Actor、推进时间和抛出断言。
    internal static class SeasonTwoDemoSupport
    {
        public static CombatWorld NewWorld(EventBus events = null, CombatTime time = null, IRandom random = null)
        {
            // 默认使用固定随机数，保证暴击和其它随机分支在回归测试中稳定。
            var baked = new CodeCombatContent().Bake();
            var world = new CombatWorld(
                new FighterActorFactory(baked),
                new IntentQueue(),
                events ?? new EventBus(),
                time ?? new CombatTime(),
                random ?? new FixedRandom(0f),
                baked.Cues,
                baked.Motor);
            baked.Install(world);
            return world;
        }

        public static Actor Spawn(CombatWorld world, string blueprint, float x, float z)
        {
            // Spawn 后立即设置平面坐标，调用方只需关注测试场景布局。
            var id = world.SpawnActor(new ActorSpawnSpec(blueprint));
            if (!world.TryGetActor(id, out var actor) || actor == null)
                throw new Exception($"生成 Actor 失败：blueprint={blueprint}");
            actor.GetComp<TransformComp>().Position = new SimVec3(x, 0f, z);
            return actor;
        }

        public static void Step(CombatWorld world, float dt)
        {
            // 测试默认不主动移动 Actor，避免运动噪声影响时间和战斗断言。
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
