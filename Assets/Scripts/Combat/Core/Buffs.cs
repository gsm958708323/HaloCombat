using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class BuffComp : Comp
    {
        sealed class Instance
        {
            public int Id,Stacks,BornFrame;
            public EntityId Source;
            public double Age,ExpiresAt,NextPeriod;
            public float Attack,Power;
            public DurationSpec Spec;
            public SkillOrigin Origin;
            public readonly List<TagLease> Leases=new List<TagLease>();
        }
        readonly List<Instance> _list=new List<Instance>();
        readonly HashSet<(int,BuffEventKind)> _dispatching=new HashSet<(int,BuffEventKind)>();
        AttributeSet _attr;
        TagComp _tags;
        int _nextLocal,_clearing,_dispatchDepth;
        public int Count=>_list.Count;
        public int StacksOf(int buffId)
        {
            long count = 0;
            foreach (var item in _list)
                if (item.Spec.BuffId == buffId) count += item.Stacks;
            return (int)Math.Min(int.MaxValue, count);
        }
        protected override void OnAttach(){_attr=Self.GetComp<AttributeSet>();_tags=Self.GetComp<TagComp>();}
        protected override void OnDetach()
        {ClearAll(BuffRemovalReason.Detached,false);_attr=null;_tags=null;_dispatching.Clear();}

        public BuffState[] Snapshot()
        {
            var result=new BuffState[_list.Count];
            for(int i=0;i<result.Length;i++)
            {
                var item=_list[i];result[i]=new BuffState(item.Id,item.Spec.BuffId,item.Spec.Key,item.Spec.Name,item.Source,item.Stacks,
                    double.IsPositiveInfinity(item.ExpiresAt)?null:(float?)Math.Max(0,item.ExpiresAt-item.Age));
            }
            return result;
        }
        public bool Apply(DurationSpec spec,Actor source,int addStacks=1,float? snapshotAttack=null,float power=1,SkillOrigin origin=default)
        {
            if(spec==null)throw new ArgumentNullException(nameof(spec));
            if(Self==null||!Self.IsActive||_clearing>0||_tags.Has(CommonTags.Dead))return false;
            addStacks=Math.Max(1,addStacks);
            if(spec.MutexGroup>0)
                foreach(var old in _list.ToArray())
                    if(old.Spec.MutexGroup==spec.MutexGroup&&old.Spec.BuffId!=spec.BuffId)Remove(old,BuffRemovalReason.Mutex);
            if(!Self.IsActive||_tags.Has(CommonTags.Dead))return false;
            Instance existing=null;
            if(spec.Stack!=StackPolicy.Independent)
                foreach(var item in _list)if(item.Spec.BuffId==spec.BuffId){existing=item;break;}
            if(existing!=null)
            {
                if(spec.Stack==StackPolicy.RejectIfExists){Record(existing,BuffEventKind.Rejected);return false;}
                int old=existing.Stacks;
                if(spec.Stack==StackPolicy.AddStack)existing.Stacks=Math.Min(Math.Max(1,spec.MaxStacks),existing.Stacks+addStacks);
                existing.ExpiresAt=spec.Duration>0?existing.Age+spec.Duration:double.PositiveInfinity;
                SetSource(existing,source,snapshotAttack,power,origin);
                var kind=existing.Stacks>old?BuffEventKind.Stacked:BuffEventKind.Refreshed;
                Record(existing,kind);
                if(kind==BuffEventKind.Stacked)Dispatch(existing,kind,spec.OnStack!=null&&spec.OnStack.Length>0?spec.OnStack:spec.OnApply);
                return true;
            }
            if(_list.Count>=128)
            {
                Self.World?.Events.Publish(new EvBuffLifecycle(Self.Id,source?.Id??EntityId.Invalid,source?.Id??EntityId.Invalid,spec.BuffId,0,0,BuffEventKind.Rejected));
                return false;
            }
            var created=new Instance{Id=Self.World!=null?Self.World.NextBuffInstanceId():++_nextLocal,Stacks=Math.Min(Math.Max(1,spec.MaxStacks),addStacks),
                BornFrame=Self.World?.Time.Frame??0,Spec=spec,ExpiresAt=spec.Duration>0?spec.Duration:double.PositiveInfinity,NextPeriod=spec.TickInterval};
            SetSource(created,source,snapshotAttack,power,origin);_list.Add(created);
            foreach(var prototype in spec.Modifiers??Array.Empty<Modifier>()){var mod=prototype;mod.SourceId=created.Id;_attr.AddMod(mod);}
            foreach(var tag in spec.GrantedTags??Array.Empty<TagId>())created.Leases.Add(_tags.Acquire(tag));
            Record(created,BuffEventKind.Applied);Dispatch(created,BuffEventKind.Applied,spec.OnApply);return true;
        }
        void SetSource(Instance item,Actor source,float? snapshotAttack,float power,SkillOrigin origin)
        {
            if(source!=null&&source.World==Self.World&&source.IsActive)
            {item.Source=source.Id;item.Attack=snapshotAttack??(source.TryGetComp<AttributeSet>(out var attr)?attr.GetFinal(AttrId.Atk):0);}
            else if(!item.Source.IsValid){item.Source=EntityId.Invalid;item.Attack=snapshotAttack??0;}
            item.Power=power>0?power:1;
            item.Origin=origin;
        }
        public override void Tick(float dt)
        {
            if(Self.World==null||dt<0)return;
            foreach(var item in _list.ToArray())
            {
                if(!_list.Contains(item)||item.BornFrame==Self.World.Time.Frame)continue;
                item.Age+=dt;
                if(item.Spec.TickInterval>0)
                    while(_list.Contains(item)&&item.NextPeriod<=item.Age+.000001&&item.NextPeriod<item.ExpiresAt-.000001)
                    {
                        item.NextPeriod+=item.Spec.TickInterval;
                        Record(item,BuffEventKind.Period);Dispatch(item,BuffEventKind.Period,item.Spec.OnPeriod);
                    }
                if(_list.Contains(item)&&item.Age+.000001>=item.ExpiresAt)Remove(item,BuffRemovalReason.Expired);
            }
        }
        // A cost is paid across oldest instances first. All debits happen before
        // OnRemoved effects can apply or consume another buff.
        public bool TryConsumeStacks(int buffId, int count)
        {
            if (count <= 0 || StacksOf(buffId) < count) return false;
            var debits = new List<(Instance Item, bool Removed)>();
            for (int i = 0; count > 0;)
            {
                var item = _list[i];
                if (item.Spec.BuffId != buffId) { i++; continue; }
                if (item.Stacks <= count)
                {
                    count -= item.Stacks;
                    _list.RemoveAt(i);
                    ReleaseContributions(item);
                    debits.Add((item, true));
                    continue;
                }
                item.Stacks -= count;
                count = 0;
                debits.Add((item, false));
            }
            foreach (var debit in debits)
            {
                if (debit.Removed) NotifyRemoval(debit.Item, BuffRemovalReason.Consumed, true);
                else Record(debit.Item, BuffEventKind.Consumed, BuffRemovalReason.Consumed);
            }
            return true;
        }
        public bool RemoveInstance(int id)
        {foreach(var item in _list.ToArray())if(item.Id==id){Remove(item,BuffRemovalReason.Manual);return true;}return false;}
        public int Dispel(DispelMode mode,int key,TagId tag,int maxCount,EntityId source=default)
        {
            int removed=0;
            var snapshot=_list.ToArray();
            for(int i=snapshot.Length-1;i>=0;i--)
            {
                var item=snapshot[i];if(!_list.Contains(item))continue;
                bool matches=mode==DispelMode.ByBuffId?item.Spec.BuffId==key:
                    mode==DispelMode.ByMutexGroup?key>0&&item.Spec.MutexGroup==key:
                    mode==DispelMode.BySource?source.IsValid&&item.Source==source:Grants(item.Spec,tag);
                if(!matches)continue;Remove(item,BuffRemovalReason.Dispelled);removed++;
                if(maxCount>0&&removed>=maxCount)break;
            }
            return removed;
        }
        public void ClearAll(BuffRemovalReason reason,bool effects=true)
        {
            _clearing++;
            try{foreach(var item in _list.ToArray())Remove(item,reason,effects);}
            finally{_clearing--;}
        }
        void Remove(Instance item,BuffRemovalReason reason,bool effects=true)
        {
            if(!_list.Remove(item))return;
            ReleaseContributions(item);
            NotifyRemoval(item, reason, effects);
        }
        void ReleaseContributions(Instance item)
        {
            _attr.RemoveBySource(item.Id);
            foreach (var lease in item.Leases) lease.Dispose();
            item.Leases.Clear();
        }
        void NotifyRemoval(Instance item, BuffRemovalReason reason, bool effects)
        {
            if(reason==BuffRemovalReason.Expired)
            {Record(item,BuffEventKind.Expired,reason);if(effects)Dispatch(item,BuffEventKind.Expired,item.Spec.OnExpire,removed:true);}
            Record(item,BuffEventKind.Removed,reason);
            if(effects)Dispatch(item,BuffEventKind.Removed,item.Spec.OnRemoved,removed:true);
        }
        public void DispatchOnHurted(Actor attacker,SkillOrigin origin=default)
        {
            foreach(var item in _list.ToArray())
                if(item.BornFrame!=(Self.World?.Time.Frame??0)&&_list.Contains(item))
                {Record(item,BuffEventKind.Hurt,source:attacker?.Id??EntityId.Invalid);Dispatch(item,BuffEventKind.Hurt,item.Spec.OnHurted,attacker,eventOrigin:origin);}
        }
        public void DispatchOnOwnerCast()
        {
            foreach(var item in _list.ToArray())if(_list.Contains(item))
            {Record(item,BuffEventKind.Cast,source:Self.Id);Dispatch(item,BuffEventKind.Cast,item.Spec.OnOwnerCast,Self,eventOrigin:Self.TryGetComp<SkillDirectorComp>(out var director)?director.CurrentOrigin:default);}
        }
        void Dispatch(Instance item,BuffEventKind kind,IEffect[] effects,Actor eventSource=null,bool removed=false,SkillOrigin eventOrigin=default)
        {
            if(effects==null||effects.Length==0||Self.World==null||(!removed&&!_list.Contains(item)))return;
            if(_dispatchDepth>=16||!_dispatching.Add((item.Id,kind))){Record(item,BuffEventKind.Rejected);return;}
            _dispatchDepth++;
            try
            {
                Actor source=eventSource;float attack=item.Attack;
                if(kind==BuffEventKind.Hurt||kind==BuffEventKind.Cast)
                    attack=source!=null&&source.TryGetComp<AttributeSet>(out var attr)?attr.GetFinal(AttrId.Atk):0;
                else Self.World.TryGetActor(item.Source,out source);
                var provenance=kind==BuffEventKind.Hurt||kind==BuffEventKind.Cast?eventOrigin:item.Origin;
                if(item.Origin.SuppressedEvents)provenance=provenance.WithoutEvents();
                if (kind == BuffEventKind.Period) provenance = provenance.FromBuffPeriod(item.Spec.BuffId);
                Self.World.Deliver(effects, source, Self, attack, Self.GetComp<TransformComp>().Position,
                    buffStacks:item.Stacks, power:item.Power, origin:provenance);
            }
            finally{_dispatchDepth--;_dispatching.Remove((item.Id,kind));}
        }
        void Record(Instance item,BuffEventKind kind,BuffRemovalReason reason=default,EntityId? source=null)
            =>Self.World?.Events.Publish(new EvBuffLifecycle(Self.Id,source??item.Source,item.Source,item.Spec.BuffId,item.Id,item.Stacks,kind,reason));
        static bool Grants(DurationSpec spec,TagId tag)
        {foreach(var value in spec.GrantedTags??Array.Empty<TagId>())if(value==tag)return true;return false;}
    }
}
