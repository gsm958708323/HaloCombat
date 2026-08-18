using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface IEntityRegistry
    {
        bool TryGet(EntityId id, out Actor actor);
        EntityId Spawn(in ActorSpawnSpec spec);
        void RequestDespawn(EntityId id);
        void FlushDespawn();
        int ActiveCount { get; }
        void ClearAll();
    }

    public sealed class EntityRegistry : IEntityRegistry
    {
        struct Slot
        {
            public Actor Actor;
            public int Generation;
            public bool Occupied;
        }

        readonly IActorFactory _factory;
        readonly List<Slot> _slots = new List<Slot>(64);
        readonly Queue<int> _freeIndices = new Queue<int>(32);
        readonly HashSet<EntityId> _pendingDespawn = new HashSet<EntityId>();
        readonly List<EntityId> _despawnScratch = new List<EntityId>(16);
        readonly List<Actor> _activeScratch = new List<Actor>(64);

        public int ActiveCount { get; private set; }

        public EntityRegistry(IActorFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            // index 0 废弃，与 EntityId.Invalid 对齐
            _slots.Add(new Slot { Occupied = false, Generation = 0, Actor = null });
        }

        public bool TryGet(EntityId id, out Actor actor)
        {
            actor = null;
            if (!id.IsValid)
                return false;
            if (id.Index <= 0 || id.Index >= _slots.Count)
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
            if (_freeIndices.Count > 0)
            {
                index = _freeIndices.Dequeue();
                var slot = _slots[index];
                int gen = slot.Generation + 1;
                if (gen <= 0) gen = 1; // 溢出保护

                actor.SetId(new EntityId(index, gen));
                actor.SetActive(true);

                _slots[index] = new Slot
                {
                    Actor = actor,
                    Generation = gen,
                    Occupied = true
                };
            }
            else
            {
                index = _slots.Count;
                actor.SetId(new EntityId(index, 1));
                actor.SetActive(true);
                _slots.Add(new Slot
                {
                    Actor = actor,
                    Generation = 1,
                    Occupied = true
                });
            }

            if (actor is Actor concrete)
                concrete.AttachAll();

            ActiveCount++;
            return actor.Id;
        }

        public void RequestDespawn(EntityId id)
        {
            if (!id.IsValid)
                return;
            if (!TryGet(id, out _))
                return;

            _pendingDespawn.Add(id);
        }

        public void FlushDespawn()
        {
            if (_pendingDespawn.Count == 0)
                return;

            _despawnScratch.Clear();
            foreach (var id in _pendingDespawn)
                _despawnScratch.Add(id);
            _pendingDespawn.Clear();

            for (int i = 0; i < _despawnScratch.Count; i++)
            {
                var id = _despawnScratch[i];
                if (id.Index <= 0 || id.Index >= _slots.Count)
                    continue;

                var slot = _slots[id.Index];
                if (!slot.Occupied || slot.Generation != id.Generation)
                    continue;

                var actor = slot.Actor;
                if (actor is Actor concrete)
                    concrete.DetachAll();

                actor.SetActive(false);
                _factory.Release(actor);

                // 保留 Generation，占用清除，index 回炉
                _slots[id.Index] = new Slot
                {
                    Actor = null,
                    Generation = slot.Generation,
                    Occupied = false
                };
                _freeIndices.Enqueue(id.Index);

                if (ActiveCount > 0)
                    ActiveCount--;
            }
        }

        /// <summary>
        /// 供 World 本地阶段遍历；返回的列表本帧内只读使用。
        /// </summary>
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
                if (!slot.Occupied || slot.Actor == null)
                    continue;

                if (slot.Actor is Actor concrete)
                    concrete.DetachAll();

                slot.Actor.SetActive(false);
                _factory.Release(slot.Actor);
            }

            _slots.Clear();
            _slots.Add(new Slot { Occupied = false, Generation = 0, Actor = null });
            _freeIndices.Clear();
            _pendingDespawn.Clear();
            _activeScratch.Clear();
            ActiveCount = 0;
        }
    }
}
