using System;
using Combat.Core;
using UnityEngine;

namespace Combat.ComboDirectorDemo
{
    public class ComboDirectorDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            var factory = new CombatActorFactory(time, ComboTableSO.Create(), TimelineLibrary.Create(), new EffectFactory(intents), intents, new ProjectileSpecLibrary());
            var world = new CombatWorld(factory, intents, events, time);

            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            if (!world.TryGetActor(id, out var actor))
                throw new Exception("spawn failed");

            var sm = actor.GetComp<StateMachineComp>();
            var combo = actor.GetComp<ComboComp>();
            var director = actor.GetComp<SkillDirectorComp>();
            var input = actor.GetComp<InputBufferComp>();

            // 先进 Attack
            input.Push(InputToken.Attack);
            sm.TryEnter(ActorStateId.Attack, new StateEnterArgs(ActorStateId.Root, "Input"));

            // 匹配连招（成功则 Consume）
            if (combo.TryResolve(out var result))
                print($"连招匹配成功！换轴到 {result.ToSkill}");

            // 播轴
            director.Play(result.ToSkill, result.Timeline);

            // 模拟受击停轴
            sm.TryEnter(ActorStateId.Hit, new StateEnterArgs(ActorStateId.Attack, "Hit"));
            print($"受击后状态={sm.Current}，缓冲已清={actor.GetComp<InputBufferComp>().HasBuffered}");
        }
    }
}
