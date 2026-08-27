using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum StackPolicy : byte
    {
        RefreshDuration = 0,
        AddStack = 1,
        Independent = 2,
        RejectIfExists = 3
    }

    public enum DispelMode : byte
    {
        ByBuffId = 0,
        ByMutexGroup = 1,
        ByTag = 2,
        BySource = 3
    }

    public sealed class DurationSpec
    {
        public int BuffId;
        public float Duration = 3f;
        public float TickInterval;
        public int MaxStacks = 1;
        public StackPolicy Stack = StackPolicy.RefreshDuration;
        public int MutexGroup;
        public Modifier[] Modifiers = Array.Empty<Modifier>();
        public TagId[] GrantedTags = Array.Empty<TagId>();
        public IEffect[] OnApply = Array.Empty<IEffect>();
        public IEffect[] OnStack = Array.Empty<IEffect>();
        public IEffect[] OnPeriod = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
        public IEffect[] OnHurted = Array.Empty<IEffect>();
        public IEffect[] OnOwnerCast = Array.Empty<IEffect>();
    }

    public sealed class BuffComp : Comp
    {
        public override bool WantsTick => false;

        struct Inst
        {
            public int InstanceId;
            public int BuffId;
            public int MutexGroup;
            public int Stacks;
            public int AppliedByPacked;
            public int BornFrame;
            public float ExpireTime;
            public float PeriodAcc;
            public DurationSpec Spec;
            public Actor Source;
        }

        readonly List<Inst> _list = new List<Inst>(8);
        readonly List<Inst> _snapshot = new List<Inst>(8);
        int _nextLocal;
        AttributeSet _attr;
        TagComp _tags;

        public int Count => _list.Count;

        public int StacksOf(int buffId)
        {
            int n = 0;
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].BuffId == buffId)
                    n += _list[i].Stacks;
            }

            return n;
        }

        protected override void OnAttach()
        {
            _attr = Self.GetComp<AttributeSet>();
            _tags = Self.GetComp<TagComp>();
        }

        protected override void OnDetach()
        {
            ClearAllSilent();
            _attr = null;
            _tags = null;
        }

        public bool Apply(DurationSpec spec, Actor source, int addStacks = 1)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (addStacks < 1) addStacks = 1;

            if (spec.MutexGroup > 0)
                RemoveMutexGroup(spec.MutexGroup, spec.BuffId);

            if (spec.Stack != StackPolicy.Independent)
            {
                int idx = IndexOfBuffId(spec.BuffId);
                if (idx >= 0)
                    return ApplyOnExisting(idx, spec, source, addStacks);
            }

            if (spec.Stack == StackPolicy.RejectIfExists && IndexOfBuffId(spec.BuffId) >= 0)
                return false;

            return SpawnNew(spec, source, addStacks);
        }

        public void Tick(float dt)
        {
            if (Self.World == null) return;
            int frame = Self.World.Time.Frame;
            float now = Self.World.Time.Time;

            for (int i = _list.Count - 1; i >= 0; i--)
            {
                var inst = _list[i];
                var spec = inst.Spec;
                if (spec.Duration > 0f && now >= inst.ExpireTime)
                {
                    RemoveAt(i, true);
                    continue;
                }

                if (spec.TickInterval <= 0f) continue;
                if (inst.BornFrame == frame) continue;

                inst.PeriodAcc += dt;
                while (inst.PeriodAcc >= spec.TickInterval)
                {
                    inst.PeriodAcc -= spec.TickInterval;
                    _list[i] = inst;
                    Dispatch(spec.OnPeriod, inst.Source, Self, inst.Stacks);
                    if (i >= _list.Count || _list[i].InstanceId != inst.InstanceId)
                        break;
                    inst = _list[i];
                }

                if (i < _list.Count && _list[i].InstanceId == inst.InstanceId)
                    _list[i] = inst;
            }
        }

        public bool RemoveInstance(int instanceId)
        {
            int idx = IndexOfInstance(instanceId);
            if (idx < 0) return false;
            RemoveAt(idx, true);
            return true;
        }

        public int Dispel(DispelMode mode, int key, TagId tag, int maxCount)
        {
            int removed = 0;
            int limit = maxCount <= 0 ? int.MaxValue : maxCount;
            for (int i = _list.Count - 1; i >= 0 && removed < limit; i--)
            {
                var inst = _list[i];
                bool hit = false;
                switch (mode)
                {
                    case DispelMode.ByBuffId: hit = inst.BuffId == key; break;
                    case DispelMode.ByMutexGroup: hit = inst.MutexGroup != 0 && inst.MutexGroup == key; break;
                    case DispelMode.ByTag: hit = Grants(inst.Spec, tag); break;
                    case DispelMode.BySource: hit = inst.AppliedByPacked == key; break;
                }

                if (!hit) continue;
                RemoveAt(i, true);
                removed++;
            }

            return removed;
        }

        public void ClearAllWithExpire()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
                RemoveAt(i, true);
        }

        public void DispatchOnHurted(Actor attacker)
        {
            if (Self.World == null) return;
            int frame = Self.World.Time.Frame;
            _snapshot.Clear();
            for (int i = 0; i < _list.Count; i++)
            {
                if (_list[i].BornFrame == frame) continue;
                var bag = _list[i].Spec.OnHurted;
                if (bag == null || bag.Length == 0) continue;
                _snapshot.Add(_list[i]);
            }

            for (int i = 0; i < _snapshot.Count; i++)
            {
                var inst = _snapshot[i];
                Dispatch(inst.Spec.OnHurted, attacker, Self, inst.Stacks);
            }
        }

        public void DispatchOnOwnerCast()
        {
            if (Self.World == null) return;
            _snapshot.Clear();
            for (int i = 0; i < _list.Count; i++)
            {
                var bag = _list[i].Spec.OnOwnerCast;
                if (bag == null || bag.Length == 0) continue;
                _snapshot.Add(_list[i]);
            }

            for (int i = 0; i < _snapshot.Count; i++)
            {
                var inst = _snapshot[i];
                Dispatch(inst.Spec.OnOwnerCast, Self, Self, inst.Stacks);
            }
        }

        void ClearAllSilent()
        {
            for (int i = _list.Count - 1; i >= 0; i--)
                RemoveAt(i, false);
        }

        bool ApplyOnExisting(int idx, DurationSpec spec, Actor source, int addStacks)
        {
            var inst = _list[idx];
            if (spec.Stack == StackPolicy.RejectIfExists)
                return false;

            if (spec.Stack == StackPolicy.RefreshDuration)
            {
                inst.ExpireTime = NextExpire(spec);
                inst.Source = source ?? inst.Source;
                _list[idx] = inst;
                return true;
            }

            if (spec.Stack == StackPolicy.AddStack)
            {
                int cap = spec.MaxStacks < 1 ? 1 : spec.MaxStacks;
                int old = inst.Stacks;
                int n = old + addStacks;
                if (n > cap) n = cap;
                inst.Stacks = n;
                inst.ExpireTime = NextExpire(spec);
                inst.Source = source ?? inst.Source;
                _list[idx] = inst;
                if (n > old)
                {
                    var bag = (spec.OnStack != null && spec.OnStack.Length > 0)
                        ? spec.OnStack
                        : spec.OnApply;
                    Dispatch(bag, inst.Source, Self, inst.Stacks);
                }

                return true;
            }

            return false;
        }

        bool SpawnNew(DurationSpec spec, Actor source, int stacks)
        {
            int cap = spec.MaxStacks < 1 ? 1 : spec.MaxStacks;
            if (stacks > cap) stacks = cap;
            int instanceId = Self.World != null ? Self.World.NextBuffInstanceId() : ++_nextLocal;
            var inst = new Inst
            {
                InstanceId = instanceId,
                BuffId = spec.BuffId,
                MutexGroup = spec.MutexGroup,
                Stacks = stacks,
                AppliedByPacked = Pack(source),
                BornFrame = Self.World != null ? Self.World.Time.Frame : 0,
                ExpireTime = NextExpire(spec),
                PeriodAcc = 0f,
                Spec = spec,
                Source = source
            };
            _list.Add(inst);
            AttachModsAndTags(inst);
            Dispatch(spec.OnApply, source, Self, inst.Stacks);
            return true;
        }

        void RemoveMutexGroup(int group, int keepBuffId)
        {
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                if (_list[i].MutexGroup == group && _list[i].BuffId != keepBuffId)
                    RemoveAt(i, true);
            }
        }

        void RemoveAt(int idx, bool fireExpire)
        {
            var inst = _list[idx];
            if (fireExpire)
                Dispatch(inst.Spec.OnExpire, inst.Source, Self, inst.Stacks);
            _attr.RemoveBySource(inst.InstanceId);
            DetachTags(inst);
            _list.RemoveAt(idx);
        }

        void AttachModsAndTags(in Inst inst)
        {
            var mods = inst.Spec.Modifiers;
            if (mods != null)
            {
                for (int i = 0; i < mods.Length; i++)
                {
                    var m = mods[i];
                    m.SourceId = inst.InstanceId;
                    _attr.AddMod(m);
                }
            }

            var tags = inst.Spec.GrantedTags;
            if (tags == null || _tags == null) return;
            for (int i = 0; i < tags.Length; i++)
                _tags.Add(tags[i], 1, TagSource.Effect("Buff.Grant"));
        }

        void DetachTags(in Inst inst)
        {
            var tags = inst.Spec.GrantedTags;
            if (tags == null || _tags == null) return;
            for (int i = 0; i < tags.Length; i++)
                _tags.Remove(tags[i], 1, TagSource.Effect("Buff.Ungrant"));
        }

        void Dispatch(IEffect[] bag, Actor source, Actor target, int stacks)
        {
            if (bag == null || bag.Length == 0 || Self.World == null) return;
            float atk = 0f;
            if (source != null && source.TryGetComp<AttributeSet>(out var srcAttr))
                atk = srcAttr.GetFinal(AttrId.Atk);
            Self.World.Deliver(bag, source, target, atk, null, null, stacks);
        }

        float NextExpire(DurationSpec spec)
        {
            if (spec.Duration <= 0f) return float.PositiveInfinity;
            float t = Self.World != null ? Self.World.Time.Time : 0f;
            return t + spec.Duration;
        }

        int IndexOfBuffId(int buffId)
        {
            for (int i = 0; i < _list.Count; i++)
                if (_list[i].BuffId == buffId) return i;
            return -1;
        }

        int IndexOfInstance(int instanceId)
        {
            for (int i = 0; i < _list.Count; i++)
                if (_list[i].InstanceId == instanceId) return i;
            return -1;
        }

        static bool Grants(DurationSpec spec, TagId tag)
        {
            var tags = spec.GrantedTags;
            if (tags == null) return false;
            for (int i = 0; i < tags.Length; i++)
                if (tags[i].Equals(tag)) return true;
            return false;
        }

        public static int Pack(Actor a)
        {
            if (a == null || !a.Id.IsValid) return 0;
            return unchecked(a.Id.Index * 397 ^ a.Id.Generation);
        }
    }
}
