using System;
using Combat.Core;

namespace Combat.Presentation
{
    // Presentation time follows the rendered frame, including while combat is paused
    // or stopped. It never changes the actor pose or any gameplay clock.
    public sealed class ArenaCameraMotion
    {
        readonly float _standing,_lowered,_springHz;
        bool _initialized;
        public SimVec3 Focus {get;private set;}
        public float Impulse {get;private set;}
        public ArenaCameraMotion(float standing,float lowered,float springHz)
        {_standing=standing;_lowered=lowered;_springHz=springHz;}
        public void NotifyHit(HitFeedbackLevel level)
        {
            float value=level==HitFeedbackLevel.Finisher?.8f:level==HitFeedbackLevel.Heavy?.55f:level==HitFeedbackLevel.ComboConfirm?.35f:level==HitFeedbackLevel.Light?.2f:0;
            Impulse=Math.Max(Impulse,value);
        }
        public void Sample(SimVec3 position,bool lowered,float deltaTime)
        {
            float dt=Math.Max(0,deltaTime),height=lowered?_lowered:_standing;
            var target=new SimVec3(position.X,position.Y+height,position.Z);
            if(!_initialized||_springHz<=0){Focus=target;_initialized=true;}
            else{
                float k=1-(float)Math.Exp(-_springHz*dt);var p=Focus;
                Focus=new SimVec3(p.X+(target.X-p.X)*k,p.Y+(target.Y-p.Y)*k,p.Z+(target.Z-p.Z)*k);
            }
            Impulse=Math.Max(0,Impulse-dt);
        }
    }
}
