using Combat.Core;
using System.Collections.Generic;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public struct BuffVisualRule
    {
        public int BuffId,
            VisualId;
        public bool UseStacks;
    }

    public struct TagVisualRule
    {
        public TagId Tag;
        public int VisualId;
    }

    public sealed class BuffFxPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        readonly List<BuffVisualRule> _buffs = new List<BuffVisualRule>(4);
        readonly List<TagVisualRule> _tags = new List<TagVisualRule>(2);
        readonly Dictionary<int, int> _handles = new Dictionary<int, int>(8);
        readonly LoopVfxController _vfx;

        public BuffFxPresent(Transform vfxRoot)
        {
            _vfx = new LoopVfxController(vfxRoot);
            AddBuff(CombatIds.Burn, CombatIds.Burn, true);
            AddBuff(CombatIds.AuraSlow, CombatIds.AuraSlow, true);
        }

        public void AddBuff(int buffId, int visualId, bool useStacks = true) =>
            _buffs.Add(
                new BuffVisualRule
                {
                    BuffId = buffId,
                    VisualId = visualId,
                    UseStacks = useStacks,
                }
            );

        public void AddTag(TagId tag, int visualId) =>
            _tags.Add(new TagVisualRule { Tag = tag, VisualId = visualId });

        public override void SyncLogic(CombatWorld world)
        {
            if (!Self.TryLogic(world, out var actor))
            {
                StopAll();
                return;
            }
            actor.TryGetComp<BuffComp>(out var buffs);
            actor.TryGetComp<TagComp>(out var tags);
            for (int i = 0; i < _buffs.Count; i++)
            {
                var r = _buffs[i];
                int n = buffs == null ? 0 : buffs.StacksOf(r.BuffId);
                if (!r.UseStacks && n > 0)
                    n = 1;
                SyncHandle(r.VisualId, n);
            }
            for (int i = 0; i < _tags.Count; i++)
                SyncHandle(_tags[i].VisualId, tags != null && tags.Has(_tags[i].Tag) ? 1 : 0);
        }

        void SyncHandle(int id, int stacks)
        {
            if (stacks <= 0)
            {
                if (_handles.TryGetValue(id, out var h))
                {
                    _vfx.Stop(h);
                    _handles.Remove(id);
                }
                return;
            }
            if (!_handles.TryGetValue(id, out var handle))
            {
                handle = _vfx.PlayLoop(id);
                if (handle == 0)
                    return;
                _handles[id] = handle;
            }
            _vfx.SetIntensity(handle, stacks);
        }

        void StopAll()
        {
            foreach (var kv in _handles)
                _vfx.Stop(kv.Value);
            _handles.Clear();
        }

        protected override void OnDetach()
        {
            StopAll();
            _vfx.Release();
        }
    }
}
