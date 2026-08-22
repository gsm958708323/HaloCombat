using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public readonly struct ProjectileSpecId : IEquatable<ProjectileSpecId>
    {
        public readonly int Value;
        public ProjectileSpecId(int value) => Value = value;
        public bool IsValid => Value != 0;
        public bool Equals(ProjectileSpecId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ProjectileSpecId id && Equals(id);
        public override int GetHashCode() => Value;
        public override string ToString() => $"ProjSpec({Value})";

        public static readonly ProjectileSpecId Bolt901 = new ProjectileSpecId(901);
    }

    [Serializable]
    public sealed class ProjectileSpec
    {
        public ProjectileSpecId Id;
        public float Speed = 12f;
        public float Lifetime = 1.5f;
        public float Radius = 0.35f;
        public float DirX = 1f;   // 相对发射者朝向的本地方向（MVP 用世界近似）
        public float DirY = 0f;
        public float DirZ = 0f;
        public int AttackSpecValue = 1; // 交给后续 Damage；本步只写入 HitIntent
        public bool Pierce;             // false=命中后销毁；true=可穿（仍去重每目标一次）
        public float SpawnOffsetX = 0.5f;
        public float SpawnOffsetY = 1.0f;
        public float SpawnOffsetZ = 0f;
    }

    public sealed class ProjectileSpecLibrary
    {
        readonly Dictionary<int, ProjectileSpec> _map = new Dictionary<int, ProjectileSpec>(16);

        public void Register(ProjectileSpec spec)
        {
            if (spec == null || !spec.Id.IsValid)
                throw new ArgumentException("Invalid ProjectileSpec");
            _map[spec.Id.Value] = spec;
        }

        public bool TryGet(ProjectileSpecId id, out ProjectileSpec spec)
            => _map.TryGetValue(id.Value, out spec);

        public bool TryGet(int value, out ProjectileSpec spec)
            => _map.TryGetValue(value, out spec);

        public ProjectileSpecLibrary Create()
        {
            var projSpecs = new ProjectileSpecLibrary();
            projSpecs.Register(new ProjectileSpec
            {
                Id = ProjectileSpecId.Bolt901,
                Speed = 10f,
                Lifetime = 2f,
                Radius = 0.35f,
                DirX = 1f,
                AttackSpecValue = 42,
                Pierce = false,
                SpawnOffsetX = 0.6f,
                SpawnOffsetY = 1.0f
            });
            return projSpecs;
        }
    }
}
