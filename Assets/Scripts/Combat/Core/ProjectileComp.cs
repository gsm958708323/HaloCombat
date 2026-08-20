using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class HurtboxComp : Comp
    {
        public float Radius = 0.5f;
        public bool CanBeHit = true;
        public void SetRadius(float r) => Radius = r > 0f ? r : 0.01f;
    }

    public sealed class ProjectileContactComp : Comp
    {
        public EntityId Owner;
        public int Team;
        public float Radius;
        public int AttackSpecValue;
        public bool Pierce;
        public int SourceSkillValue;
        public void Setup(
            EntityId owner,
            int team,
            float radius,
            int attackSpecValue,
            bool pierce,
            int sourceSkillValue)
        {
            Owner = owner;
            Team = team;
            Radius = radius > 0f ? radius : 0.01f;
            AttackSpecValue = attackSpecValue;
            Pierce = pierce;
            SourceSkillValue = sourceSkillValue;
        }
    }

    public sealed class ProjectileHitRecordComp : Comp
    {
        readonly HashSet<int> _hitIndexPacked = new HashSet<int>();
        // 用 Index+Generation 打包不够安全时改存 EntityId；MVP 存两个 int 的字符串键更稳
        readonly HashSet<long> _hits = new HashSet<long>();
        public bool HasHit(EntityId id)
        {
            if (!id.IsValid) return true;
            return _hits.Contains(Pack(id));
        }
        public void Record(EntityId id)
        {
            if (!id.IsValid) return;
            _hits.Add(Pack(id));
        }
        public void Clear() => _hits.Clear();
        protected override void OnDetach() => Clear();
        static long Pack(EntityId id)
            => ((long)id.Index << 32) ^ (uint)id.Generation;
    }

    public sealed class ProjectileMoveComp : Comp
    {
        TransformComp _tf;
        public SimVec3 Velocity;
        public override bool WantsTick => true;
        protected override void OnAttach()
        {
            _tf = Self.GetComp<TransformComp>();
        }
        protected override void OnDetach()
        {
            _tf = null;
            Velocity = SimVec3.Zero;
        }
        public void SetVelocity(in SimVec3 v) => Velocity = v;
        public override void Tick(float dt)
        {
            if (_tf == null) return;
            _tf.Position = new SimVec3(
                _tf.Position.X + Velocity.X * dt,
                _tf.Position.Y + Velocity.Y * dt,
                _tf.Position.Z + Velocity.Z * dt);
        }
    }

    /// <summary>寿命到点 → 投递自毁 Intent，不直接抓 Registry（保持 Comp 瘦依赖）。</summary>
    public sealed class ProjectileLifetimeComp : Comp
    {
        readonly IntentQueue _intents;
        float _left;
        bool _armed;
        public override bool WantsTick => true;
        public ProjectileLifetimeComp(IntentQueue intents)
        {
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
        }
        public void Arm(float lifetime)
        {
            _left = lifetime > 0f ? lifetime : 0.01f;
            _armed = true;
        }
        protected override void OnDetach()
        {
            _armed = false;
        }
        public override void Tick(float dt)
        {
            if (!_armed) return;
            _left -= dt;
            if (_left > 0f) return;
            _armed = false;
            _intents.Post(new DespawnEntityIntent(Self.Id));
        }
    }
    public readonly struct DespawnEntityIntent
    {
        public readonly EntityId Target;
        public DespawnEntityIntent(EntityId target) => Target = target;
    }
}