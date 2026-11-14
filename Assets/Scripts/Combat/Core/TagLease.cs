using System;

namespace Combat.Core
{
    public sealed class TagLease : IDisposable
    {
        TagComp _owner;
        readonly int _id;
        internal TagLease(TagComp owner,int id){_owner=owner;_id=id;}
        public void Dispose(){var owner=_owner;_owner=null;owner?.ReleaseLease(_id);}
    }
}
