using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface IComp
    {
        void OnAttach(IActor self);
        void OnDetach();
    }

    public interface ITickComp : IComp
    {
        void Tick(float dt);
    }

    public interface IActor
    {
        EntityId Id { get; }
        bool IsActive { get; }
        T GetComp<T>() where T : class, IComp;
        bool TryGetComp<T>(out T comp) where T : class, IComp;
        void SetId(EntityId id);           // 仅 Registry 在 Spawn 时调用
        void SetActive(bool active);
        IReadOnlyList<IComp> AllComps { get; }
    }

    public interface IActorFactory
    {
        IActor Create(in ActorSpawnSpec spec);
        void Release(IActor actor);
    }

    public readonly struct ActorSpawnSpec
    {
        public readonly string BlueprintId;

        public ActorSpawnSpec(string blueprintId)
        {
            BlueprintId = blueprintId ?? string.Empty;
        }
    }

    /// <summary>
    /// 最小 Actor：只做挂载与查找。业务规则禁止写在这里。
    /// </summary>
    public sealed class Actor : IActor
    {
        readonly Dictionary<Type, IComp> _comps = new Dictionary<Type, IComp>(16);
        readonly List<IComp> _order = new List<IComp>(16);
        readonly List<ITickComp> _ticks = new List<ITickComp>(8);

        public EntityId Id { get; private set; }
        public bool IsActive { get; private set; }
        public IReadOnlyList<IComp> AllComps => _order;

        public void SetId(EntityId id) => Id = id;
        public void SetActive(bool active) => IsActive = active;

        public void AddComp(IComp comp)
        {
            if (comp == null)
                throw new ArgumentNullException(nameof(comp));

            var type = comp.GetType();
            // 同类型只允许一个实例（MVP）；接口查找见 GetComp
            if (_comps.ContainsKey(type))
                throw new InvalidOperationException($"Duplicate comp type: {type.Name}");

            _comps[type] = comp;
            _order.Add(comp);

            if (comp is ITickComp tick)
                _ticks.Add(tick);
        }

        public void AttachAll()
        {
            for (int i = 0; i < _order.Count; i++)
                _order[i].OnAttach(this);
        }

        public void DetachAll()
        {
            for (int i = _order.Count - 1; i >= 0; i--)
                _order[i].OnDetach();
        }

        public void TickAll(float dt)
        {
            if (!IsActive)
                return;

            for (int i = 0; i < _ticks.Count; i++)
                _ticks[i].Tick(dt);
        }

        public T GetComp<T>() where T : class, IComp
        {
            if (TryGetComp<T>(out var c))
                return c;
            throw new InvalidOperationException(
                $"Actor {Id} missing comp {typeof(T).Name}");
        }

        public bool TryGetComp<T>(out T comp) where T : class, IComp
        {
            // 1) 精确类型
            if (_comps.TryGetValue(typeof(T), out var exact) && exact is T typed)
            {
                comp = typed;
                return true;
            }

            // 2) 接口 / 基类：线性查（Comp 数量很小）
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
        public IActor Create(in ActorSpawnSpec spec)
        {
            var actor = new Actor();
            actor.SetActive(true);
            // blueprint 本步仅保留字段，不挂 Comp
            return actor;
        }

        public void Release(IActor actor)
        {
            if (actor is Actor a)
                a.ResetForPool();
            // MVP 不强制池化 Actor；后续接 Pool<Actor>
        }
    }
}
