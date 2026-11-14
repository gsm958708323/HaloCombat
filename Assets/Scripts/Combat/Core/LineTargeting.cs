using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public readonly struct LineHit
    {
        public readonly EntityId Target;
        public readonly float Fraction;
        public readonly SimVec3 Point;
        public LineHit(EntityId target,float fraction,SimVec3 point){Target=target;Fraction=fraction;Point=point;}
    }
    public static class LineTargeting
    {
        public static LineHit[] Query(CombatWorld world,Actor source,SimVec3 from,SimVec3 to,float radius,int mask=0)
        {
            var center=CollisionGeometry.Lerp(from,to,.5f);
            float length=(float)Math.Sqrt(SpatialGeometry.DistanceSquared(from,to));
            // This is only the broad phase. Include roundoff at segment endpoints;
            // CircleEntry below remains the authority for actual contact.
            var candidates=AreaTargets.Query(world,center,length*.5f+radius+.0001f,source,mask,0);
            var hits=new List<LineHit>();
            foreach(var id in candidates)
            {
                if(!AreaTargets.TryLive(world,id,out var actor))continue;
                if(CollisionGeometry.CircleEntry(from,to,actor.GetComp<TransformComp>().Position,radius,out float fraction))
                    hits.Add(new LineHit(id,fraction,CollisionGeometry.Lerp(from,to,fraction)));
            }
            hits.Sort((a,b)=>{int order=a.Fraction.CompareTo(b.Fraction);if(order!=0)return order;order=a.Target.Index.CompareTo(b.Target.Index);return order!=0?order:a.Target.Generation.CompareTo(b.Target.Generation);});
            return hits.ToArray();
        }
    }
}
