using System;

namespace Combat.Unity.Game
{
    public sealed class LogicTicker
    {
        public const float Step = 1f / 50f;
        float _acc;
        public float Remainder => _acc;

        public void Reset() => _acc = 0f;

        public void Accumulate(float dt, Action<float> tick)
        {
            if (tick == null)
                return;
            if (dt < 0)
                dt = 0;
            _acc = Math.Min(.25f, _acc + dt);
            while (_acc >= Step)
            {
                tick(Step);
                _acc -= Step;
            }
        }

        public float RenderLogicTime(float latest) => latest - Step + _acc;
    }
}
