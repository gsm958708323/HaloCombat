using System;
using Combat.Core;
using UnityEngine;

namespace Combat.BuffDemo
{
    public class BuffDemo : MonoBehaviour
    {
        public void Awake()
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
    }
}
