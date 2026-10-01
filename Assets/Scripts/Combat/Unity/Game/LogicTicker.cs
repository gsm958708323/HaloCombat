using System;

namespace Combat.Unity.Game
{
    /// <summary>
    /// 逻辑固定步长驱动器：50Hz（Step = 1/50）。真实帧时长先折进累加器再按固定步长补跑，
    /// 所以帧率高低不改变逻辑步长，随机数与判定才可复现。
    /// 追帧上限 0.25s：进程挂起 / 断点 / 长卡顿造成的巨大 dt 最多只补 15 步，多余时间直接丢弃。
    /// 这不是省性能，而是防死亡螺旋——欠账补得越多，下一帧的 dt 就越大。
    /// </summary>
    public sealed class LogicTicker
    {
        public const float Step = 1f / 50f;
        float _acc;
        /// <summary>本帧补跑后不足一步的余量，供插值使用（见 RenderLogicTime）。</summary>
        public float Remainder => _acc;

        /// <summary>清空累加器；重开一局 / 传送这类世界重置后调用，避免把上一局的欠账补进来。</summary>
        public void Reset() => _acc = 0f;

        /// <summary>
        /// dt 是真实帧时长，负值夹到 0，保证累加器只增不减。
        /// 累加器封顶 0.25s 后连续补跑：tick 收到的始终是 Step，而不是真实 dt。
        /// tick 为 null 时直接返回，不消耗也不累加。
        /// </summary>
        public void Accumulate(float dt, Action<float> tick)
        {
            if (tick == null)
                return;
            if (dt < 0)
                dt = 0;
            // 0.25s 追帧上限，见类注释：一次长卡顿只补 15 步。
            _acc = Math.Min(.25f, _acc + dt);
            while (_acc >= Step)
            {
                tick(Step);
                _acc -= Step;
            }
        }

        /// <summary>
        /// 插值用的逻辑时刻：latest 是刚补跑完的世界时间，减一个 Step 得到“上一逻辑帧”，
        /// 再加余量 acc 就落在两逻辑帧之间；表现层按这个时刻插值，可抹掉 50Hz 的台阶感。
        /// </summary>
        public float RenderLogicTime(float latest) => latest - Step + _acc;
    }
}
