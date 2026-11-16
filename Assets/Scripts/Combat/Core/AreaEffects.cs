using System;

namespace Combat.Core
{
    public enum EffectCenter { Point, Target, Source, Auto }

    public static class EffectPosition
    {
        public static bool TryResolve(in EffectContext context, EffectCenter center, out SimVec3 point)
        {
            point = default;
            if (center == EffectCenter.Auto) center = context.HasPoint ? EffectCenter.Point : EffectCenter.Source;
            if (center == EffectCenter.Point) { point = context.Point; return context.HasPoint; }
            var actor = center == EffectCenter.Source ? context.Source : context.Target;
            if (actor == null || !actor.IsActive || !actor.TryGetComp<TransformComp>(out var transform)) return false;
            point = transform.Position; return true;
        }
    }

    public sealed class TargetEffects : IEffect
    {
        readonly IEffect[] _effects;
        readonly bool _self;
        public TargetEffects(IEffect[] effects, bool self) { _effects = effects; _self = self; }
        public void Apply(ref EffectContext context)
        {
            var target = _self ? context.Source : context.Target;
            if (context.World == null || target == null || !context.World.TryGetActor(target.Id, out var current)) return;
            context.World.Deliver(_effects, context.Source, current, context.SnapshotAtk,
                context.HasPoint ? context.Point : (SimVec3?)null, context.HasDir ? context.Dir : (SimVec3?)null,
                context.BuffStacks, context.Power, context.Origin, context.ProjectileHit,context.Damage,context.Contact);
        }
    }

    public sealed class CircleEffects : IEffect
    {
        readonly IEffect[] _effects;
        readonly EffectCenter _center;
        readonly float _radius;
        readonly int _maximum;
        readonly int _mask;
        public CircleEffects(IEffect[] effects, EffectCenter center, float radius, int maximum = 0,int hostileMask=0)
        { _effects = effects; _center = center; _radius = radius; _maximum = maximum;_mask=hostileMask; }
        public void Apply(ref EffectContext context)
        {
            if (context.World == null || !EffectPosition.TryResolve(context, _center, out var point)) return;
            var targets = AreaTargets.Query(context.World, point, _radius, context.Source, _mask, _maximum);
            context.World.Events.Publish(new EvAreaResolved(context.Source != null ? context.Source.Id : EntityId.Invalid, point, _radius, targets.Length));
            foreach (var id in targets)
            {
                if (!AreaTargets.TryLive(context.World, id, out var target)) continue;
                context.World.Deliver(_effects, context.Source, target, context.SnapshotAtk, point,
                    context.HasDir ? context.Dir : (SimVec3?)null, context.BuffStacks, context.Power, context.Origin, context.ProjectileHit,context.Damage,context.Contact);
            }
        }
    }

    internal static class AreaTargets
    {
        public static EntityId[] Query(CombatWorld world, SimVec3 point, float radius, Actor source, int mask, int maximum)
        {
            var buffer = new Actor[world.RegistryActive().Count];
            int count = world.Query.OverlapCircle(point, radius, source, mask, buffer);
            if (maximum > 0) count = Math.Min(count, maximum);
            var ids = new EntityId[count];
            for (int i = 0; i < count; i++) ids[i] = buffer[i].Id;
            return ids;
        }
        public static bool TryLive(CombatWorld world, EntityId id, out Actor actor)
            => world.TryGetActor(id, out actor) && actor.IsActive && (!actor.TryGetComp<TagComp>(out var tags) || !tags.Has(CommonTags.Dead));
    }
}
