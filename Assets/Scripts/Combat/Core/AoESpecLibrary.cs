using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum AoEShapeType : byte
    {
        Circle = 1,
        // Box = 2,
        // Fan = 3,
    }

    public readonly struct AoEIntent
    {
        public readonly EntityId Source;   // 释放者或火池 Actor
        public readonly EntityId Owner;    // 伤害归属（玩家）
        public readonly int OwnerTeam;
        public readonly AoEShapeType Shape;
        public readonly float CenterX, CenterY, CenterZ;
        public readonly float Radius;
        public readonly int AttackSpecValue;
        public readonly int SourceSkillValue;
        public readonly bool HitOwner;     // 默认 false

        public AoEIntent(
            EntityId source,
            EntityId owner,
            int ownerTeam,
            AoEShapeType shape,
            float cx, float cy, float cz,
            float radius,
            int attackSpecValue,
            int sourceSkillValue,
            bool hitOwner = false)
        {
            Source = source;
            Owner = owner;
            OwnerTeam = ownerTeam;
            Shape = shape;
            CenterX = cx; CenterY = cy; CenterZ = cz;
            Radius = radius;
            AttackSpecValue = attackSpecValue;
            SourceSkillValue = sourceSkillValue;
            HitOwner = hitOwner;
        }
    }

    [Serializable]
    public sealed class AoESpec
    {
        public int Id = 1;
        public AoEShapeType Shape = AoEShapeType.Circle;
        public float Radius = 1.5f;
        public int AttackSpecValue = 42;
        public float OffsetX, OffsetY, OffsetZ; // 相对释放者
        public bool HitOwner;
    }

    public sealed class AoESpecLibrary
    {
        readonly Dictionary<int, AoESpec> _map = new Dictionary<int, AoESpec>(16);

        public void Register(AoESpec spec)
        {
            if (spec == null || spec.Id == 0)
                throw new ArgumentException("Invalid AoESpec");
            _map[spec.Id] = spec;
        }

        public bool TryGet(int id, out AoESpec spec) => _map.TryGetValue(id, out spec);
    }
}
