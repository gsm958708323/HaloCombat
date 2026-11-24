using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum HitShape { Thrust, Fan, Circle, Capsule }

    public static class SpatialGeometry
    {
        public static float DistanceSquared(SimVec3 a, SimVec3 b) => (a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z);

        public static EntityId[] Query(CombatWorld world, Actor source, SimVec3 origin, float yaw, HitShape shape, float length, float width, float angle)
        {
            var forward = LocomotionComp.ForwardFromYaw(yaw);
            float radius = shape == HitShape.Thrust || shape == HitShape.Capsule ? length + width : length;
            var candidates = AreaTargets.Query(world, origin, radius, source, 0, 0);
            var results = new List<EntityId>();
            foreach (var id in candidates)
            {
                if (!AreaTargets.TryLive(world, id, out var target)) continue;
                var point = target.GetComp<TransformComp>().Position;
                if(world.Collision.Trace(origin,point).Blocked)continue;
                float x = point.X-origin.X, z = point.Z-origin.Z;
                float along = x*forward.X+z*forward.Z;
                float across = -x*forward.Z+z*forward.X;
                bool hit = false;
                switch(shape)
                {
                    case HitShape.Circle: hit = x*x+z*z <= length*length+.00001f; break;
                    case HitShape.Fan:
                        hit = x*x+z*z <= length*length+.00001f && (angle >= 360 || along >= Math.Sqrt(x*x+z*z)*Math.Cos(angle*Math.PI/360)-.00001); break;
                    case HitShape.Thrust: hit = along >= -.00001f && along <= length+.00001f && Math.Abs(across) <= width*.5f+.00001f; break;
                    case HitShape.Capsule:
                        float nearest = Math.Max(0, Math.Min(length, along));
                        hit = (along-nearest)*(along-nearest)+across*across <= width*width*.25f+.00001f; break;
                }
                if(hit)results.Add(id);
            }
            return results.ToArray();
        }
    }
}
