using System.Collections.Generic;
using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    internal sealed class ArenaInputSource
    {
        readonly CombatControls.Sampler _skills = new CombatControls.Sampler();
        readonly List<string> _pressed = new List<string>(CombatControls.Count);

        public void Cancel() => _skills.Cancel();

        public ArenaInputFrame Sample(Camera camera, Vector3 origin, bool pointerOverHud)
        {
            _pressed.Clear();
            bool primaryHeld = false;
            for (int i = 0; i < CombatControls.Count; i++)
            {
                var input = _skills.Read(i, !pointerOverHud);
                if (input.Pressed) _pressed.Add(CombatControls.At(i).Action);
                if (i == 0) primaryHeld = input.Held;
            }
            var frame = new ArenaInputFrame
            {
                MoveX = Input.GetAxisRaw("Horizontal"), MoveZ = Input.GetAxisRaw("Vertical"),
                PrimaryHeld = primaryHeld, Pressed = _pressed.Count == 0 ? System.Array.Empty<string>() : _pressed.ToArray()
            };
            if(camera!=null&&!pointerOverHud&&camera.pixelRect.Contains(Input.mousePosition))
            {
                var ray=camera.ScreenPointToRay(Input.mousePosition);
                if(ray.direction.y<-.05f&&new Plane(Vector3.up,new Vector3(0,origin.y,0)).Raycast(ray,out float distance)&&distance<200)
                {var point=ray.GetPoint(distance);var delta=point-origin;if(delta.x*delta.x+delta.z*delta.z>.01f){frame.AimValid=true;frame.Aim=new SkillAim(EntityId.Invalid,new SimVec3(point.x,origin.y,point.z),LocomotionComp.YawFromStick(new SimVec3(delta.x,0,delta.z)));}}
            }
            return frame;
        }
    }
}
