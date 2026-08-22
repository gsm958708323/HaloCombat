using System;
using Combat.Core;
using UnityEngine;

namespace Combat.TimelineEffectDemo
{
    public class TimelineEffectDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            var factory = new CombatActorFactory(time, ComboTableSO.Create(), TimelineLibrary.Create(), new EffectFactory(intents), intents, new ProjectileSpecLibrary());
            var world = new CombatWorld(factory, intents, events, time);

            // Service 阶段仅打印跨实体 Intent（投射物真正生成留给下一步）
            world.AddServicePhase(() =>
            {
                intents.Drain<AnimSignalIntent>(i =>
                    print($"[F{time.Frame}] AnimSignal {i.Signal} from {i.Source}"));
                intents.Drain<SpawnProjectileIntent>(i =>
                    print($"[F{time.Frame}] SpawnProjectile spec={i.SpecValue} owner={i.Owner}"));
            });

            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            world.TryGetActor(id, out var actor);
            var input = actor.GetComp<InputBufferComp>();
            var tags = actor.GetComp<TagComp>();
            var director = actor.GetComp<SkillDirectorComp>();
            var fsm = actor.GetComp<StateMachineComp>();

            // 开招
            input.Push(InputToken.Attack);
            world.Tick(0.05f); // Driver 匹配 G1 并 Play
            print($"After open: state={fsm.Current}, skill={director.CurrentSkill}, cancel={tags.Has(CommonTags.Cancel)}");

            // 推到取消窗内
            world.Tick(0.05f); // 累计 ~0.10，Cancel 应已加上（键在 0.05）
            print($"At ~0.10: cancel={tags.Has(CommonTags.Cancel)}");

            // 取消接 G2
            input.Push(InputToken.Attack);
            world.Tick(0.05f);
            print($"After cancel G2: skill={director.CurrentSkill}");

            // 继续推，直到 G1 旧轴已换；再推到生成物键（在 G1 上）。本 demo 换轴后走 G2，不再生成 901。
            // 再开一轮专测投射物：
            // 重置体验：直接 Play G1 并推进到 0.25
            director.Play(SkillNodeId.G1, TimelineId.TL_G1);
            for (int i = 0; i < 6; i++)
                world.Tick(0.05f); // 0.30s

            print($"Done. skill={director.CurrentSkill}, cancel={tags.Has(CommonTags.Cancel)}");

            // 受击停轴
            input.Push(InputToken.Attack);
            fsm.TryEnter(ActorStateId.Hit, new StateEnterArgs(ActorStateId.Attack, "Hit"));
            print($"On Hit: skill={director.CurrentSkill}, buffered={input.HasBuffered}, state={fsm.Current}");
        }
    }
}
