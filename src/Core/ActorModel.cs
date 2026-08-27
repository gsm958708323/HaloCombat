using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public abstract class Comp
    {
        protected Actor Self { get; private set; }
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

    public sealed class Actor
    {
        readonly Dictionary<Type, Comp> _comps = new Dictionary<Type, Comp>(16);
        readonly List<Comp> _order = new List<Comp>(16);
        readonly List<Comp> _ticks = new List<Comp>(8);

        public EntityId Id { get; private set; }
        public bool IsActive { get; private set; }
        public CombatWorld World { get; internal set; }

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

        public T GetComp<T>() where T : Comp
        {
            if (TryGetComp<T>(out var c)) return c;
            throw new InvalidOperationException("Actor " + Id + " missing " + typeof(T).Name);
        }

        public bool TryGetComp<T>(out T comp) where T : Comp
        {
            if (_comps.TryGetValue(typeof(T), out var exact))
            {
                comp = (T)exact;
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
        }
    }

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
        readonly List<Actor> _activeScratch = new List<Actor>(64);

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
            _slots[index] = new Slot { Actor = actor, Generation = gen, Occupied = true };
            actor.AttachAll();
            ActiveCount++;
            return actor.Id;
        }

        public void RequestDespawn(EntityId id)
        {
            if (!id.IsValid) return;
            if (!TryGet(id, out _)) return;
            _pending.Add(id);
        }

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

        public List<Actor> CopyActiveActors()
        {
            _activeScratch.Clear();
            for (int i = 1; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                if (slot.Occupied && slot.Actor != null && slot.Actor.IsActive)
                    _activeScratch.Add(slot.Actor);
            }

            return _activeScratch;
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
            _activeScratch.Clear();
            ActiveCount = 0;
        }
    }
}
