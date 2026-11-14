using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum StackPolicy : byte { RefreshDuration, AddStack, Independent, RejectIfExists }
    public enum DispelMode : byte { ByBuffId, ByMutexGroup, ByTag, BySource }
    public enum BuffEventKind { Applied, Stacked, Refreshed, Period, Expired, Hurt, Cast, Removed, Rejected }
    public enum BuffRemovalReason { Expired, Dispelled, Mutex, Death, Manual, Detached }

    public sealed class DurationSpec
    {
        public int BuffId;
        public string Key, Name;
        public float Duration = 3f, TickInterval;
        public int MaxStacks = 1, MutexGroup;
        public StackPolicy Stack = StackPolicy.RefreshDuration;
        public Modifier[] Modifiers = Array.Empty<Modifier>();
        public TagId[] GrantedTags = Array.Empty<TagId>();
        public IEffect[] OnApply = Array.Empty<IEffect>(), OnStack = Array.Empty<IEffect>(), OnPeriod = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>(), OnHurted = Array.Empty<IEffect>(), OnOwnerCast = Array.Empty<IEffect>(), OnRemoved = Array.Empty<IEffect>();
    }

    public sealed class BuffCatalog
    {
        readonly Dictionary<string,DurationSpec> _keys = new Dictionary<string,DurationSpec>(StringComparer.Ordinal);
        readonly Dictionary<int,DurationSpec> _ids = new Dictionary<int,DurationSpec>();
        public void Register(DurationSpec spec)
        {
            if(spec==null||string.IsNullOrWhiteSpace(spec.Key)||spec.BuffId<=0||_keys.ContainsKey(spec.Key)||_ids.ContainsKey(spec.BuffId))
                throw new ArgumentException("Buff requires unique key and positive runtime identity.");
            _keys.Add(spec.Key,spec);_ids.Add(spec.BuffId,spec);
        }
        public bool TryGet(string key,out DurationSpec spec)=>_keys.TryGetValue(key??"",out spec);
        public bool TryGet(int id,out DurationSpec spec)=>_ids.TryGetValue(id,out spec);
    }

    public readonly struct BuffState
    {
        public readonly int InstanceId,BuffId,Stacks;
        public readonly string Key,Name;
        public readonly EntityId Source;
        public readonly float? Remaining;
        public BuffState(int instanceId,int buffId,string key,string name,EntityId source,int stacks,float? remaining)
        {InstanceId=instanceId;BuffId=buffId;Key=key;Name=name;Source=source;Stacks=stacks;Remaining=remaining;}
    }
}
