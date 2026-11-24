namespace Combat.Core
{
    public sealed class BlinkStrikeEffect : IEffect
    {
        readonly IEffect[] _effects;
        readonly float _distance,_radius;
        readonly int _maximum;
        readonly bool _requiresTarget;
        public BlinkStrikeEffect(IEffect[] effects,float distance,float radius,int maximum,bool requiresTarget)
        { _effects=effects;_distance=distance;_radius=radius;_maximum=maximum;_requiresTarget=requiresTarget; }
        public void Apply(ref EffectContext context)
        {
            if(context.Source==null||!context.Source.IsActive||context.World==null||!context.HasPoint)return;
            var point=context.Point;
            if(_requiresTarget)
            {
                if(context.Target==null||!AreaTargets.TryLive(context.World,context.Target.Id,out var target))return;
                point=target.GetComp<TransformComp>().Position;
            }
            var origin=context.Source.GetComp<TransformComp>().Position;
            if(SpatialGeometry.DistanceSquared(origin,point)>_distance*_distance+.0001f)return;
            if(!context.Source.GetComp<LocomotionComp>().RequestTeleport(point))return;
            var delivery=context;delivery.Point=point;delivery.HasPoint=true;
            new CircleEffects(_effects,EffectCenter.Point,_radius,_maximum).Apply(ref delivery);
        }
    }
}
