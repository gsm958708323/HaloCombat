using Combat.Config;
using UnityEngine;

namespace Combat.Unity
{
    internal static class SkillLabSweepVisual
    {
        public static void Update(GameObject root,SkillSpatialSnapshot volume,Material material)
        {
            if(volume.Shape!="fan"&&volume.Shape!="circle")return;
            var child=root.transform.Find("SweepEdge");
            if(child==null)
            {
                var blade=new GameObject("SweepEdge");blade.transform.SetParent(root.transform,false);child=blade.transform;
                var edge=blade.AddComponent<LineRenderer>();edge.useWorldSpace=false;edge.widthMultiplier=.14f;edge.sharedMaterial=material;edge.positionCount=2;
                var trail=new GameObject("SweepTrail");trail.transform.SetParent(child,false);var arc=trail.AddComponent<LineRenderer>();arc.useWorldSpace=false;arc.widthMultiplier=.09f;arc.sharedMaterial=material;arc.positionCount=13;
            }
            float angle=Mathf.Min(360,volume.Angle),end=-angle/2+Mathf.Clamp01(volume.Progress)*angle;
            float start=Mathf.Max(-angle/2,end-35),r=volume.Length;
            var line=child.GetComponent<LineRenderer>();line.SetPosition(0,Point(end,r*.1f));line.SetPosition(1,Point(end,r));
            var arcLine=child.GetChild(0).GetComponent<LineRenderer>();
            for(int i=0;i<13;i++)arcLine.SetPosition(i,Point(Mathf.Lerp(start,end,i/12f),r));
        }
        static Vector3 Point(float angle,float radius)=>new Vector3(Mathf.Cos(angle*Mathf.Deg2Rad)*radius,.06f,Mathf.Sin(angle*Mathf.Deg2Rad)*radius);
    }
}
