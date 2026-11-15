using System;

namespace Combat.Core
{
    public sealed class AmmoComp : Comp
    {
        public int Capacity {get;}
        public bool Unlimited {get;}
        public int Current {get;private set;}
        public AmmoComp(int capacity,int? initial=null,bool unlimited=false)
        {
            if(capacity<0||initial.HasValue&&(initial.Value<0||initial.Value>capacity))throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity=capacity;Current=initial??capacity;Unlimited=unlimited;
        }
        public bool CanSpend(int amount)=>amount>=0&&(Unlimited||Current>=amount);
        public bool TrySpend(int amount){if(!CanSpend(amount))return false;if(!Unlimited){Current-=amount;Publish(-amount,"Spent");}return true;}
        public void Refill(int amount){int before=Current;if(amount>0)Current=(int)Math.Min(Capacity,(long)Current+amount);Publish(Current-before,"Refilled");}
        void Publish(int delta,string reason){if(delta!=0&&Self?.World!=null)Self.World.Events.Publish(new EvAmmoChanged(Self.Id,Current,Capacity,delta,reason));}
        protected override void OnDetach()=>Current=0;
    }
    public readonly struct EvAmmoChanged
    {
        public readonly EntityId Source;
        public readonly int Current,Capacity,Delta;
        public readonly string Reason;
        public EvAmmoChanged(EntityId source,int current,int capacity,int delta,string reason){Source=source;Current=current;Capacity=capacity;Delta=delta;Reason=reason;}
    }
    public sealed class RefillAmmoEffect : IEffect
    {
        readonly int _amount;
        public RefillAmmoEffect(int amount)=>_amount=amount;
        public void Apply(ref EffectContext context)
        {var target=context.Target??context.Source;if(target!=null&&target.TryGetComp<AmmoComp>(out var ammo))ammo.Refill(_amount);}
    }
}
