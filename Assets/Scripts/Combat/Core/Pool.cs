using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface IPoolable
    {
        void OnRent();
        void OnReturn();
    }

    public interface IPool<T> where T : class
    {
        T Rent();
        void Return(T item);
        int RentedCount { get; }
        int PooledCount { get; }
    }

    /// <summary>
    /// 简易无锁对象池（逻辑单线程 Tick 假设）。
    /// 工厂负责 new；回池前必须 OnReturn 清脏状态。
    /// </summary>
    public sealed class Pool<T> : IPool<T> where T : class
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
                throw new InvalidOperationException($"Pool<{typeof(T).Name}>: double rent detected.");

            if (item is IPoolable poolable)
                poolable.OnRent();

            return item;
        }

        public void Return(T item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!_rented.Remove(item))
                throw new InvalidOperationException(
                    $"Pool<{typeof(T).Name}>: return of non-rented or double-return.");

            if (item is IPoolable poolable)
                poolable.OnReturn();

            if (_free.Count < _maxPooled)
                _free.Push(item);
            // 超出上限直接丢弃，交给 GC；避免池无限涨
        }
    }
}
