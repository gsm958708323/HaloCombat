using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class CombatTime
    {
        public float Delta { get; private set; }
        public float Time { get; private set; }
        public int Frame { get; private set; }

        public void Advance(float delta)
        {
            if (delta < 0f) delta = 0f;
            Delta = delta;
            Time += delta;
            Frame++;
        }

        public void Reset()
        {
            Delta = 0f;
            Time = 0f;
            Frame = 0;
        }
    }

    public sealed class IntentQueue
    {
        readonly Dictionary<Type, object> _queues = new Dictionary<Type, object>(32);

        Queue<T> Q<T>() where T : struct
        {
            var type = typeof(T);
            if (!_queues.TryGetValue(type, out var boxed))
            {
                var created = new Queue<T>(16);
                _queues[type] = created;
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
            foreach (var kv in _queues)
            {
                if (kv.Value is System.Collections.ICollection)
                    kv.Value.GetType().GetMethod("Clear")?.Invoke(kv.Value, null);
            }
        }
    }

    public sealed class EventBus
    {
        readonly Dictionary<Type, List<Delegate>> _handlers = new Dictionary<Type, List<Delegate>>(32);
        readonly List<Delegate> _snapshot = new List<Delegate>(16);

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
            _snapshot.Clear();
            _snapshot.AddRange(list);
            for (int i = 0; i < _snapshot.Count; i++)
            {
                if (_snapshot[i] is Action<T> action)
                    action(evt);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
            _snapshot.Clear();
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
