using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class CollisionWorld
    {
        readonly ObstacleDefinition[] _obstacles;
        public IReadOnlyList<ObstacleDefinition> Obstacles { get; }
        public CollisionWorld(ObstacleDefinition[] obstacles = null)
        {
            _obstacles=obstacles == null ? Array.Empty<ObstacleDefinition>() : (ObstacleDefinition[])obstacles.Clone();
            var ids=new HashSet<string>();
            foreach(var box in _obstacles)
                if(string.IsNullOrWhiteSpace(box.Id)||!ids.Add(box.Id)||!Finite(box.X)||!Finite(box.Z)||!Finite(box.Width)||!Finite(box.Depth)||box.Width<=0||box.Depth<=0)
                    throw new ArgumentException("Obstacle requires a unique id and finite positive geometry.");
            Obstacles=Array.AsReadOnly(_obstacles);
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public bool IsFree(SimVec3 point,float radius)
        {
            if(!Finite(point.X)||!Finite(point.Y)||!Finite(point.Z)||!Finite(radius)||radius<0)return false;
            foreach(var box in _obstacles)if(CollisionGeometry.Overlaps(point,radius,box))return false;
            return true;
        }
        public ObstacleHit Trace(SimVec3 from,SimVec3 to,float radius=0)
        {
            ObstacleHit result=default;
            foreach(var box in _obstacles)
            {
                var hit=CollisionGeometry.Sweep(from,to,radius,box);
                if(hit.Blocked&&(!result.Blocked||hit.Fraction<result.Fraction))result=hit;
            }
            return result;
        }
        public SimVec3 Constrain(SimVec3 from,SimVec3 to,float radius,out ObstacleHit hit)
        {
            hit=Trace(from,to,radius);
            if(!hit.Blocked)return to;
            float distance=(float)Math.Sqrt(SpatialGeometry.DistanceSquared(from,to));
            float fraction=Math.Max(0,hit.Fraction-(distance>0?.0001f/distance:0));
            var point=CollisionGeometry.Lerp(from,to,fraction);
            return new SimVec3(point.X,to.Y,point.Z);
        }
    }
}
