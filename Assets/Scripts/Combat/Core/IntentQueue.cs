using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface IIntentQueue
    {
        void Post<T>(in T intent) where T : struct;
        bool TryConsume<T>(out T intent) where T : struct;
        int Count<T>() where T : struct;
        void ClearAll();
        void Clear<T>() where T : struct;
    }

    /// <summary>
    /// 按意图类型分队列。Comp 只 Post；对应 Service 在固定阶段 TryConsume 排空。
    /// </summary>
    public sealed class IntentQueue : IIntentQueue
    {
        readonly Dictionary<Type, object> _queues = new Dictionary<Type, object>(32);

        Queue<T> GetOrCreate<T>() where T : struct
        {
            var type = typeof(T);
            if (!_queues.TryGetValue(type, out var boxed))
            {
                var q = new Queue<T>(16);
                _queues[type] = q;
                return q;
            }

            return (Queue<T>)boxed;
        }

        public void Post<T>(in T intent) where T : struct
        {
            GetOrCreate<T>().Enqueue(intent);
        }

        public bool TryConsume<T>(out T intent) where T : struct
        {
            var type = typeof(T);
            if (_queues.TryGetValue(type, out var boxed))
            {
                var q = (Queue<T>)boxed;
                if (q.Count > 0)
                {
                    intent = q.Dequeue();
                    return true;
                }
            }

            intent = default;
            return false;
        }

        public int Count<T>() where T : struct
        {
            var type = typeof(T);
            if (_queues.TryGetValue(type, out var boxed))
                return ((Queue<T>)boxed).Count;
            return 0;
        }

        public void Clear<T>() where T : struct
        {
            var type = typeof(T);
            if (_queues.TryGetValue(type, out var boxed))
                ((Queue<T>)boxed).Clear();
        }

        public void ClearAll()
        {
            foreach (var kv in _queues)
            {
                // 非泛型 Clear：各队列都是 Queue<T>，用动态清
                if (kv.Value is System.Collections.ICollection)
                {
                    var clear = kv.Value.GetType().GetMethod("Clear");
                    clear?.Invoke(kv.Value, null);
                }
            }
        }

        /// <summary>
        /// Service 阶段推荐：排空某类型并回调，避免 while 样板散落。
        /// </summary>
        public void Drain<T>(Action<T> handler) where T : struct
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            while (TryConsume<T>(out var intent))
                handler(intent);
        }
    }
}
