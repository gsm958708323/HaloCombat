namespace Combat.Core
{
    public sealed class RayStrikeEffect : IEffect
    {
        readonly IEffect[] _hit,_wall,_complete;
        readonly float _length,_width;
        readonly bool _pierce;
        readonly int _maximum;
        public RayStrikeEffect(IEffect[] hit,IEffect[] wall,IEffect[] complete,float length,float width,bool pierce,int maximum)
        {_hit=hit;_wall=wall;_complete=complete;_length=length;_width=width;_pierce=pierce;_maximum=maximum;}
        public void Apply(ref EffectContext context)
        {
            if(context.World==null||context.Source==null||!context.Source.IsActive)return;
            var from=context.Source.GetComp<TransformComp>().Position;
            var direction=context.HasDir?context.Dir:LocomotionComp.ForwardFromYaw(context.Source.GetComp<TransformComp>().YawDegrees);
            float magnitude=LocomotionComp.StickMag(direction);if(magnitude<=.000001f)return;
            direction=new SimVec3(direction.X/magnitude,0,direction.Z/magnitude);
            var to=new SimVec3(from.X+direction.X*_length,from.Y,from.Z+direction.Z*_length);
            var obstacle=context.World.Collision.Trace(from,to,_width*.5f);
            float limit=obstacle.Blocked?obstacle.Fraction:1;
            int hits=0;bool stopped=false;
            foreach(var hit in LineTargeting.Query(context.World,context.Source,from,to,_width*.5f))
            {
                if(obstacle.Blocked?hit.Fraction>=limit-.000001f:hit.Fraction>limit+.000001f)break;
                if(!AreaTargets.TryLive(context.World,hit.Target,out var target))continue;
                context.World.Deliver(_hit,context.Source,target,context.SnapshotAtk,hit.Point,direction,context.BuffStacks,context.Power,context.Origin);hits++;
                if(!_pierce||(_maximum>0&&hits>=_maximum)){to=hit.Point;stopped=true;break;}
            }
            if(obstacle.Blocked&&!stopped)
            {
                to=obstacle.Point;
                context.World.Events.Publish(new EvObstacleHit(context.Source.Id,context.Source.Id,obstacle.Id,obstacle.Point,obstacle.Normal,"ray"));
                context.World.Deliver(_wall,context.Source,null,context.SnapshotAtk,to,direction,context.BuffStacks,context.Power,context.Origin);
            }
            context.World.Events.Publish(new EvRayResolved(context.Source.Id,from,to,_width,hits));
            context.World.Deliver(_complete,context.Source,null,context.SnapshotAtk,to,direction,context.BuffStacks,context.Power,context.Origin);
        }
    }
}
