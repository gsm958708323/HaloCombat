using System;

namespace Combat.Core
{
    public readonly struct BuffTypeId : IEquatable<BuffTypeId>
    {
        public readonly int Value;
        public BuffTypeId(int value) => Value = value;
        public bool Equals(BuffTypeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is BuffTypeId id && Equals(id);
        public override int GetHashCode() => Value;
        public static bool operator ==(BuffTypeId a, BuffTypeId b) => a.Equals(b);
        public static bool operator !=(BuffTypeId a, BuffTypeId b) => !a.Equals(b);
    }

    public readonly struct BuffStack
    {
        public readonly BuffTypeId Type;
        public readonly int Count;
        public readonly float DurationLeft; // 当前剩余秒数
        public readonly float TotalDuration; // 原配置总时长
        public readonly TagSource Source;

        public BuffStack(BuffTypeId type, int count, float durationLeft, float totalDuration, TagSource source)
        {
            Type = type;
            Count = count;
            DurationLeft = durationLeft;
            TotalDuration = totalDuration;
            Source = source;
        }
    }

    public readonly struct BuffApplyArgs
    {
        public readonly BuffTypeId Type;
        public readonly int Stacks;
        public readonly float Duration;
        public readonly TagSource Source;
        public readonly bool RefreshIfExist;
        // public readonly Func<BuffStack, BuffStack> CustomApply; // 扩展点：自定义叠加逻辑

        public BuffApplyArgs(BuffTypeId type, int stacks, float duration, TagSource source, bool refreshIfExist = false)
        {
            Type = type;
            Stacks = stacks;
            Duration = duration;
            Source = source;
            RefreshIfExist = refreshIfExist;
        }
    }
}
