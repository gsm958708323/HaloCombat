using System;
using System.Collections.Generic;
namespace Combat.Core
{
    // Target acquisition and steering are shared by all motion strategies.
    internal sealed class ProjectileSteering
    {
        readonly CombatWorld _world;
        readonly List<Actor> _buffer = new List<Actor>(64);
        public ProjectileSteering(CombatWorld world) => _world = world;
        /// <summary>锁定转向：锁定目标失效时按 HomingRetarget 决定重选还是保持原航向；单帧转角同时受 HomingRate*dt 与 HomingMaxTurn 限制。</summary>
        public void Update(Actor proj, ProjectileComp body, TransformComp tf, float dt, Actor owner)
        {
            var def = body.Def;
            if (def == null || def.HomingRate <= 0f) return;
            if (!body.HomingTarget.IsValid)
                body.HomingTarget = AcquireNearest(tf.Position, owner, def);
            else if (!IsValid(body.HomingTarget))
            {
                // A projectile keeps its last heading when the locked target dies.
                // Optional retargeting is an explicit definition flag.
                if (!def.HomingRetarget) return;
                body.HomingTarget = AcquireNearest(tf.Position, owner, def);
            }
            if (!IsValid(body.HomingTarget) || !_world.TryGetActor(body.HomingTarget, out var target) ||
                !target.TryGetComp<TransformComp>(out var targetTf)) return;

            float want = LocomotionComp.YawFromStick(new SimVec3(
                targetTf.Position.X - tf.Position.X, 0f, targetTf.Position.Z - tf.Position.Z));
            float delta = NormalizeDeg(want - tf.YawDegrees);
            float step = def.HomingRate * dt;
            if (def.HomingMaxTurn > 0f && step > def.HomingMaxTurn) step = def.HomingMaxTurn;
            if (delta > step) delta = step;
            else if (delta < -step) delta = -step;
            tf.YawDegrees += delta;
        }

        /// <summary>锁定目标是否仍可追击：实体存在且未挂 Dead 标签。</summary>
        bool IsValid(EntityId id)
        {
            if (!_world.TryGetActor(id, out var a) || a == null) return false;
            return !a.TryGetComp<TagComp>(out var tags) || !tags.Has(CommonTags.Dead);
        }

        /// <summary>在 HomingAcquireRadius 内重新锁定最近的可命中目标。</summary>
        EntityId AcquireNearest(SimVec3 origin, Actor owner, ProjectileDefinition def)
        {
            int n = _world.Query.OverlapCircle(origin, def.HomingAcquireRadius, owner, def.HostileMask, _buffer);
            float best = float.MaxValue;
            EntityId pick = EntityId.Invalid;
            for (int i = 0; i < n; i++)
            {
                var v = _buffer[i];
                if (v == null || !v.TryGetComp<TransformComp>(out var tf)) continue;
                float dx = tf.Position.X - origin.X;
                float dz = tf.Position.Z - origin.Z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; pick = v.Id; }
            }
            return pick;
        }

        /// <summary>把角度归一到 ±180 以内，避免累计转向出现大跳变。</summary>
        static float NormalizeDeg(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }

    }
}
