using System;

namespace Combat.Core
{
    /// <summary>
    /// 实体句柄。Index 复用槽位，Generation 递增防止「已销毁 Id 误命中」。
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId Invalid = new EntityId(0, 0);

        public readonly int Index;
        public readonly int Generation;

        public EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public bool IsValid => Index > 0 && Generation > 0;

        public bool Equals(EntityId other)
            => Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj)
            => obj is EntityId other && Equals(other);

        public override int GetHashCode()
            => unchecked((Index * 397) ^ Generation);

        public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
        public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);

        public override string ToString()
            => IsValid ? $"Entity({Index}:{Generation})" : "Entity(Invalid)";
    }
}