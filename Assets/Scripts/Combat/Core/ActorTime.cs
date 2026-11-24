using System;

namespace Combat.Core
{
    public sealed class ActorTime
    {
        int _pending,_remaining;
        public double Time {get;private set;}
        public float Delta {get;private set;}
        public bool IsStopped {get;private set;}
        public int HitstopLeft=>Math.Max(_pending,_remaining);
        internal void RequestHitstop(int frames)=>_pending=Math.Max(_pending,frames);
        internal void BeginStep(float dt)
        {
            _remaining=Math.Max(_remaining,_pending);_pending=0;IsStopped=_remaining>0;
            Delta=IsStopped?0:Math.Max(0,dt);Time+=Delta;if(IsStopped)_remaining--;
        }
        internal void Reset(){_pending=_remaining=0;Time=0;Delta=0;IsStopped=false;}
    }
}
