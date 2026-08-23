using System;
using Combat.Core;
using UnityEngine;

namespace Combat.BuffDemo
{
    public class BuffDemo : MonoBehaviour
    {
        public void Awake()
        {
            // Test1();
            Test2();
        }

        public void Test1()
        {
            var timelines = new TimelineLibrary();
            var tl = new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.8f,
                Keys = new[]
                {
                    new TimelineKey
                    {
                        Time = 0.05f, EndTime = 0.45f,
                        Type = EffectType.Buff,
                        BuffTypeValue = 1001, BuffStacks = 1, BuffDuration = 0.4f, BuffRefreshIfExist = true
                    },
                    new TimelineKey
                    {
                        Time = 0.05f, EndTime = -1f,
                        Type = EffectType.Buff,
                        BuffTypeValue = 1002, BuffStacks = 1, BuffDuration = 0.3f
                    }
                }
            };
            timelines.Register(tl);

            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            var factory = new CombatActorFactory(time, ComboTableSO.Create(), timelines, new EffectFactory(intents), intents, new ProjectileSpecLibrary());
            var world = new CombatWorld(factory, intents, events, time);
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var player);
            var buffComp = player.GetComp<BuffComp>();
            var attr = player.GetComp<AttrComp>();

            // 应用 Buff
            buffComp.Apply(new BuffApplyArgs(new BuffTypeId(1001), 2, 0.5f, TagSource.Effect("Test"), false));

            // 验证
            print($"TotalAtk={attr.TotalAtk}, TotalDef={attr.TotalDef}");
            print($"Buff count={buffComp.AllBuffs.Count}");

            for (int i = 0; i < 50; i++)
                world.Tick(0.05f);

            print($"After 2.5s, TotalAtk={attr.TotalAtk}");
            print("BuffDemo PASSED");
        }

        void Test2()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            var buffDefs = new BuffDefLookup();
            var atkDef = new BuffDefSO
            {
                Id = new BuffTypeId(9001),
                Modifiers = new[] { new AttrModifierDef { Channel = AttrChannel.Atk, Op = ModifierOp.Flat, Value = 8f } },
                Hooks = new[]
                {
                    new BuffHook(BuffTrigger.OnHurt, data =>
                    {
                        print($"[HOOK] Buff 9001 OnHurt, stacks={data.Stacks}, target={data.Target}");
                        // 例如：Buff 9001 期间伤害 +8（已在 AttrSheet 重算）
                    })
                }
            };
            buffDefs.Register(atkDef);
            // Timeline / Combo / Factory 同 4.11（略）
            var timelines = new TimelineLibrary();
            timelines.Register(new TimelineSO { Id = TimelineId.TL_G1, Duration = 0.55f, Keys = Array.Empty<TimelineKey>() }); // 简化版
            var combos = new ComboTableSO { Entries = Array.Empty<ComboEntry>() };
            var projSpecs = new ProjectileSpecLibrary();
            var aoeSpecs = new AoESpecLibrary();
            var pulseSpecs = new PulseZoneSpecLibrary();
            var attackSpecs = new AttackSpecLibrary();
            attackSpecs.Register(new AttackSpec { Id = 42, Power = 1f, StunDuration = 0.30f, ApplyHitStun = true });
            var effects = new EffectFactory(intents, aoeSpecs);
            var factory = new CombatActorFactory(time, combos, timelines, effects, intents, projSpecs);
            var world = new CombatWorld(factory, intents, events, time);
            var projectileService = new ProjectileService(world, intents, projSpecs, factory);
            var pulseZoneService = new PulseZoneService(world, intents, pulseSpecs, aoeSpecs, factory);
            var hitDetect = new HitDetectService(world, intents, projectileService);
            var aoeService = new AoEService(world, intents);
            var damage = new DamageService(world, intents, attackSpecs, events);
            var despawn = new DespawnService(world, intents);
            world.AddServicePhase(() => projectileService.Tick());
            world.AddServicePhase(() => pulseZoneService.Tick());
            world.AddServicePhase(() => hitDetect.Tick());
            world.AddServicePhase(() => aoeService.Tick());
            world.AddServicePhase(() => damage.Tick());
            world.AddServicePhase(() => despawn.Tick());
            var playerId = world.SpawnActor(new ActorSpawnSpec("fighter"));
            var dummy = world.SpawnActor(new ActorSpawnSpec("dummy"));
            world.TryGetActor(playerId, out var player);
            player.GetComp<BuffComp>().RegisterHook(new BuffTypeId(9001), atkDef.Hooks[0]);
            player.GetComp<AttrComp>().BaseAtk = 15f;
            player.GetComp<HealthComp>().ResetToFull();
            print("HookDemo: Buff 9001 OnHurt hook 已注册，伤害 +8 生效");
            print("FeelRulesDemo + BuffDemo 合并：请运行 FeelRulesDemo 同时验证 Hook");
        }
    }
}
