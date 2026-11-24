using System;

namespace Combat.Core
{
    internal static class ActorBodyCollision
    {
        public static SimVec3 ConstrainDash(Actor self,SimVec3 from,SimVec3 to,float radius,out ObstacleHit contact)
        {
            contact=default;float length=(float)Math.Sqrt(SpatialGeometry.DistanceSquared(from,to));
            if(length<=.000001f)return to;
            foreach(var target in self.World.QueryActors())
            {
                if(target==self||!target.IsActive||!target.TryGetComp<LocomotionComp>(out var body)||
                    !target.TryGetComp<AttributeSet>(out var attributes)||!attributes.IsAlive())continue;
                var center=target.GetComp<TransformComp>().Position;float combined=radius+body.CollisionRadius;
                float before=SpatialGeometry.DistanceSquared(from,center),after=SpatialGeometry.DistanceSquared(to,center);
                // Existing overlap must allow an actor to leave instead of locking it in place.
                if(before<combined*combined&&after>=before)continue;
                if(!CollisionGeometry.CircleEntry(from,to,center,combined,out float fraction))continue;
                if(contact.Blocked&&fraction>=contact.Fraction)continue;
                var point=CollisionGeometry.Lerp(from,to,fraction);float dx=point.X-center.X,dz=point.Z-center.Z;
                float distance=(float)Math.Sqrt(dx*dx+dz*dz);
                var normal=distance>.000001f?new SimVec3(dx/distance,0,dz/distance):new SimVec3(-(to.X-from.X)/length,0,-(to.Z-from.Z)/length);
                contact=new ObstacleHit("actor:"+target.Id.Index+":"+target.Id.Generation,fraction,point,normal);
            }
            if(!contact.Blocked)return to;
            return CollisionGeometry.Lerp(from,to,Math.Max(0,contact.Fraction-.0001f/length));
        }
    }
}
