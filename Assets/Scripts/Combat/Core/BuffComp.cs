using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class BuffComp : Comp
    {
        public IReadOnlyDictionary<BuffTypeId, BuffStack> AllBuffs => _buffs;
        readonly Dictionary<BuffTypeId, BuffStack> _buffs = new Dictionary<BuffTypeId, BuffStack>();
        readonly List<BuffTypeId> _removeQueue = new List<BuffTypeId>();
        readonly Dictionary<BuffTypeId, List<BuffHook>> _activeHooks = new Dictionary<BuffTypeId, List<BuffHook>>();
        public int Revision { get; private set; } // 脏标记
        // 注册 Hook（Demo 用，商用可从 BuffDefSO 自动加载）
        public void RegisterHook(BuffTypeId type, BuffHook hook)
        {
            if (!_activeHooks.TryGetValue(type, out var list))
            {
                list = new List<BuffHook>();
                _activeHooks[type] = list;
            }
            if (!list.Contains(hook))
                list.Add(hook);
        }
        public void Apply(in BuffApplyArgs args)
        {
            var type = args.Type;
            if (!_buffs.TryGetValue(type, out var existing))
                existing = new BuffStack(type, 0, 0f, 0f, args.Source);
            int newCount = args.RefreshIfExist ? args.Stacks : existing.Count + args.Stacks;
            if (newCount <= 0)
            {
                _removeQueue.Add(type);
                return;
            }
            float newDur = args.Duration;
            if (args.RefreshIfExist && existing.Count > 0)
                newDur = existing.TotalDuration;
            var newStack = new BuffStack(type, newCount, newDur, existing.TotalDuration, args.Source);
            _buffs[type] = newStack;
            Revision++;
            // 触发 Hook
            if (_activeHooks.TryGetValue(type, out var hooks))
            {
                var data = new BuffHookData
                {
                    Target = Self.Id,
                    Type = type,
                    Stacks = newCount,
                    DurationLeft = newDur,
                    State = Self.TryGetComp<StateMachineComp>(out var s) ? s : null,
                    Director = Self.TryGetComp<SkillDirectorComp>(out var d) ? d : null,
                    Tags = Self.TryGetComp<TagComp>(out var t) ? t : null,
                    Attr = Self.TryGetComp<AttrSheet>(out var a) ? a : null
                };
                foreach (var h in hooks)
                    h.Callback?.Invoke(data);
            }
            NotifyAttrChanged();
        }
        public void Remove(BuffTypeId type, TagSource source)
        {
            if (_buffs.Remove(type))
            {
                _removeQueue.Add(type);
                Revision++;
                NotifyAttrChanged();
            }
        }
        void NotifyAttrChanged()
        {
            Self.TryGetComp<AttrSheet>(out var AttrSheet);
            AttrSheet?.MarkDirty();
        }

        public override void Tick(float dt)
        {
            _removeQueue.Clear();
            foreach (var kv in _buffs)
            {
                var stack = kv.Value;
                stack = new BuffStack(stack.Type, stack.Count, stack.DurationLeft - dt, stack.TotalDuration, stack.Source);
                if (stack.DurationLeft <= 0f)
                    _removeQueue.Add(stack.Type);
                else
                    _buffs[kv.Key] = stack;
            }
            foreach (var t in _removeQueue)
                _buffs.Remove(t);
        }
        //     public override void Tick(float dt)
        //     {
        //         _removeQueue.Clear();
        //         _updateQueue.Clear();

        //         // Dictionary 只在遍历结束后写回，避免在 foreach 中修改集合。
        //         foreach (var kv in _buffs)
        //         {
        //             var stack = kv.Value;
        //             stack = new BuffStack(stack.Type, stack.Count, stack.DurationLeft - dt, stack.TotalDuration, stack.Source);
        //             if (stack.DurationLeft <= 0f)
        //                 _removeQueue.Add(stack.Type);
        //             else
        //                 _updateQueue.Add(new KeyValuePair<BuffTypeId, BuffStack>(kv.Key, stack));
        //         }

        //         for (int i = 0; i < _updateQueue.Count; i++)
        //         {
        //             var update = _updateQueue[i];
        //             _buffs[update.Key] = update.Value;
        //         }

        //         foreach (var t in _removeQueue)
        //             _buffs.Remove(t);
        //     }
        // }
    }
}
