using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface IRandom
    {
        float Next01();
    }

    public sealed class SeededRandom : IRandom
    {
        readonly Random _rng;
        public SeededRandom(int seed = 1) => _rng = new Random(seed);
        public float Next01() => (float)_rng.NextDouble();
    }

    public sealed class FixedRandom : IRandom
    {
        readonly float _value;
        public FixedRandom(float value01) => _value = value01;
        public float Next01() => _value;
    }

    public interface ITargetQuery
    {
        int OverlapCircle(SimVec3 center, float radius, Actor source, int hostileMask, Actor[] buffer);
        int OverlapFan(SimVec3 origin, float yawDegrees, float radius, float halfAngleDegrees, Actor source, int hostileMask, Actor[] buffer);
    }

    public sealed class SimpleTargetQuery : ITargetQuery
    {
        CombatWorld _world;
        public void Bind(CombatWorld world) => _world = world;

        public int OverlapCircle(SimVec3 center, float radius, Actor source, int hostileMask, Actor[] buffer)
        {
            if (_world == null || buffer == null || buffer.Length == 0) return 0;
            float r2 = radius * radius;
            int n = 0;
            var actors = _world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (a == null || !a.IsActive || a == source) continue;
                if (!PassesFilter(source, a, hostileMask)) continue;
                if (!a.TryGetComp<TransformComp>(out var tf)) continue;
                float dx = tf.Position.X - center.X;
                float dz = tf.Position.Z - center.Z;
                if (dx * dx + dz * dz > r2) continue;
                buffer[n++] = a;
                if (n >= buffer.Length) break;
            }

            return n;
        }

        public int OverlapFan(SimVec3 origin, float yawDegrees, float radius, float halfAngleDegrees, Actor source, int hostileMask, Actor[] buffer)
        {
            int n = OverlapCircle(origin, radius, source, hostileMask, buffer);
            if (n <= 0) return 0;
            float half = halfAngleDegrees < 0f ? 0f : halfAngleDegrees;
            int w = 0;
            for (int i = 0; i < n; i++)
            {
                var tf = buffer[i].GetComp<TransformComp>();
                float dx = tf.Position.X - origin.X;
                float dz = tf.Position.Z - origin.Z;
                float yaw = (float)(Math.Atan2(dz, dx) * (180.0 / Math.PI));
                float delta = NormalizeAngle(yaw - yawDegrees);
                if (Math.Abs(delta) <= half)
                    buffer[w++] = buffer[i];
            }

            return w;
        }

        static bool PassesFilter(Actor source, Actor target, int hostileMask)
        {
            // Runtime bodies are not combatants. They carry a TeamComp only so
            // spawned projectiles/AoEs can inherit ownership, but must never become
            // AI or hit-scan targets themselves.
            if (target.TryGetComp<ProjectileComp>(out _) || target.TryGetComp<AoeComp>(out _))
                return false;
            if (target.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead))
                return false;
            if (!target.TryGetComp<TeamComp>(out var tt))
                return false;
            if (hostileMask != 0)
                return (hostileMask & (1 << tt.TeamId)) != 0;
            if (source == null || !source.TryGetComp<TeamComp>(out var st))
                return false;
            return st.IsHostileTo(tt);
        }

        static float NormalizeAngle(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }
    }

    public sealed class HitDetectService
    {
        readonly CombatWorld _world;
        readonly Actor[] _buffer = new Actor[32];

        public HitDetectService(CombatWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public void Tick()
        {
            var actors = _world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var attacker = actors[i];
                if (!attacker.TryGetComp<HitboxComp>(out var box) || !box.IsOpen) continue;
                if (box.BakedOnHit == null || box.BakedOnHit.Length == 0) continue;
                if (!attacker.TryGetComp<TransformComp>(out var tf)) continue;

                var center = WorldPoint(tf, box.LocalOffset);
                int n = _world.Query.OverlapCircle(center, box.Radius, attacker, 0, _buffer);
                float snapshotAtk = 0f;
                if (attacker.TryGetComp<AttributeSet>(out var attr))
                    snapshotAtk = attr.GetFinal(AttrId.Atk);

                for (int k = 0; k < n; k++)
                {
                    var victim = _buffer[k];
                    if (!box.TryRecord(victim.Id)) continue;
                    var point = victim.TryGetComp<TransformComp>(out var vtf) ? vtf.Position : center;
                    _world.Intents.Post(new ApplyEffectsIntent(
                        box.BakedOnHit, attacker.Id, victim.Id, snapshotAtk, 0, point, true));
                }
            }
        }

        static SimVec3 WorldPoint(TransformComp tf, SimVec3 local)
        {
            double r = tf.YawDegrees * Math.PI / 180.0;
            float c = (float)Math.Cos(r);
            float s = (float)Math.Sin(r);
            return new SimVec3(
                tf.Position.X + local.X * c - local.Z * s,
                tf.Position.Y + local.Y,
                tf.Position.Z + local.X * s + local.Z * c);
        }
    }
}
