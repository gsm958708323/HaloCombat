using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler);
        void Unsubscribe<T>(Action<T> handler);
        void Publish<T>(T evt);
        void Clear();
    }

    /// <summary>
    /// 同步事件泵。逻辑线程内立即派发；不做跨线程。
    /// 表现层订阅；玩法真相不得依赖「有没有人订阅」。
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        readonly Dictionary<Type, List<Delegate>> _handlers =
            new Dictionary<Type, List<Delegate>>(32);

        // 派发中允许退订：用快照
        readonly List<Delegate> _snapshot = new List<Delegate>(16);

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

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
            if (handler == null)
                return;

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
}
