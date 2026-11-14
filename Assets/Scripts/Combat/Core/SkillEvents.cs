using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum SkillEventKind {Hit,Critical,Kill,Completed,Wall,Range,ProjectileEnd}
    public sealed class SkillEventDefinition
    {
        readonly Dictionary<SkillEventKind,IEffect[]> _effects=new Dictionary<SkillEventKind,IEffect[]>();
        public void Set(SkillEventKind kind,IEffect[] effects)=>_effects[kind]=effects??Array.Empty<IEffect>();
        public IEffect[] Get(SkillEventKind kind)=>_effects.TryGetValue(kind,out var effects)?effects:Array.Empty<IEffect>();
    }
    public sealed class SkillEventCatalog
    {
        readonly Dictionary<int,SkillEventDefinition> _map=new Dictionary<int,SkillEventDefinition>();
        public void Register(SkillNodeId skill,SkillEventDefinition definition)=>_map.Add(skill.Value,definition);
        public bool TryGet(SkillNodeId skill,out SkillEventDefinition definition)=>_map.TryGetValue(skill.Value,out definition);
    }
    public readonly struct EvSkillEvent
    {
        public readonly SkillEventKind Kind;
        public readonly SkillOrigin Origin;
        public readonly EntityId Source,Target;
        public readonly SimVec3 Point,Direction;
        public readonly bool HasPoint,HasDirection,Rejected;
        public readonly DamageResult Damage;
        public EvSkillEvent(SkillEventKind kind,in EffectContext context,bool rejected=false)
        {Kind=kind;Origin=context.Origin;Source=context.Source?.Id??EntityId.Invalid;Target=context.Target?.Id??EntityId.Invalid;
            Point=context.Point;Direction=context.Dir;HasPoint=context.HasPoint;HasDirection=context.HasDir;Damage=context.Damage;Rejected=rejected;}
    }
    internal sealed class SkillEventRouter
    {
        readonly CombatWorld _world;
        readonly SkillEventCatalog _catalog;
        int _frame=-1,_count,_depth;
        bool _rejected;
        public SkillEventRouter(CombatWorld world,SkillEventCatalog catalog){_world=world;_catalog=catalog;}
        public void Emit(SkillEventKind kind,in EffectContext context)
        {
            if(context.Origin.SuppressedEvents||context.Origin.CastId<=0||!context.Origin.Skill.IsValid||
                context.Source==null||!context.Source.IsActive||context.Source.World!=_world||
                (context.Source.TryGetComp<TagComp>(out var tags)&&tags.Has(CommonTags.Dead))||
                !_catalog.TryGet(context.Origin.Skill,out var definition))return;
            if(context.Origin.Caster.IsValid&&!AreaTargets.TryLive(_world,context.Origin.Caster,out _))return;
            if(_frame!=_world.Time.Frame){_frame=_world.Time.Frame;_count=0;_rejected=false;}
            if(++_count>1024||_depth>=16)
            {if(!_rejected){_rejected=true;_world.Events.Publish(new EvSkillEvent(kind,context,true));}return;}
            _world.Events.Publish(new EvSkillEvent(kind,context));
            _depth++;
            try
            {
                _world.Deliver(definition.Get(kind),context.Source,context.Target,context.SnapshotAtk,
                    context.HasPoint?(SimVec3?)context.Point:null,context.HasDir?(SimVec3?)context.Dir:null,
                    context.BuffStacks,context.Power,context.Origin.WithoutEvents(),damage:context.Damage);
            }
            finally{_depth--;}
        }
    }
}
