using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public static class ArenaInputSource
    {
        public static ArenaInputFrame Sample(Camera camera,Vector3 origin)
        {
            var keys=new List<string>();
            if(Input.GetKeyDown(KeyCode.Space))keys.Add("Roll");
            if(Input.GetKeyDown(KeyCode.Q))keys.Add("Homing");
            if(Input.GetKeyDown(KeyCode.E))keys.Add("Monkey");
            KeyCode[] codes={KeyCode.Alpha1,KeyCode.Alpha2,KeyCode.Alpha3,KeyCode.Alpha4,KeyCode.Alpha5};
            for(int i=0;i<codes.Length;i++)if(Input.GetKeyDown(codes[i])||Input.GetMouseButtonDown(i))keys.Add("Fire"+(i+1));
            var frame=new ArenaInputFrame{MoveX=Input.GetAxisRaw("Horizontal"),MoveZ=Input.GetAxisRaw("Vertical"),PrimaryHeld=Input.GetKey(KeyCode.Alpha1)||Input.GetMouseButton(0),Pressed=keys.ToArray()};
            if(camera!=null)
            {
                var ray=camera.ScreenPointToRay(Input.mousePosition);
                if(ray.direction.y<-.05f&&new Plane(Vector3.up,new Vector3(0,origin.y,0)).Raycast(ray,out float distance)&&distance<200)
                {var point=ray.GetPoint(distance);var delta=point-origin;if(delta.x*delta.x+delta.z*delta.z>.01f){frame.AimValid=true;frame.Aim=new SkillAim(EntityId.Invalid,new SimVec3(point.x,origin.y,point.z),LocomotionComp.YawFromStick(new SimVec3(delta.x,0,delta.z)));}}
            }
            return frame;
        }
    }
}
