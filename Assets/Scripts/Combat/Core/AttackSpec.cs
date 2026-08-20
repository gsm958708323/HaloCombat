using System;
using System.Collections.Generic;

namespace Combat.Core
{
    [Serializable]
    public sealed class AttackSpec
    {
        public int Id = 1;
        public float Power = 1f;          // 与 Atk 相乘
        public float FlatBonus = 0f;
        public float StunDuration = 0.35f;
        public bool ApplyHitStun = true;
        public bool IgnoreDef;
    }

    public sealed class AttackSpecLibrary
    {
        readonly Dictionary<int, AttackSpec> _map = new Dictionary<int, AttackSpec>(16);

        public void Register(AttackSpec spec)
        {
            if (spec == null || spec.Id == 0)
                throw new ArgumentException("Invalid AttackSpec");
            _map[spec.Id] = spec;
        }

        public bool TryGet(int id, out AttackSpec spec) => _map.TryGetValue(id, out spec);
    }
}
