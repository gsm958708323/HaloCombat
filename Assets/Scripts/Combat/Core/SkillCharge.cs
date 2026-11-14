using System;

namespace Combat.Core
{
    public enum SkillCastMode { Instant, Channel, Charge }

    public sealed class ChargeDefinition
    {
        public float MinimumTime = .2f;
        public float MaximumTime = 1f;
        public float MinimumPower = 1f;
        public float MaximumPower = 3f;
        public bool AutoRelease = true;

        public float Progress(float time) => Math.Max(0, Math.Min(1, (time - MinimumTime) / (MaximumTime - MinimumTime)));
        public float Power(float time) => MinimumPower + (MaximumPower - MinimumPower) * Progress(time);
    }
}
