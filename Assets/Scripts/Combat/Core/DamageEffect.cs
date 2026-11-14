using System;

namespace Combat.Core
{
    public enum DamageKind {Physical,Elemental}

    public sealed class DamageResult
    {
        public EntityId Source {get;}
        public EntityId Target {get;}
        public SkillOrigin Origin {get;}
        public int HitIndex {get;}
        public DamageKind Kind {get;}
        public float Base {get;}
        public float Scaled {get;}
        public float AfterDefense {get;}
        public float AfterCritical {get;}
        public float Final {get;}
        public float Absorbed {get;}
        public float HpLost {get;}
        public float Overkill {get;}
        public bool Critical {get;}
        public bool Killed {get;}
        public DamageResult(EntityId source,EntityId target,SkillOrigin origin,int index,DamageKind kind,
            float basis,float scaled,float defense,float critical,float final,float absorbed,float lost,float overkill,bool crit,bool killed)
        {Source=source;Target=target;Origin=origin;HitIndex=index;Kind=kind;Base=basis;Scaled=scaled;AfterDefense=defense;AfterCritical=critical;
            Final=final;Absorbed=absorbed;HpLost=lost;Overkill=overkill;Critical=crit;Killed=killed;}
    }

    public sealed class DamageEffect:IEffect
    {
        public float Coeff=1,Flat;
        public bool IgnoreDef,CanCrit,ScaleByBuffStacks;
        public bool UseSnapshotAtk=true,IncludeBonuses=true,FireOnHurted=true;
        public float CritMul=2,CritChance=-1;
        public int HitstopFrames;
        public DamageKind Kind;
        public void Apply(ref EffectContext ctx)
        {
            ctx.Damage=null;var target=ctx.Target;var source=ctx.Source;
            if(ctx.World==null||target==null||!target.IsActive||target.GetComp<TagComp>().Has(CommonTags.Dead))return;
            var attributes=target.GetComp<AttributeSet>();float hp=attributes.GetBase(AttrId.Hp);if(hp<=0)return;
            if(target.GetComp<TagComp>().Has(CommonTags.Invincible)||(target.TryGetComp<HealthComp>(out var health)&&health.InIFrame))
            {ctx.World.Events.Publish(new EvImmune(target.Id,source?.Id??EntityId.Invalid));return;}
            float attack=UseSnapshotAtk?ctx.SnapshotAtk:source!=null&&source.TryGetComp<AttributeSet>(out var live)?live.GetFinal(AttrId.Atk):ctx.SnapshotAtk;
            float basis=Math.Max(0,(IncludeBonuses?attack*Coeff:0)+Flat),scaled=basis*(ctx.Power>0?ctx.Power:1);
            float defense=IgnoreDef||Kind==DamageKind.Elemental?0:attributes.GetFinal(AttrId.Def);
            float afterDefense=Math.Max(0,scaled-defense)*(ScaleByBuffStacks?Math.Max(1,ctx.BuffStacks):1);
            bool crit=false;
            if(CanCrit)
            {
                float rate=CritChance;
                if(rate<0&&source!=null&&source.TryGetComp<AttributeSet>(out var sourceAttributes))rate=sourceAttributes.GetFinal(AttrId.CritRate);
                crit=ctx.World.Random.Next01()<rate;
            }
            float afterCritical=afterDefense*(crit?(CritMul>0?CritMul:2):1);
            float deal=IncludeBonuses&&source!=null&&source.TryGetComp<AttributeSet>(out var outgoing)?outgoing.GetFinal(AttrId.DmgDealMul):1;
            float final=Math.Max(0,afterCritical*deal*attributes.GetFinal(AttrId.DmgTakenMul));
            float absorbed=Math.Min(Math.Max(0,attributes.GetBase(AttrId.Shield)),final);
            float lost=Math.Min(hp,Math.Max(0,final-absorbed)),overkill=Math.Max(0,final-absorbed-lost);
            attributes.SetBase(AttrId.Shield,Math.Max(0,attributes.GetBase(AttrId.Shield)-absorbed));
            attributes.SetBase(AttrId.Hp,hp-lost);
            bool killed=lost>0&&hp-lost<=0;
            var result=new DamageResult(source?.Id??EntityId.Invalid,target.Id,ctx.Origin,ctx.World.NextDamageHitId(),Kind,basis,scaled,afterDefense,afterCritical,final,absorbed,lost,overkill,crit,killed);
            ctx.Damage=result;
            ctx.World.Events.Publish(new EvDamage(result.Source,target.Id,absorbed+lost,crit,absorbed,killed,ctx.Origin,result));
            if(HitstopFrames>0&&absorbed+lost>0)
            {ctx.World.RequestHitstop(HitstopFrames);ctx.World.Events.Publish(new EvHitstop(result.Source,target.Id,HitstopFrames));}
            if(killed&&target.TryGetComp<StateMachineComp>(out var fsm))
                fsm.TryEnter(ActivityId.Dead,new ActivityEnterArgs{Reason="Kill",Killer=result.Source});
            if(FireOnHurted&&target.TryGetComp<BuffComp>(out var buffs))buffs.DispatchOnHurted(source,ctx.Origin);
            if(lost>0)
            {
                ctx.World.DispatchSkillEvent(SkillEventKind.Hit,ctx);
                if(crit)ctx.World.DispatchSkillEvent(SkillEventKind.Critical,ctx);
                if(killed)ctx.World.DispatchSkillEvent(SkillEventKind.Kill,ctx);
            }
        }
    }

    public sealed class DamageAttackerEffect:IEffect
    {
        public DamageEffect Inner=new DamageEffect{FireOnHurted=false,CanCrit=false,UseSnapshotAtk=true};
        public void Apply(ref EffectContext ctx)
        {if(ctx.Source==null)return;var inner=ctx;inner.Target=ctx.Source;Inner.Apply(ref inner);}
    }

    public sealed class AfterDamageEffect:IEffect
    {
        readonly IEffect[] _effects;
        readonly bool _latest;
        public AfterDamageEffect(IEffect[] effects,bool requireLatestCast=false){_effects=effects;_latest=requireLatestCast;}
        public void Apply(ref EffectContext ctx)
        {
            var result=ctx.Damage;
            if(result==null||result.HpLost<=0||result.Source!=(ctx.Source?.Id??EntityId.Invalid)||result.Target!=(ctx.Target?.Id??EntityId.Invalid))return;
            if(_latest&&(ctx.Source==null||!ctx.Source.TryGetComp<SkillDirectorComp>(out var director)||
                director.CurrentOrigin.CastId!=result.Origin.CastId||!director.CurrentOrigin.Skill.Equals(result.Origin.Skill)||result.Origin.CastId<=0))return;
            ctx.World.Deliver(_effects,ctx.Source,ctx.Target,ctx.SnapshotAtk,ctx.HasPoint?(SimVec3?)ctx.Point:null,ctx.HasDir?(SimVec3?)ctx.Dir:null,
                ctx.BuffStacks,ctx.Power,ctx.Origin,ctx.ProjectileHit,result);
        }
    }
}
