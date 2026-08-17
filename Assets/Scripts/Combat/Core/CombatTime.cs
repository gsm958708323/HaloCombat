namespace Combat.Core
{
    public interface ICombatTime
    {
        float Delta { get; }
        float Time { get; }
        int Frame { get; }
        void Advance(float delta);
        void Reset();
    }

    /// <summary>
    /// 纯逻辑时间。World.Tick 入口唯一写入点。
    /// </summary>
    public sealed class CombatTime : ICombatTime
    {
        public float Delta { get; private set; }
        public float Time { get; private set; }
        public int Frame { get; private set; }

        public void Advance(float delta)
        {
            if (delta < 0f)
                delta = 0f;

            // 商用可再夹 maxDelta，防断点恢复后一次跳过大间隔
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
}
