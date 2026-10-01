using System;
using System.Collections.Generic;
using Combat.Core;
using NUnit.Framework;

namespace Combat.Tests
{
    /// <summary>
    /// 每个蓝图都必须能装配成功。工厂少加一个 comp 时，OnAttach 里的 GetComp 会立刻抛，
    /// 消息里带 blueprint id——这条用例把「工厂漏装」钉在测试里，而不是等对局跑起来才发现。
    ///
    /// 扫描的是工厂按名字分支处理的那批蓝图（见 FighterActorFactory.Create）；
    /// CodeCombatContent 并不填 CharacterCatalog，所以这里用字面量而不是目录。
    /// Arena 的 buff_player / buff_enemy / buff_barrel 由 PlayMode 冒烟覆盖（SetUp 清场 + Fire5 出桶）。
    /// </summary>
    public sealed class ActorCompositionTests
    {
        static readonly string[] Blueprints =
        {
            "fighter",           // 季节 1/2 玩家
            "stake",             // 木桩：最小组件集，验证最简组合也能装
            "melee_ai",          // 近战 AI
            "melee_ai_narrow",   // 窄域近战 AI
            "melee_guard",       // 守卫
            "ranged_ai",         // 远程 AI
            "summon",            // 召唤物
            "projectile",        // 弹体运行时体：只有 Transform/Tag/Team/ProjectileComp
            "aoe"                // AoE 运行时体：只有 Transform/Tag/Team/AoeComp
        };

        [Test]
        public void EveryFactoryBlueprintSpawns()
        {
            var baked = new CodeCombatContent().Bake();
            var install = baked.ToInstall();
            install.Intents = new IntentQueue();
            install.Events = new EventBus();
            install.Time = new CombatTime();
            install.Random = new FixedRandom(0f);
            install.Motor = baked.Motor;
            var world = new CombatWorld(new FighterActorFactory(baked), install);

            var failures = new List<string>();
            for (int i = 0; i < Blueprints.Length; i++)
            {
                string blueprintId = Blueprints[i];
                try
                {
                    world.SpawnActor(new ActorSpawnSpec(blueprintId));
                }
                catch (Exception error)
                {
                    failures.Add(blueprintId + " -> " + error.Message);
                }
            }

            Assert.IsEmpty(
                failures,
                "These blueprints failed to attach their comps. Fix the factory, or drop the dependency "
                    + "that is not actually required: " + string.Join("; ", failures));
        }
    }
}
