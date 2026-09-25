using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>
    /// 组件基类。生命周期由 Actor 驱动：AddComp → AttachAll(OnAttach) → 每帧 Tick（仅 WantsTick）→ DetachAll(OnDetach)。
    /// 组件可以在 OnAttach 里 GetComp 别的组件；缺件会抛异常，所以组装顺序由工厂负责。
    /// </summary>
    public abstract class Comp
    {
        protected Actor Self { get; private set; }

        /// <summary>是否参与阶段 1 的组件 Tick。位移与 Buff 由世界在各自阶段推动，所以它们是 false。</summary>
        public virtual bool WantsTick => false;

        internal void Attach(Actor actor)
        {
            Self = actor;
            OnAttach();
        }

        internal void Detach()
        {
            OnDetach();
            Self = null;
        }

        protected virtual void OnAttach() { }
        protected virtual void OnDetach() { }
        public virtual void OnDeath(Actor killer) { }
        public virtual void Tick(float dt) { }
    }

    public readonly struct ActorSpawnSpec
    {
        public readonly string BlueprintId;
        public ActorSpawnSpec(string blueprintId) => BlueprintId = blueprintId ?? string.Empty;
    }

    public interface IActorFactory
    {
        Actor Create(in ActorSpawnSpec spec);
        void Release(Actor actor);
    }

    /// <summary>
    /// 一个实体：组件容器 + 身份。组件按 AddComp 的顺序 Tick，_ticks 只装声明了 WantsTick 的那些。
    /// Id 由 EntityRegistry 分配（槽位 + 世代）；失活与回收也由注册表统一处理。
    /// </summary>
    public sealed class Actor
    {
        // 具体类型走字典快路径；接口 / 基类等非具体类型走 _order 的线性扫描（见 TryGetComp）。
        readonly Dictionary<Type, Comp> _comps = new Dictionary<Type, Comp>(16);
        readonly List<Comp> _order = new List<Comp>(16);
        readonly List<Comp> _ticks = new List<Comp>(8);

        public EntityId Id { get; private set; }
        public bool IsActive { get; private set; }
        public CombatWorld World { get; internal set; }

        /// <summary>生成时用的蓝图 id。只用于错误定位：缺组件时能看出是哪个蓝图装错了。</summary>
        public string BlueprintId { get; internal set; }

        public void SetId(EntityId id) => Id = id;
        public void SetActive(bool active) => IsActive = active;

        public void AddComp(Comp comp)
        {
            if (comp == null) throw new ArgumentNullException(nameof(comp));
            var type = comp.GetType();
            if (_comps.ContainsKey(type))
                throw new InvalidOperationException("Duplicate comp " + type.Name);
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
            if (!IsActive) return;
            for (int i = 0; i < _ticks.Count; i++)
                _ticks[i].Tick(dt);
        }

        public void NotifyDeath(Actor killer)
        {
            for (int i = 0; i < _order.Count; i++)
                _order[i].OnDeath(killer);
        }

        /// <summary>取组件，缺件即抛。约束放宽到 class，因此接口（如 IIncomingDamage）也能查。</summary>
        public T GetComp<T>() where T : class
        {
            if (TryGetComp<T>(out var c)) return c;
            throw new InvalidOperationException(
                "Actor " + Id + (string.IsNullOrEmpty(BlueprintId) ? "" : " (blueprint '" + BlueprintId + "')")
                + " missing " + typeof(T).Name);
        }

        /// <summary>按类型取组件：具体类型先查字典；接口 / 基类走线性扫描，返回第一个匹配的。</summary>
        public bool TryGetComp<T>(out T comp) where T : class
        {
            if (_comps.TryGetValue(typeof(T), out var exact) && exact is T typed)
            {
                comp = typed;
                return true;
            }

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
            World = null;
            BlueprintId = null;
        }
    }

    /// <summary>
    /// 实体表：槽位 + 世代。回收的槽位会被复用，世代 +1 让旧的 EntityId 立刻失效；
    /// 销毁是延迟的（RequestDespawn → 帧末 FlushDespawn），所以同帧后面的阶段仍能看见待销毁实体。
    /// </summary>
    public sealed class EntityRegistry
    {
        struct Slot
        {
            public Actor Actor;
            public int Generation;
            public bool Occupied;
        }

        readonly IActorFactory _factory;
        readonly CombatWorld _world;
        readonly List<Slot> _slots = new List<Slot>(64);
        readonly Queue<int> _free = new Queue<int>(32);
        readonly HashSet<EntityId> _pending = new HashSet<EntityId>();
        readonly List<EntityId> _despawnScratch = new List<EntityId>(16);

        public int ActiveCount { get; private set; }

        public EntityRegistry(IActorFactory factory, CombatWorld world)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _world = world;
            _slots.Add(new Slot());
        }

        public bool TryGet(EntityId id, out Actor actor)
        {
            actor = null;
            if (!id.IsValid || id.Index <= 0 || id.Index >= _slots.Count)
                return false;
            var slot = _slots[id.Index];
            if (!slot.Occupied || slot.Generation != id.Generation)
                return false;
            actor = slot.Actor;
            return actor != null && actor.IsActive;
        }

        /// <summary>
        /// 生成实体：复用空闲槽位（世代 +1），写入身份与蓝图 id，然后 AttachAll。
        /// 组件的 OnAttach 缺件会在这一步立刻抛出，并带上蓝图 id。
        /// </summary>
        public EntityId Spawn(in ActorSpawnSpec spec)
        {
            var actor = _factory.Create(spec);
            int index;
            int gen;
            if (_free.Count > 0)
            {
                index = _free.Dequeue();
                gen = _slots[index].Generation + 1;
                if (gen <= 0) gen = 1;
            }
            else
            {
                index = _slots.Count;
                gen = 1;
                _slots.Add(new Slot());
            }

            actor.SetId(new EntityId(index, gen));
            actor.SetActive(true);
            actor.World = _world;
            actor.BlueprintId = spec.BlueprintId;
            _slots[index] = new Slot { Actor = actor, Generation = gen, Occupied = true };
            actor.AttachAll();
            ActiveCount++;
            return actor.Id;
        }

        /// <summary>标记待销毁。帧末才真正回收，所以同一帧后面的阶段仍能看见它。</summary>
        public void RequestDespawn(EntityId id)
        {
            if (!id.IsValid) return;
            if (!TryGet(id, out _)) return;
            _pending.Add(id);
        }

        /// <summary>真正回收：发 EvEntityCleanup，DetachAll，归还工厂，槽位世代保留以便失效旧 id。</summary>
        public void FlushDespawn()
        {
            if (_pending.Count == 0) return;
            _despawnScratch.Clear();
            foreach (var id in _pending)
                _despawnScratch.Add(id);
            _pending.Clear();

            for (int i = 0; i < _despawnScratch.Count; i++)
            {
                var id = _despawnScratch[i];
                if (id.Index <= 0 || id.Index >= _slots.Count) continue;
                var slot = _slots[id.Index];
                if (!slot.Occupied || slot.Generation != id.Generation) continue;

                _world?.Events.Publish(new EvEntityCleanup(id, "Despawn"));

                var actor = slot.Actor;
                actor.DetachAll();
                actor.SetActive(false);
                actor.World = null;
                _factory.Release(actor);

                _slots[id.Index] = new Slot { Generation = slot.Generation, Occupied = false };
                _free.Enqueue(id.Index);
                if (ActiveCount > 0) ActiveCount--;
            }
        }

        /// <summary>把当前活跃实体填进调用方的列表。热路径用这个重载，避免每段分配新列表。</summary>
        public void CopyActiveActors(List<Actor> destination)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            destination.Clear();
            for (int i = 1; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Occupied && slot.Actor != null && slot.Actor.IsActive)
                    destination.Add(slot.Actor);
            }
        }

        public List<Actor> CopyActiveActors()
        {
            var result = new List<Actor>(64);
            CopyActiveActors(result);
            return result;
        }

        public void ClearAll()
        {
            for (int i = 1; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (!slot.Occupied || slot.Actor == null) continue;
                slot.Actor.DetachAll();
                slot.Actor.SetActive(false);
                slot.Actor.World = null;
                _factory.Release(slot.Actor);
            }

            _slots.Clear();
            _slots.Add(new Slot());
            _free.Clear();
            _pending.Clear();
            ActiveCount = 0;
        }
    }
}
