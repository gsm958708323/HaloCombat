using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class ConstructDefinition
    {
        public int SpecId,Team,Count=1,MaxAlive=64;
        public string Group,View="barrel";
        public float Hp=5,Radius=.25f,DamageCap=1,DecayPeriod=5,DecayDamage=1,ForwardOffset=.55f,Spacing=1;
        public bool CleanupWithOwner;
        public IEffect[] OnSpawn=Array.Empty<IEffect>(),OnDeath=Array.Empty<IEffect>(),OnRemoved=Array.Empty<IEffect>();
    }
    public sealed class ConstructCatalog
    {
        readonly Dictionary<int,ConstructDefinition> _definitions=new Dictionary<int,ConstructDefinition>();
        public bool TryGet(int id,out ConstructDefinition value)=>_definitions.TryGetValue(id,out value);
        public void Register(ConstructDefinition value)=>_definitions.Add(value.SpecId,value);
    }
    public abstract class IncomingDamagePolicyComp : Comp
    {
        public abstract float Filter(Actor source,float amount,in EffectContext context);
    }
    public enum ConstructEventKind {Spawned,Decay,Died,Removed,OwnerRemoved,Rejected}
    public readonly struct EvConstructLifecycle
    {
        public readonly EntityId Id,Owner;
        public readonly int SpecId;
        public readonly ConstructEventKind Kind;
        public readonly string Detail;
        public readonly SkillOrigin Origin;
        public EvConstructLifecycle(EntityId id,EntityId owner,int specId,ConstructEventKind kind,string detail,SkillOrigin origin)
        {Id=id;Owner=owner;SpecId=specId;Kind=kind;Detail=detail;Origin=origin;}
    }
}
