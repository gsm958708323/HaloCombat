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
        public static LineHit[] Query(CombatWorld world,Actor source,SimVec3 from,SimVec3 to,float radius,int mask=0,bool includeTargetRadius=false)
        {
            var hits=new List<LineHit>();
            foreach(var actor in world.QueryActors())
            {
                if(actor==source||!SimpleTargetQuery.PassesFilter(source,actor,mask)||!actor.TryGetComp<TransformComp>(out var transform))continue;
                float targetRadius=includeTargetRadius&&actor.TryGetComp<LocomotionComp>(out var body)?body.CollisionRadius:0;
                if(CollisionGeometry.CircleEntry(from,to,transform.Position,radius+Math.Max(0,targetRadius),out float fraction))
                    hits.Add(new LineHit(actor.Id,fraction,CollisionGeometry.Lerp(from,to,fraction)));
            }
            hits.Sort((a,b)=>{int order=a.Fraction.CompareTo(b.Fraction);if(order!=0)return order;order=a.Target.Index.CompareTo(b.Target.Index);return order!=0?order:a.Target.Generation.CompareTo(b.Target.Generation);});
            return hits.ToArray();
        }
    }
}
