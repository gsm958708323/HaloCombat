using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public readonly struct TagLease
    {
        internal readonly TagComp Owner;
        internal readonly long Id;
        internal TagLease(TagComp owner, long id) { Owner = owner; Id = id; }
        public void Release() => Owner?.Release(this);
    }

    public sealed class TagComp : Comp
    {
        readonly Dictionary<int, int> _stacks = new Dictionary<int, int>(16);
        readonly Dictionary<int, int> _leased = new Dictionary<int, int>(16);
        readonly Dictionary<long, TagId[]> _leases = new Dictionary<long, TagId[]>();
        long _nextLease;
        public bool Has(TagId tag) => Stack(tag) > 0;
        int Raw(TagId tag) => Count(_stacks, tag) + Count(_leased, tag);
        public int Stack(TagId tag)
        {
            int n = Raw(tag);
            if (tag == CommonTags.BlockMove || tag == CommonTags.BlockRotate ||
                tag == CommonTags.BlockSkill || tag == CommonTags.BlockSkillMotion)
                n += Raw(CommonTags.Stunned) + Raw(CommonTags.Downed) + Raw(CommonTags.Dead);
            if (tag == CommonTags.BlockSkill) n += Raw(CommonTags.Silence);
            if (tag == CommonTags.BlockHitMotion || tag == CommonTags.BlockGravity)
                n += Raw(CommonTags.Dead);
            return n;
        }
        static int Count(Dictionary<int, int> map, TagId tag)
            => map.TryGetValue(tag.Value, out var n) ? n : 0;
        static void Change(Dictionary<int, int> map, TagId tag, int delta)
        {
            int n = Count(map, tag) + delta;
            if (n <= 0) map.Remove(tag.Value); else map[tag.Value] = n;
        }
        public void Add(TagId tag, int stacks, TagSource source)
        {
            if (stacks > 0) Change(_stacks, tag, stacks);
        }
        public void Remove(TagId tag, int stacks, TagSource source)
        {
            int remove = Math.Min(Math.Max(0, stacks), Count(_stacks, tag));
            if (remove > 0) Change(_stacks, tag, -remove);
        }
        public TagLease Acquire(params TagId[] tags)
        {
            var copy = tags == null ? Array.Empty<TagId>() : (TagId[])tags.Clone();
            long id = ++_nextLease;
            _leases.Add(id, copy);
            foreach (var tag in copy) Change(_leased, tag, 1);
            return new TagLease(this, id);
        }
        public void Release(TagLease lease)
        {
            if (lease.Owner != this || !_leases.TryGetValue(lease.Id, out var tags)) return;
            _leases.Remove(lease.Id);
            foreach (var tag in tags) Change(_leased, tag, -1);
        }
        public void ClearAll() { _stacks.Clear(); _leased.Clear(); _leases.Clear(); }
        protected override void OnDetach() => ClearAll();
    }

    /// <summary>Three pending presses, FIFO. Held fire never occupies this queue.</summary>
    public sealed class InputBufferComp : Comp
    {
        public const int Capacity = 3;
        readonly Queue<Entry> _queue = new Queue<Entry>(Capacity);
        float _bufferWindow = .8f;
        struct Entry { public InputToken Token; public float Time; }
        public float LastPushTime { get; private set; }
        public bool HasBuffered { get { Expire(); return _queue.Count > 0; } }
        public int Count { get { Expire(); return _queue.Count; } }
        public bool PrimaryHeld { get; set; }
        public void SetBufferWindow(float seconds) => _bufferWindow = Math.Max(.01f, seconds);
        public void Push(in InputToken token)
        {
            if (!token.IsValid || Self == null) return;
            Expire();
            if (_queue.Count == Capacity) _queue.Dequeue();
            LastPushTime = Self.Time.Time;
            _queue.Enqueue(new Entry { Token = token, Time = LastPushTime });
        }
        public bool TryPeek(out InputToken token)
        {
            Expire();
            token = _queue.Count > 0 ? _queue.Peek().Token : default;
            return _queue.Count > 0;
        }
        public bool Consume()
        {
            Expire();
            if (_queue.Count == 0) return false;
            _queue.Dequeue();
            return true;
        }
        void Expire()
        {
            if (Self == null) return;
            while (_queue.Count > 0 && Self.Time.Time - _queue.Peek().Time > _bufferWindow)
                _queue.Dequeue();
        }
        public void Clear() { _queue.Clear(); PrimaryHeld = false; }
        protected override void OnDetach() { Clear(); LastPushTime = 0f; }
    }
}
