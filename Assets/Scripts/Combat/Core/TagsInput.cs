using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class TagComp : Comp
    {
        readonly Dictionary<int, int> _stacks = new Dictionary<int, int>(16);
        readonly Dictionary<int,int> _leasedStacks = new Dictionary<int,int>();
        readonly Dictionary<int,(TagId Tag,int Count)> _leases = new Dictionary<int,(TagId,int)>();
        int _nextLease;

        public bool Has(TagId tag)
            => Stack(tag)>0;

        public int Stack(TagId tag)
            => (_stacks.TryGetValue(tag.Value, out var n) ? n : 0)+(_leasedStacks.TryGetValue(tag.Value,out var leases)?leases:0);

        public void Add(TagId tag, int stacks, TagSource source)
        {
            if (stacks <= 0) return;
            if (_stacks.TryGetValue(tag.Value, out var cur))
                _stacks[tag.Value] = cur + stacks;
            else
                _stacks[tag.Value] = stacks;
            _ = source;
        }

        public void Remove(TagId tag, int stacks, TagSource source)
        {
            if (stacks <= 0) return;
            if (!_stacks.TryGetValue(tag.Value, out var cur)) return;
            cur -= stacks;
            if (cur <= 0) _stacks.Remove(tag.Value);
            else _stacks[tag.Value] = cur;
            _ = source;
        }

        public TagLease Acquire(TagId tag,int count=1)
        {
            if(count<=0)throw new System.ArgumentOutOfRangeException(nameof(count));
            int id=++_nextLease;_leases.Add(id,(tag,count));
            _leasedStacks[tag.Value]=(_leasedStacks.TryGetValue(tag.Value,out int current)?current:0)+count;
            return new TagLease(this,id);
        }
        internal void ReleaseLease(int id)
        {
            if(!_leases.TryGetValue(id,out var lease))return;_leases.Remove(id);
            int left=_leasedStacks[lease.Tag.Value]-lease.Count;
            if(left<=0)_leasedStacks.Remove(lease.Tag.Value);else _leasedStacks[lease.Tag.Value]=left;
        }
        public Dictionary<int,int> Snapshot()
        {var values=new Dictionary<int,int>(_stacks);foreach(var item in _leasedStacks)values[item.Key]=(values.TryGetValue(item.Key,out int count)?count:0)+item.Value;return values;}
        public void ClearAll(){_stacks.Clear();_leases.Clear();_leasedStacks.Clear();}

        protected override void OnDetach() => ClearAll();
    }

    public sealed class InputBufferComp : Comp
    {
        CombatTime _time;
        InputToken _buffered;
        bool _hasBuffered;
        float _bufferedLogicalTime;
        float _bufferWindow = 0.2f;

        public float LastPushTime { get; private set; }
        public bool HasBuffered => _hasBuffered && IsValidNow();

        public void SetBufferWindow(float seconds)
            => _bufferWindow = seconds > 0f ? seconds : 0.01f;

        protected override void OnAttach()
        {
            _time = Self.World.Time;
        }

        protected override void OnDetach()
        {
            ClearBufferedOnly();
            _time = null;
        }

        public void Push(in InputToken token)
        {
            if (!token.IsValid || _time == null) return;
            _buffered = token;
            _hasBuffered = true;
            _bufferedLogicalTime = _time.Time;
            LastPushTime = _time.Time;
        }

        public bool TryPeek(out InputToken token)
        {
            if (!TryGetValid(out token)) return false;
            return true;
        }

        public bool Consume()
        {
            if (!TryGetValid(out _)) return false;
            ClearBufferedOnly();
            return true;
        }

        public void Clear() => ClearBufferedOnly();

        void ClearBufferedOnly()
        {
            _hasBuffered = false;
            _buffered = default;
            _bufferedLogicalTime = 0f;
        }

        bool IsValidNow()
        {
            if (!_hasBuffered || _time == null) return false;
            return (_time.Time - _bufferedLogicalTime) <= _bufferWindow;
        }

        bool TryGetValid(out InputToken token)
        {
            token = default;
            if (!_hasBuffered) return false;
            if (_time != null && _time.Time - _bufferedLogicalTime > _bufferWindow)
            {
                ClearBufferedOnly();
                return false;
            }

            token = _buffered;
            return true;
        }
    }
}
