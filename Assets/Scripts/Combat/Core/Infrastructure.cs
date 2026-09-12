using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class CombatTime
    {
        public float Delta { get; private set; }
        public float Time { get; private set; }
        public int Frame { get; private set; }
        public int LogicFrame { get; private set; }
        public float WallDelta { get; private set; }
        public float WallTime { get; private set; }
        public int WallFrame { get; private set; }

        public void AdvanceWall(float delta)
        {
            if (delta < 0f) delta = 0f;
            WallDelta = delta;
            WallTime += delta;
            WallFrame++;
            Frame++;
        }

        public void AdvanceLogic(float delta)
        {
            if (delta < 0f) delta = 0f;
            Delta = delta;
            Time += delta;
            LogicFrame++;
        }

        public void PauseLogic()
        {
            Delta = 0f;
        }

        public void Reset()
        {
            Delta = 0f;
            Time = 0f;
            Frame = 0;
            LogicFrame = 0;
            WallDelta = 0f;
            WallTime = 0f;
            WallFrame = 0;
        }
    }

    public sealed class IntentQueue
    {
        readonly Dictionary<Type, object> _queues = new Dictionary<Type, object>(32);
        readonly List<Action> _clearers = new List<Action>(32);

        Queue<T> Q<T>() where T : struct
        {
            var type = typeof(T);
            if (!_queues.TryGetValue(type, out var boxed))
            {
                var created = new Queue<T>(16);
                _queues[type] = created;
                _clearers.Add(created.Clear);
                return created;
            }

            return (Queue<T>)boxed;
        }

        public void Post<T>(in T intent) where T : struct => Q<T>().Enqueue(intent);

        public bool TryConsume<T>(out T intent) where T : struct
        {
            var q = Q<T>();
            if (q.Count == 0)
            {
                intent = default;
                return false;
            }

            intent = q.Dequeue();
            return true;
        }

        public int Count<T>() where T : struct => Q<T>().Count;

        public void Clear<T>() where T : struct => Q<T>().Clear();

        public void Drain<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var q = Q<T>();
            while (q.Count > 0)
                handler(q.Dequeue());
        }

        public void ClearAll()
        {
            for (int i = 0; i < _clearers.Count; i++)
                _clearers[i]();
        }
    }

    public sealed class EventBus
    {
        readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>(32);

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list))
            {
                list = new List<Delegate>(4);
                _handlers[type] = list;
            }

            if (!list.Contains(handler))
                list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (_handlers.TryGetValue(typeof(T), out var list))
                list.Remove(handler);
        }

        public void Publish<T>(T evt)
        {
            if (!_handlers.TryGetValue(typeof(T), out var list) || list.Count == 0)
                return;
            var snapshot = new List<Delegate>(list);
            for (int i = 0; i < snapshot.Count; i++)
            {
                if (snapshot[i] is Action<T> action)
                    action(evt);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
        }
    }

    public interface IPoolable
    {
        void OnRent();
        void OnReturn();
    }

    public sealed class Pool<T> where T : class
    {
        readonly Func<T> _factory;
        readonly Stack<T> _free;
        readonly HashSet<T> _rented;
        readonly int _maxPooled;

        public int RentedCount => _rented.Count;
        public int PooledCount => _free.Count;

        public Pool(Func<T> factory, int initialCapacity = 0, int maxPooled = 256)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _maxPooled = Math.Max(0, maxPooled);
            _free = new Stack<T>(Math.Max(initialCapacity, 4));
            _rented = new HashSet<T>();
            for (int i = 0; i < initialCapacity; i++)
                _free.Push(_factory());
        }

        public T Rent()
        {
            T item = _free.Count > 0 ? _free.Pop() : _factory();
            if (!_rented.Add(item))
                throw new InvalidOperationException("double rent " + typeof(T).Name);
            if (item is IPoolable p) p.OnRent();
            return item;
        }

        public void Return(T item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (!_rented.Remove(item))
                throw new InvalidOperationException("bad return " + typeof(T).Name);
            if (item is IPoolable p) p.OnReturn();
            if (_free.Count < _maxPooled)
                _free.Push(item);
        }
    }
}
