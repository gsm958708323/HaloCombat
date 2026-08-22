using System;
using System.Collections.Generic;

namespace Combat.Core
{
    [Serializable]
    public sealed class PulseZoneSpec
    {
        public int Id = 1;
        public float Radius = 1.2f;
        public float Lifetime = 3f;
        public float Interval = 0.5f;
        public int AttackSpecValue = 42;
        public float OffsetX, OffsetY = 0f, OffsetZ;
        public int AoESpecValue; // 若 >0，半径/Attack 以 AoESpec 为准；否则用上面字段
    }


    public sealed class PulseZoneSpecLibrary
    {
        readonly Dictionary<int, PulseZoneSpec> _map = new Dictionary<int, PulseZoneSpec>(8);

        public void Register(PulseZoneSpec spec)
        {
            if (spec == null || spec.Id == 0)
                throw new ArgumentException("Invalid PulseZoneSpec");
            _map[spec.Id] = spec;
        }

        public bool TryGet(int id, out PulseZoneSpec spec) => _map.TryGetValue(id, out spec);
    }

    public readonly struct SpawnPulseZoneIntent
    {
        public readonly EntityId Owner;
        public readonly int SpecValue;

        public SpawnPulseZoneIntent(EntityId owner, int specValue)
        {
            Owner = owner;
            SpecValue = specValue;
        }
    }
}
