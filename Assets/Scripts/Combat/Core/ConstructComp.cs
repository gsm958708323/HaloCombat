using System;

namespace Combat.Core
{
    public sealed class ConstructComp : IncomingDamagePolicyComp
    {
        double _decayAge;
        public ConstructDefinition Def {get;private set;}
        public EntityId OwnerId {get;private set;}
        public SkillOrigin Origin {get;private set;}
        public float Power {get;private set;}
        public bool Ending {get;private set;}
        public float? NextDecay=>Def?.DecayPeriod>0?(float?)Math.Max(0,Def.DecayPeriod-_decayAge):null;
        public override bool WantsTick=>true;
        protected override void OnAttach()=>Self.World.Events.Subscribe<EvEntityDead>(OnDead);
        public void Setup(ConstructDefinition definition,EntityId owner,float power,SkillOrigin origin)
        {
            Def=definition;OwnerId=owner;Power=power;Origin=origin;_decayAge=0;Ending=false;
            var attributes=Self.GetComp<AttributeSet>();attributes.SetBase(AttrId.MaxHp,definition.Hp);attributes.SetBase(AttrId.Hp,definition.Hp);
            attributes.SetBase(AttrId.Atk,0);attributes.SetBase(AttrId.MoveSpeed,0);attributes.SetBase(AttrId.DmgDealMul,1);attributes.SetBase(AttrId.DmgTakenMul,1);
            Self.GetComp<TeamComp>().SetTeam(definition.Team);Self.GetComp<LocomotionComp>().CollisionRadius=definition.Radius;
        }
        public override float Filter(Actor source,float amount,in EffectContext context)
        {
            if(Def==null||Def.DamageCap==0||source!=null&&source.TryGetComp<ConstructComp>(out var other)&&other.Def?.Group==Def.Group)return amount;
            return Math.Min(Def.DamageCap,amount);
        }
        public override void Tick(float dt)
        {
            if(Def==null||Ending||Def.DecayPeriod<=0)return;_decayAge+=dt;
            while(!Ending&&_decayAge+.000001>=Def.DecayPeriod)
            {
                _decayAge-=Def.DecayPeriod;Emit(ConstructEventKind.Decay,Def.DecayDamage.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Self.World.Deliver(new IEffect[]{new DamageEffect{Flat=Def.DecayDamage,Coeff=0,IgnoreDef=true,CanCrit=false,IncludeBonuses=false,FireOnHurted=false}},Self,Self,0,origin:Origin);
            }
        }
        void OnDead(EvEntityDead e)
        {if(Self==null||e.Id!=Self.Id||Def==null||Ending)return;Ending=true;Self.World.QueueConstructDeath(this);}
        internal void CompleteDeath(bool effects=true)
        {if(Self==null||Def==null)return;Emit(effects?ConstructEventKind.Died:ConstructEventKind.Rejected,effects?"Death":"DeathWaveLimit");Complete(effects,effects);}
        public void Remove(bool ownerRemoved=false)
        {if(Ending||Def==null)return;Ending=true;Emit(ownerRemoved?ConstructEventKind.OwnerRemoved:ConstructEventKind.Removed,"Removed");Complete(false,true);}
        void Complete(bool death,bool callbacks)
        {
            var world=Self.World;var definition=Def;var point=Self.GetComp<TransformComp>().Position;world.TryGetActor(OwnerId,out var owner);
            float attack=owner!=null&&owner.TryGetComp<AttributeSet>(out var attr)?attr.GetFinal(AttrId.Atk):0;
            if(death)world.Deliver(definition.OnDeath,owner??Self,Self,attack,point,power:Power,origin:Origin);
            if(callbacks)world.Deliver(definition.OnRemoved,owner??Self,Self,attack,point,power:Power,origin:Origin);
            world.RequestDespawn(Self.Id);
        }
        void Emit(ConstructEventKind kind,string detail)=>Self.World.Events.Publish(new EvConstructLifecycle(Self.Id,OwnerId,Def.SpecId,kind,detail,Origin));
        protected override void OnDetach()
        {Self.World?.Events.Unsubscribe<EvEntityDead>(OnDead);Def=null;OwnerId=EntityId.Invalid;Origin=default;Power=1;_decayAge=0;Ending=false;}
    }
}
