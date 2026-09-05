using System;
using System.Collections.Generic;
using Combat.Core;

namespace Combat.Presentation
{
    public abstract class PresentComp
    {
        protected PresentActor Self { get; private set; }
        public virtual bool WantsLateTick => false;
        public virtual bool WantsLogicSync => false;

        internal void Attach(PresentActor self)
        {
            Self = self;
            OnAttach();
        }

        internal void Detach()
        {
            OnDetach();
            Self = null;
        }

        protected virtual void OnAttach() { }

        protected virtual void OnDetach() { }

        public virtual void SyncLogic(CombatWorld world) { }

        public virtual void LateTick(float renderDt) { }
    }

    public sealed class PresentActor
    {
        readonly Dictionary<Type, PresentComp> _map = new Dictionary<Type, PresentComp>(8);
        readonly List<PresentComp> _order = new List<PresentComp>(8);
        readonly List<PresentComp> _sync = new List<PresentComp>(8);
        readonly List<PresentComp> _late = new List<PresentComp>(8);
        public EntityId Id { get; private set; }
        public string BlueprintId { get; private set; }
        public bool Bound => Id.IsValid;

        public void Add(PresentComp comp)
        {
            if (comp == null)
                throw new ArgumentNullException(nameof(comp));
            var t = comp.GetType();
            if (_map.ContainsKey(t))
                throw new InvalidOperationException("Duplicate present: " + t.Name);
            _map.Add(t, comp);
            _order.Add(comp);
        }

        public void Bind(EntityId id, string blueprintId)
        {
            Id = id;
            BlueprintId = blueprintId ?? string.Empty;
            _sync.Clear();
            _late.Clear();
            for (int i = 0; i < _order.Count; i++)
            {
                var c = _order[i];
                c.Attach(this);
                if (c.WantsLogicSync)
                    _sync.Add(c);
                if (c.WantsLateTick)
                    _late.Add(c);
            }
        }

        public bool TryLogic(CombatWorld world, out Actor actor)
        {
            actor = null;
            return world != null
                && Id.IsValid
                && world.TryGetActor(Id, out actor)
                && actor != null
                && actor.IsActive;
        }

        public T Get<T>()
            where T : PresentComp
        {
            if (TryGet(out T c))
                return c;
            throw new InvalidOperationException("Missing present " + typeof(T).Name);
        }

        public bool TryGet<T>(out T comp)
            where T : PresentComp
        {
            if (_map.TryGetValue(typeof(T), out var exact))
            {
                comp = (T)exact;
                return true;
            }
            for (int i = 0; i < _order.Count; i++)
                if (_order[i] is T m)
                {
                    comp = m;
                    return true;
                }
            comp = null;
            return false;
        }

        public void SyncLogic(CombatWorld world)
        {
            for (int i = 0; i < _sync.Count; i++)
                _sync[i].SyncLogic(world);
        }

        public void LateTick(float dt)
        {
            for (int i = 0; i < _late.Count; i++)
                _late[i].LateTick(dt);
        }

        public void Release()
        {
            for (int i = _order.Count - 1; i >= 0; i--)
                _order[i].Detach();
            _sync.Clear();
            _late.Clear();
            _map.Clear();
            _order.Clear();
            Id = EntityId.Invalid;
            BlueprintId = string.Empty;
        }
    }
}
