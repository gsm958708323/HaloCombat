using System;
using Combat.Core;

namespace Combat.Presentation
{
    public sealed class HitFeedbackState
    {
        public float Flash {get;private set;}
        public float Impulse {get;private set;}
        public HitFeedbackLevel Level {get;private set;}
        public int Sequence {get;private set;}
        public void Receive(HitFeedbackLevel level)
        {
            Level=level;Sequence++;Flash=1;
            float impulse=level==HitFeedbackLevel.Light?.35f:level==HitFeedbackLevel.ComboConfirm?.65f:level==HitFeedbackLevel.Heavy?.9f:level==HitFeedbackLevel.Finisher?1.2f:0;
            Impulse=Math.Max(Impulse,impulse);
        }
        public void AdvanceWall(float dt){dt=Math.Max(0,dt);Flash=Math.Max(0,Flash-dt/.18f);Impulse=Math.Max(0,Impulse-dt*3.5f);}
        public void Reset(){Flash=Impulse=0;Level=HitFeedbackLevel.None;Sequence=0;}
    }
}
