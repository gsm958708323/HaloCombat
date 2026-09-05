using System;

namespace Combat.Core
{
    public sealed class PerceptionComp : Comp
    {
        readonly Actor[] _scan = new Actor[32];
        readonly float _alertRadius;
        BtBlackboard _board;
        EntityId _forced;
        Action<EvDamage> _onDamage;
        Action<EvImmune> _onImmune;
        public override bool WantsTick => true;
        public bool Enabled { get; set; } = true;
        public float AlertRadius => _alertRadius;
        public EntityId Forced => _forced;
        public PerceptionComp(float alertRadius = 8f) => _alertRadius = alertRadius > 0f ? alertRadius : 8f;

        protected override void OnAttach()
        {
            _onDamage = OnDamage; _onImmune = OnImmune;
            var bus = Self.World != null ? Self.World.Events : null;
            if (bus != null) { bus.Subscribe(_onDamage); bus.Subscribe(_onImmune); }
            BindBoard();
        }
        protected override void OnDetach()
        {
            var bus = Self != null && Self.World != null ? Self.World.Events : null;
            if (bus != null) { bus.Unsubscribe(_onDamage); bus.Unsubscribe(_onImmune); }
            _onDamage = null; _onImmune = null; _board = null; _forced = EntityId.Invalid;
        }
        void BindBoard()
        {
            if (_board == null && Self.TryGetComp<BehaviorTreeComp>(out var bt)) _board = bt.Board;
        }
        public void Alert(EntityId attacker)
        {
            if (!Enabled) return;
            BindBoard();
            if (_board != null && _board.Returning) return;
            if (!IsHostileAlive(attacker)) return;
            _forced = attacker;
            if (_board != null) _board.Target = attacker;
        }
        public void ClearAlert()
        {
            _forced = EntityId.Invalid;
            _board?.ClearTarget();
        }
        public override void Tick(float dt)
        {
            if (!Enabled) return;
            BindBoard();
            if (_board == null) return;
            if (_board.Returning) { _forced = EntityId.Invalid; return; }
            if (IsHostileAlive(_forced))
            {
                if (WithinRadius(_forced, _alertRadius)) { _board.Target = _forced; return; }
                _forced = EntityId.Invalid;
            }
            if (CondHasTarget.IsTargetValid(new BtTick(Self, Self.World, _board, dt)) &&
                WithinRadius(_board.Target, _alertRadius)) return;
            _board.ClearTarget();
            TryScan(_alertRadius);
        }
        public bool TryScan(float radius)
        {
            if (!Enabled) return false;
            BindBoard();
            if (_board == null || Self.World == null || !Self.TryGetComp<TransformComp>(out var tf)) return false;
            int n = Self.World.Query.OverlapCircle(tf.Position, radius > 0f ? radius : _alertRadius, Self, 0, _scan);
            float best = float.MaxValue; EntityId picked = EntityId.Invalid;
            for (int i = 0; i < n; i++)
            {
                var v = _scan[i];
                if (v == null || !v.TryGetComp<TransformComp>(out var vt)) continue;
                float dx = vt.Position.X - tf.Position.X, dz = vt.Position.Z - tf.Position.Z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; picked = v.Id; }
            }
            if (!picked.IsValid) return false;
            _board.Target = picked; return true;
        }
        void OnDamage(EvDamage e) { if (Self != null && e.Target == Self.Id) Alert(e.Source); }
        void OnImmune(EvImmune e) { if (Self != null && e.Target == Self.Id) Alert(e.Source); }
        bool IsHostileAlive(EntityId id)
        {
            if (!id.IsValid || Self.World == null || !Self.World.TryGetActor(id, out var a) || a == null || !a.IsActive) return false;
            if (Self.TryGetComp<TeamComp>(out var ownTeam) && a.TryGetComp<TeamComp>(out var targetTeam) &&
                !ownTeam.IsHostileTo(targetTeam)) return false;
            return !a.TryGetComp<TagComp>(out var tags) || !tags.Has(CommonTags.Dead);
        }

        bool WithinRadius(EntityId id, float radius)
        {
            if (!IsHostileAlive(id) || !Self.TryGetComp<TransformComp>(out var own) ||
                !Self.World.TryGetActor(id, out var target) || !target.TryGetComp<TransformComp>(out var tf))
                return false;
            float dx = tf.Position.X - own.Position.X;
            float dz = tf.Position.Z - own.Position.Z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
