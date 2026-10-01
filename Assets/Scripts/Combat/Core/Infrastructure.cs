using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>Per-actor simulation clock. Requests latch at the next world step.</summary>
    public sealed class ActorTime
    {
        int _pending;
        int _remaining;
        public float Time { get; private set; }
        public float Delta { get; private set; }
        public bool IsStopped { get; private set; }
        public int HitstopLeft => _remaining;
        public void RequestHitstop(int frames) => _pending = Math.Max(_pending, frames);
        internal void BeginStep(float dt)
        {
            _remaining = Math.Max(_remaining, _pending);
            _pending = 0;
            IsStopped = _remaining > 0;
            Delta = IsStopped ? 0f : Math.Max(0f, dt);
            Time += Delta;
            if (IsStopped) _remaining--;
        }
        internal void Reset()
        {
            _pending = _remaining = 0;
            Time = Delta = 0f;
            IsStopped = false;
        }
    }

    /// <summary>
    /// 战斗双时钟。wall 墙钟由 <see cref="AdvanceWall"/> 推进，永不停摆，供表现/UI/计时使用；
    /// logic 逻辑钟由 <see cref="AdvanceLogic"/> 推进，是模拟的唯一时间来源。Hitstop 期间只推 wall、不推 logic
    /// （或经 <see cref="PauseLogic"/> 把 Delta 置零），所以顿帧只冻结模拟，不影响表现节奏。
    /// 坑：Frame 只由 AdvanceWall 自增（它是墙钟帧号），模拟步数必须看 LogicFrame，两者不可混用。
    /// </summary>
    public sealed class CombatTime
    {
        public float Delta { get; private set; }        // logic 钟本步的 dt
        public float Time { get; private set; }         // logic 钟累计时间
        public int Frame { get; private set; }          // wall 钟帧号，由 AdvanceWall 自增
        public int LogicFrame { get; private set; }     // logic 钟步数
        public float WallDelta { get; private set; }    // wall 钟本帧 dt
        public float WallTime { get; private set; }     // wall 钟累计时间
        public int WallFrame { get; private set; }      // wall 钟帧数

        /// <summary>
        /// 只推进 wall 时钟（并自增 Frame）。Hitstop 期间仍要调用它，表现层才不会被冻住。
        /// 负 delta 夹到 0，避免时间倒流。
        /// </summary>
        public void AdvanceWall(float delta)
        {
            if (delta < 0f) delta = 0f;
            WallDelta = delta;
            WallTime += delta;
            WallFrame++;
            Frame++;
        }

        /// <summary>
        /// 只推进 logic 时钟（模拟唯一的 dt 来源）。负 delta 夹到 0，避免时间倒流。
        /// </summary>
        public void AdvanceLogic(float delta)
        {
            if (delta < 0f) delta = 0f;
            Delta = delta;
            Time += delta;
            LogicFrame++;
        }

        /// <summary>
        /// 把本步 logic 的 Delta 置零但不动 Time/LogicFrame：用于“这一帧模拟停摆、但时间轴不跳变”的暂停。
        /// 与“干脆不调用 AdvanceLogic”的区别是消费者本帧读到的 Delta 明确为 0，而不是上一帧的旧值。
        /// </summary>
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

    /// <summary>
    /// 帧内意图队列：按 struct 类型各持一条 FIFO 队列，输入侧 Post，各系统在约定阶段 TryConsume/Drain。
    /// 队列本身不感知帧或阶段，什么时候清空由调用方决定——忘记在阶段边界清理会让上一阶段的意图泄漏到下一阶段。
    /// </summary>
    public sealed class IntentQueue
    {
        readonly Dictionary<Type, object> _queues = new Dictionary<Type, object>(32);
        readonly List<Action> _clearers = new List<Action>(32);

        // 用 object 抹掉泛型后统一缓存，并顺带缓存各队列的 Clear 委托，ClearAll 才能不反射地一次清干净。
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

        /// <summary>
        /// 排空该类型的全部意图。注意是 while 而非快照：handler 里再 Post 同类型意图会被本次 Drain 一并消费，
        /// 想拿它做级联处理就要防自激死循环。
        /// </summary>
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

        /// <summary>
        /// 幂等订阅：同一委托重复订阅只保留一份，避免同一事件被回调两次；null 直接抛错，尽早暴露误用。
        /// </summary>
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

        /// <summary>
        /// 发布事件。三条快照契约必须守住，否则订阅方在回调里增删会读到脏数据：
        /// 1) 回调里 Unsubscribe 不影响本次遍历（遍历的是发布瞬间的副本）；
        /// 2) 回调里嵌套 Publish 不会覆盖外层快照（每次发布各自新建副本，互不共享）；
        /// 3) 发布期间新增的订阅者本次收不到，下一次发布才生效。
        /// 代价是每次发布都复制一份委托列表，热路径上的高频事件要留意这份分配。
        /// </summary>
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

    /// <summary>
    /// 池化对象的生命周期钩子：Rent 后调 OnRent、归还前调 OnReturn，用来重置上一轮遗留的瞬时状态。
    /// </summary>
    public interface IPoolable
    {
        void OnRent();
        void OnReturn();
    }

    /// <summary>
    /// 通用对象池。租还都在 _rented 里记账：重复 Rent 同一实例、或归还未租出的实例会当场抛错，
    /// 把误用暴露在出错点而不是留给后续的状态错乱。超过 maxPooled 的归还对象直接交给 GC。
    /// </summary>
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
