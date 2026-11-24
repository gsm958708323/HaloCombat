using System;

namespace Combat.Core
{
    // Concurrent movement and attack each advance once, in the authored child order.
    public sealed class BtParallel : BtNode
    {
        readonly BtNode[] _children;
        public BtParallel(params BtNode[] children)=>_children=children??Array.Empty<BtNode>();
        public override BtStatus Tick(in BtTick context)
        {
            bool running=false,failed=false;
            foreach(var child in _children){var status=child.Tick(context);running|=status==BtStatus.Running;failed|=status==BtStatus.Failure;}
            return running?BtStatus.Running:failed?BtStatus.Failure:BtStatus.Success;
        }
        public override void Abort(in BtTick context){foreach(var child in _children)child.Abort(context);}
        public override BtNode Clone()
        {var children=new BtNode[_children.Length];for(int i=0;i<children.Length;i++)children[i]=_children[i].Clone();return new BtParallel(children);}
    }
}
