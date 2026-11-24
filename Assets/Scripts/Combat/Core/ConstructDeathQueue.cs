using System.Collections.Generic;

namespace Combat.Core
{
    internal sealed class ConstructDeathQueue
    {
        readonly Queue<ConstructComp> _pending=new Queue<ConstructComp>();
        bool _draining;
        public void Enqueue(ConstructComp body)
        {
            _pending.Enqueue(body);if(_draining)return;_draining=true;int count=0;
            try{while(_pending.Count>0)_pending.Dequeue().CompleteDeath(++count<=256);}
            finally{_pending.Clear();_draining=false;}
        }
    }
}
