using System;

namespace Combat.Presentation
{
    public sealed class SimulationClock
    {
        public readonly float LogicDt;
        public readonly int MaxStepsPerFrame;
        float _accumulator;

        public float Accumulator => _accumulator;
        public float Alpha => LogicDt <= 0f ? 1f : Math.Min(1f, _accumulator / LogicDt);

        public SimulationClock(float logicDt = 0.02f, int maxStepsPerFrame = 4)
        {
            LogicDt = logicDt > 0f ? logicDt : 0.02f;
            MaxStepsPerFrame = maxStepsPerFrame < 1 ? 1 : maxStepsPerFrame;
        }

        public void Reset() => _accumulator = 0f;

        public int BeginFrame(float realDt)
        {
            if (realDt < 0f) realDt = 0f;
            if (realDt > 0.25f) realDt = 0.25f;
            _accumulator += realDt;
            int steps = 0;
            while (_accumulator >= LogicDt && steps < MaxStepsPerFrame)
            {
                _accumulator -= LogicDt;
                steps++;
            }

            if (steps == MaxStepsPerFrame && _accumulator >= LogicDt)
                _accumulator = LogicDt;
            return steps;
        }
    }
}
