using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface IActorFactory
    {
        Actor Create(in ActorSpawnSpec spec);
        void Release(Actor actor);
    }
    public readonly struct ActorSpawnSpec
    {
        public readonly string BlueprintId;
        public ActorSpawnSpec(string blueprintId)
        {
            BlueprintId = blueprintId ?? string.Empty;
        }
    }

    public sealed class Actor
    {
        readonly Dictionary<Type, Comp> _comps = new Dictionary<Type, Comp>(16);
        readonly List<Comp> _order = new List<Comp>(16);
        readonly List<Comp> _ticks = new List<Comp>(8);
        public EntityId Id { get; private set; }
        public bool IsActive { get; private set; }
        public void SetId(EntityId id) => Id = id;
        public void SetActive(bool active) => IsActive = active;
        public void AddComp(Comp comp)
        {
            if (comp == null)
                throw new ArgumentNullException(nameof(comp));
            var type = comp.GetType();
            if (_comps.ContainsKey(type))
                throw new InvalidOperationException($"Duplicate comp: {type.Name}");
            _comps.Add(type, comp);
            _order.Add(comp);
        }
        public void AttachAll()
        {
            _ticks.Clear();
            for (int i = 0; i < _order.Count; i++)
            {
                var c = _order[i];
                c.Attach(this);
                if (c.WantsTick)
                    _ticks.Add(c);
            }
        }
        public void DetachAll()
        {
            for (int i = _order.Count - 1; i >= 0; i--)
                _order[i].Detach();
            _ticks.Clear();
        }
        public void TickAll(float dt)
        {
            if (!IsActive)
                return;
            for (int i = 0; i < _ticks.Count; i++)
                _ticks[i].Tick(dt);
        }
        public T GetComp<T>() where T : Comp
        {
            if (TryGetComp<T>(out var c))
                return c;
            throw new InvalidOperationException(
                $"Actor {Id} missing {typeof(T).Name}");
        }
        public bool TryGetComp<T>(out T comp) where T : Comp
        {
            // 精确类型
            if (_comps.TryGetValue(typeof(T), out var exact))
            {
                comp = (T)exact;
                return true;
            }
            // 允许基类键查询派生实例（少用；优先具体类型）
            for (int i = 0; i < _order.Count; i++)
            {
                if (_order[i] is T match)
                {
                    comp = match;
                    return true;
                }
            }
            comp = null;
            return false;
        }
        public void ResetForPool()
        {
            DetachAll();
            _comps.Clear();
            _order.Clear();
            _ticks.Clear();
            Id = EntityId.Invalid;
            IsActive = false;
        }
    }

    /// <summary>
    /// 默认工厂：按 blueprint 字符串创建空壳 Actor。
    /// 后续子系统会替换为「读蓝图挂 Comp」的商用工厂。
    /// </summary>
    public sealed class EmptyActorFactory : IActorFactory
    {
        public Actor Create(in ActorSpawnSpec spec)
        {
            var actor = new Actor();
            actor.SetActive(true);
            return actor;
        }
        public void Release(Actor actor)
        {
            actor?.ResetForPool();
        }
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
}
