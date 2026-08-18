using System;

namespace Combat.Core
{
    /// <summary>输入令牌。MVP 用稳定字符串；商用可改 int 哈希。</summary>
    public readonly struct InputToken : IEquatable<InputToken>
    {
        public readonly string Action;

        public InputToken(string action) => Action = action ?? string.Empty;

        public bool IsValid => !string.IsNullOrEmpty(Action);

        public bool Equals(InputToken other)
            => string.Equals(Action, other.Action, StringComparison.Ordinal);

        public override bool Equals(object obj)
            => obj is InputToken t && Equals(t);

        public override int GetHashCode()
            => Action != null ? StringComparer.Ordinal.GetHashCode(Action) : 0;

        public static bool operator ==(InputToken a, InputToken b) => a.Equals(b);
        public static bool operator !=(InputToken a, InputToken b) => !a.Equals(b);

        public override string ToString() => Action;

        public static readonly InputToken Attack   = new InputToken("Attack");
        public static readonly InputToken Jump     = new InputToken("Jump");
        public static readonly InputToken UpAttack = new InputToken("UpAttack");
    }

    /// <summary>
    /// 单槽预输入缓冲（ARPG 手感常用：只保留最近一次可消费指令）。
    /// 专属依赖：逻辑时间（构造注入）。
    /// </summary>
    public sealed class InputBufferComp : Comp
    {
        readonly CombatTime _time;

        InputToken _buffered;
        bool _hasBuffered;
        float _bufferedLogicalTime;
        float _bufferWindow; // 预输入有效窗（秒）

        /// <summary>最近一次成功 Push 的逻辑时间（Clear 后仍保留，供「受击后时间窗」类规则选用）。</summary>
        public float LastPushTime { get; private set; }

        /// <summary>当前是否持有未消费输入。</summary>
        public bool HasBuffered => _hasBuffered;

        public InputBufferComp(CombatTime time, float bufferWindow = 0.2f)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bufferWindow = bufferWindow > 0f ? bufferWindow : 0.2f;
        }

        /// <summary>运行时可改窗（例如角色成长、调试）。</summary>
        public void SetBufferWindow(float seconds)
        {
            _bufferWindow = seconds > 0f ? seconds : 0.01f;
        }

        protected override void OnAttach()
        {
            // 无同 Actor 硬依赖；时间已在构造注入
        }

        protected override void OnDetach()
        {
            ClearBufferedOnly();
        }

        /// <summary>
        /// Adapter 写入。clientTime 可选；MVP 用逻辑时间戳。
        /// </summary>
        public void Push(in InputToken token)
        {
            if (!token.IsValid)
                return;

            _buffered = token;
            _hasBuffered = true;
            _bufferedLogicalTime = _time.Time;
            LastPushTime = _time.Time;
        }

        /// <summary>
        /// 偷看当前缓冲；若已过期则自动丢弃并返回 false。
        /// </summary>
        public bool TryPeek(out InputToken token)
        {
            if (!TryGetValidBuffered(out token))
                return false;
            return true;
        }

        /// <summary>
        /// 连招匹配成功后调用：消费掉当前缓冲。
        /// </summary>
        public bool Consume()
        {
            if (!TryGetValidBuffered(out _))
                return false;

            ClearBufferedOnly();
            return true;
        }

        /// <summary>
        /// 受击 / 死亡：丢掉未消费预输入。
        /// 不重置 LastPushTime，避免误伤「用历史时间做规则」的扩展。
        /// </summary>
        public void Clear()
        {
            ClearBufferedOnly();
        }

        void ClearBufferedOnly()
        {
            _hasBuffered = false;
            _buffered = default;
            _bufferedLogicalTime = 0f;
        }

        bool TryGetValidBuffered(out InputToken token)
        {
            token = default;
            if (!_hasBuffered)
                return false;

            float age = _time.Time - _bufferedLogicalTime;
            if (age > _bufferWindow)
            {
                ClearBufferedOnly();
                return false;
            }

            token = _buffered;
            return true;
        }
    }
}