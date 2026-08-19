using System;
using Combat.Core;
using UnityEngine;
// using Combat.Gameplay; // 假设工厂在 Gameplay 命名空间

namespace Combat.Demos
{
    public class StateMachineDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var comboSO = new ComboTableSO(); // 空表，MVP 演示状态流
            var tags = new TagComp();
            var input = new InputBufferComp(time);
            var library = new TimelineLibrary();
            var intents = new IntentQueue();
            var effects = new EffectFactory(intents);
            var factory = new FighterActorFactory(time, comboSO, library, effects);
            var world = new CombatWorld(factory, new IntentQueue(), new EventBus(), time);

            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            if (!world.TryGetActor(id, out var actor))
                throw new Exception("spawn failed");

            var sm = actor.GetComp<StateMachineComp>();

            print($"初始状态：{sm.Current}");

            // 模拟输入进入 Attack（连招解析在 ComboComp 里）
            input.Push(InputToken.Attack);
            sm.TryEnter(ActorStateId.Attack, new StateEnterArgs(ActorStateId.Root, "Input"));

            print($"输入后状态：{sm.Current}");

            // 模拟受击（自动清缓冲 + 进 Hit）
            input.Clear(); // 模拟
            sm.TryEnter(ActorStateId.Hit, new StateEnterArgs(ActorStateId.Attack, "Hit"));

            print($"受击后状态：{sm.Current}, 缓冲已清={actor.GetComp<InputBufferComp>().HasBuffered}");
        }
    }
}
