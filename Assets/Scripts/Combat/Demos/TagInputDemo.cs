using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Demos
{
    // 测试用事件
    readonly struct TagChangedEvent
    {
        public readonly EntityId ActorId;
        public readonly TagId Tag;
        public readonly int Stack;
        public readonly TagSource Source;

        public TagChangedEvent(EntityId id, TagId tag, int stack, TagSource source)
        {
            ActorId = id;
            Tag = tag;
            Stack = stack;
            Source = source;
        }
    }

    readonly struct InputConsumedEvent
    {
        public readonly EntityId ActorId;
        public readonly InputToken Token;

        public InputConsumedEvent(EntityId id, InputToken token)
        {
            ActorId = id;
            Token = token;
        }
    }

    sealed class TestTagComp : Comp
    {
        readonly IEventBus _events;
        readonly ICombatTime _time;

        public TestTagComp(IEventBus events, ICombatTime time)
        {
            _events = events;
            _time = time;
        }

        // gsm todo 调用
        protected override void OnAttach()
        {
            _events.Publish(new TagChangedEvent(Self.Id, new TagId(1), 1, new TagSource("OnAttach" + _time.Time)));
        }

        protected override void OnDetach() { }

        public void Add(TagId tag, int stacks, TagSource source)
        {
            // 模拟状态进出
            _events.Publish(new TagChangedEvent(Self.Id, tag, stacks, source));
        }

        public void Remove(TagId tag, int stacks, TagSource source)
        {
            _events.Publish(new TagChangedEvent(Self.Id, tag, stacks, source));
        }
    }

    sealed class TestInputBufferComp : Comp
    {
        readonly ICombatTime _time;
        readonly IEventBus _events;

        public TestInputBufferComp(ICombatTime time, IEventBus events)
        {
            _time = time;
            _events = events;
        }

        public void Push(in InputToken token, float clientTime)
        {
            _events.Publish(new InputConsumedEvent(Self.Id, token)); // 消费
        }

        public void Clear() { }
        public bool TryPeek(out InputToken token) { token = default; return false; }
        public bool Consume() { return false; }
        public float LastInputTime => _time.Time;
    }

    public sealed class FighterActorFactory : IActorFactory
    {
        readonly CombatTime _time;
        public FighterActorFactory(CombatTime time)
        {
            _time = time;
        }
        public Actor Create(in ActorSpawnSpec spec)
        {
            var actor = new Actor();
            actor.SetActive(true);
            // 专属参数在构造；不在基类 OnAttach 形参里塞万能包
            actor.AddComp(new TagComp());
            actor.AddComp(new InputBufferComp(_time, bufferWindow: 0.2f));
            return actor;
        }
        public void Release(Actor actor)
        {
            actor?.ResetForPool();
        }
    }

    public class TagInputDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var factory = new FighterActorFactory(time);
            var world = new CombatWorld(factory, new IntentQueue(), new EventBus(), time);
            var id = world.SpawnActor(new ActorSpawnSpec("fighter"));
            if (!world.TryGetActor(id, out var actor))
                throw new Exception("spawn failed");
            var tags = actor.GetComp<TagComp>();
            var input = actor.GetComp<InputBufferComp>();
            // --- Tag ---
            tags.Add(CommonTags.Grounded, 1, TagSource.StateEnter("Root"));
            tags.Add(CommonTags.Cancel, 1, TagSource.Effect("CancelWindow"));
            print($"Has Cancel={tags.Has(CommonTags.Cancel)}, Stack={tags.Stack(CommonTags.Cancel)}");
            tags.Remove(CommonTags.Cancel, 1, TagSource.Effect("CancelWindowEnd"));
            print($"After remove Cancel={tags.Has(CommonTags.Cancel)}");
            // --- 预输入 ---
            input.Push(InputToken.Attack);
            print($"Peek right after push: {input.TryPeek(out var t1)} action={t1.Action}");
            // 0.1s 内仍有效
            world.Tick(0.1f);
            print($"Peek at 0.1: {input.TryPeek(out var t2)} action={t2.Action}");
            // 再过 0.15s，总龄 0.25 > 0.2 窗 → 过期
            world.Tick(0.15f);
            print($"Peek at 0.25: {input.TryPeek(out _)} (expect false)");
            // 再推一次，模拟受击 Clear
            input.Push(InputToken.UpAttack);
            print($"Before hit clear, has={input.HasBuffered}");
            input.Clear(); // 以后由 Hit 状态进入调用
            print($"After hit clear, has={input.HasBuffered}, LastPushTime={input.LastPushTime}");
            // Consume 路径
            input.Push(InputToken.Attack);
            if (input.TryPeek(out var peek) && peek == InputToken.Attack)
            {
                bool ok = input.Consume();
                print($"Consume Attack ok={ok}, has={input.HasBuffered}");
            }
        }
    }
}
