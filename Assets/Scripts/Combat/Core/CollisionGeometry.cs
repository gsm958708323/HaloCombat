using System;

namespace Combat.Core
{
    public readonly struct ObstacleDefinition
    {
        public readonly string Id;
        public readonly float X, Z, Width, Depth;
        public ObstacleDefinition(string id, float x, float z, float width, float depth)
        { Id = id; X = x; Z = z; Width = width; Depth = depth; }
    }

    public readonly struct ObstacleHit
    {
        public readonly string Id;
        public readonly float Fraction;
        public readonly SimVec3 Point, Normal;
        public bool Blocked => Id != null;
        public ObstacleHit(string id, float fraction, SimVec3 point, SimVec3 normal)
        { Id = id; Fraction = fraction; Point = point; Normal = normal; }
    }

    internal static class CollisionGeometry
    {
        public static SimVec3 Lerp(SimVec3 from, SimVec3 to, float t)
            => new SimVec3(from.X+(to.X-from.X)*t, from.Y+(to.Y-from.Y)*t, from.Z+(to.Z-from.Z)*t);

        public static bool Overlaps(SimVec3 point, float radius, in ObstacleDefinition box)
        {
            float x = Math.Max(box.X-box.Width*.5f, Math.Min(box.X+box.Width*.5f, point.X));
            float z = Math.Max(box.Z-box.Depth*.5f, Math.Min(box.Z+box.Depth*.5f, point.Z));
            if (radius <= 0) return point.X > box.X-box.Width*.5f && point.X < box.X+box.Width*.5f
                && point.Z > box.Z-box.Depth*.5f && point.Z < box.Z+box.Depth*.5f;
            return (point.X-x)*(point.X-x)+(point.Z-z)*(point.Z-z) < radius*radius-.000001f;
        }

        public static bool CircleEntry(SimVec3 from, SimVec3 to, SimVec3 center, float radius, out float fraction)
        {
            double dx=to.X-from.X, dz=to.Z-from.Z, x=from.X-center.X, z=from.Z-center.Z;
            double a=dx*dx+dz*dz, c=x*x+z*z-radius*radius;
            fraction=0;
            if (c <= .00000001) return true;
            if (a <= .000000000001) return false;
            double b=x*dx+z*dz, discriminant=b*b-a*c;
            if (discriminant < 0) return false;
            double t=(-b-Math.Sqrt(discriminant))/a;
            if (t < -.0000001 || t > 1.0000001) return false;
            fraction=(float)Math.Max(0,Math.Min(1,t)); return true;
        }

        public static ObstacleHit Sweep(SimVec3 from, SimVec3 to, float radius, in ObstacleDefinition box)
        {
            float minX=box.X-box.Width*.5f,maxX=box.X+box.Width*.5f,minZ=box.Z-box.Depth*.5f,maxZ=box.Z+box.Depth*.5f;
            float dx=to.X-from.X,dz=to.Z-from.Z,best=float.PositiveInfinity;SimVec3 normal=default;
            if (Overlaps(from,radius,box))
                return new ObstacleHit(box.Id,0,from,default);
            void Face(float t, float coordinate, float minimum, float maximum, float nx, float nz)
            {
                if(t<-.000001f||t>1.000001f||coordinate<minimum-.000001f||coordinate>maximum+.000001f||t>=best)return;
                if(dx*nx+dz*nz>=-.0000001f)return;
                best=Math.Max(0,Math.Min(1,t));normal=new SimVec3(nx,0,nz);
            }
            if(Math.Abs(dx)>.0000001f)
            {
                float t=(minX-radius-from.X)/dx;Face(t,from.Z+dz*t,minZ,maxZ,-1,0);
                t=(maxX+radius-from.X)/dx;Face(t,from.Z+dz*t,minZ,maxZ,1,0);
            }
            if(Math.Abs(dz)>.0000001f)
            {
                float t=(minZ-radius-from.Z)/dz;Face(t,from.X+dx*t,minX,maxX,0,-1);
                t=(maxZ+radius-from.Z)/dz;Face(t,from.X+dx*t,minX,maxX,0,1);
            }
            if(radius>0)
                foreach(float x in new[]{minX,maxX})foreach(float z in new[]{minZ,maxZ})
                {
                    var corner=new SimVec3(x,from.Y,z);
                    if(!CircleEntry(from,to,corner,radius,out float t)||t>=best)continue;
                    var point=Lerp(from,to,t);float nx=(point.X-x)/radius,nz=(point.Z-z)/radius;
                    if(dx*nx+dz*nz>=-.0000001f)continue;
                    best=t;normal=new SimVec3(nx,0,nz);
                }
            return float.IsPositiveInfinity(best)?default:new ObstacleHit(box.Id,best,Lerp(from,to,best),normal);
        }
    }
}
