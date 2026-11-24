using Combat.Config;
using UnityEngine;
using System.Collections.Generic;

namespace Combat.Unity
{
    static class SkillVolumeGeometry
    {
        public static Vector3[] Points(SkillSpatialSnapshot volume)
        {
            var points=new List<Vector3>();float length=volume.Length,width=volume.Width*.5f;
            if(volume.Shape=="thrust")return new[]{new Vector3(0,0,-width),new Vector3(length,0,-width),new Vector3(length,0,width),new Vector3(0,0,width),new Vector3(0,0,-width)};
            if(volume.Shape=="capsule")
            {
                for(int i=0;i<=16;i++){float angle=(-90+i*180f/16)*Mathf.Deg2Rad;points.Add(new Vector3(length+Mathf.Cos(angle)*width,0,Mathf.Sin(angle)*width));}
                for(int i=0;i<=16;i++){float angle=(90+i*180f/16)*Mathf.Deg2Rad;points.Add(new Vector3(Mathf.Cos(angle)*width,0,Mathf.Sin(angle)*width));}
                points.Add(points[0]);return points.ToArray();
            }
            float arc=volume.Shape=="circle"?360:volume.Angle;
            if(arc<360)points.Add(Vector3.zero);
            for(int i=0;i<=64;i++){float angle=(-arc*.5f+i*arc/64)*Mathf.Deg2Rad;points.Add(new Vector3(Mathf.Cos(angle)*length,0,Mathf.Sin(angle)*length));}
            if(arc<360)points.Add(Vector3.zero);
            return points.ToArray();
        }
    }
}
