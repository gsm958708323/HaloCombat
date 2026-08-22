using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class AoEService
    {
        readonly CombatWorld _world;
        readonly IntentQueue _intents;

        // 同一脉冲源对同一目标的去重：本帧内同 Source+Target 只打一次
        readonly HashSet<long> _framePairDedup = new HashSet<long>();

        public AoEService(CombatWorld world, IntentQueue intents)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
        }

        public void Tick()
        {
            _framePairDedup.Clear();
            _intents.Drain<AoEIntent>(Resolve);
        }

        void Resolve(AoEIntent aoe)
        {
            if (aoe.Shape != AoEShapeType.Circle)
                throw new NotSupportedException("MVP only Circle");

            var actors = _world.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                var target = actors[i];
                if (!target.TryGetComp<HurtboxComp>(out var hurt) || !hurt.CanBeHit)
                    continue;
                if (!target.TryGetComp<TransformComp>(out var ttf))
                    continue;
                if (!target.TryGetComp<TeamComp>(out var team))
                    continue;

                if (team.Team == aoe.OwnerTeam)
                    continue;

                if (!aoe.HitOwner && target.Id == aoe.Owner)
                    continue;

                if (target.Id == aoe.Source && aoe.Source != aoe.Owner)
                {
                    // 火池 Source 是场地本身：不打自己
                    continue;
                }

                if (!OverlapCircle(
                        aoe.CenterX, aoe.CenterY, aoe.CenterZ, aoe.Radius,
                        ttf.Position, hurt.Radius))
                    continue;

                long pair = PackPair(aoe.Source, target.Id);
                if (!_framePairDedup.Add(pair))
                    continue;

                _intents.Post(new HitIntent(
                    source: aoe.Source,
                    target: target.Id,
                    owner: aoe.Owner,
                    attackSpecValue: aoe.AttackSpecValue,
                    sourceSkillValue: aoe.SourceSkillValue));
            }
        }

        static bool OverlapCircle(
            float cx, float cy, float cz, float radius,
            in SimVec3 p, float hurtRadius)
        {
            float dx = cx - p.X;
            float dy = cy - p.Y;
            float dz = cz - p.Z;
            float r = radius + hurtRadius;
            return dx * dx + dy * dy + dz * dz <= r * r;
        }

        static long PackPair(EntityId source, EntityId target)
        {
            // 粗打包：足够帧内去重；跨帧靠火池自身节奏
            unchecked
            {
                long a = ((long)source.Index << 32) ^ (uint)source.Generation;
                long b = ((long)target.Index << 32) ^ (uint)target.Generation;
                return a ^ (b * 397);
            }
        }
    }
}
