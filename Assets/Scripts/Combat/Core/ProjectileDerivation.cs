using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public readonly struct SkillOrigin
    {
        public readonly SkillNodeId Skill;
        public readonly int CastId;
        public readonly bool SuppressedEvents;
        public readonly EntityId Caster;
        public SkillOrigin(SkillNodeId skill,int castId,bool suppressedEvents=false,EntityId caster=default){Skill=skill;CastId=castId;SuppressedEvents=suppressedEvents;Caster=caster;}
        public SkillOrigin WithoutEvents()=>new SkillOrigin(Skill,CastId,true,Caster);
    }

    public enum ChainTargetMode {Nearest,Random}
    public sealed class ProjectileChainDefinition
    {
        public int ExtraChains;
        public float Radius=6;
        public ChainTargetMode Mode;
    }

    // A family owns only a bounded emission quota. Hit history remains per branch.
    public sealed class ProjectileFamily
    {
        public const int MaxEmissions=256,MaxGeneration=8;
        public SkillOrigin Origin {get;}
        public int Emitted {get;private set;}
        public ProjectileFamily(SkillOrigin origin){Origin=origin;}
        internal bool Reserve(){if(Emitted>=MaxEmissions)return false;Emitted++;return true;}
    }

    public sealed class ProjectileSeed
    {
        internal readonly ProjectileFamily Family;
        internal readonly EntityId[] Ancestors;
        internal readonly int Generation;
        internal readonly float Range;
        internal readonly bool AtImpact;
        internal readonly double? VerticalSpeed;
        internal ProjectileSeed(ProjectileFamily family,int generation,float range,bool atImpact,EntityId[] ancestors,double? verticalSpeed=null)
        {Family=family;Generation=generation;Range=range;AtImpact=atImpact;Ancestors=ancestors??Array.Empty<EntityId>();VerticalSpeed=verticalSpeed;}
    }

    // Only the projectile hit boundary creates this context; effects cannot invent a parent.
    public sealed class ProjectileHitContext
    {
        internal readonly ProjectileDefinition Definition;
        internal readonly ProjectileFamily Family;
        readonly EntityId[] _visited;
        readonly double _verticalSpeed;
        public EntityId Projectile {get;}
        public EntityId Owner {get;}
        public int Generation {get;}
        public int HitIndex {get;}
        public float RemainingRange {get;}
        public SimVec3 Point {get;}
        public float Yaw {get;}
        public float Attack {get;}
        public float Power {get;}
        public SkillOrigin Origin=>Family.Origin;
        internal ProjectileHitContext(Actor actor,ProjectileComp body,SimVec3 point,float yaw,float attack)
        {
            Projectile=actor.Id;Owner=body.OwnerId;Definition=body.Def;Family=body.Family;
            Generation=body.Generation;HitIndex=body.HitCount;RemainingRange=(float)Math.Max(0,body.RangeLimit-body.Distance);
            Point=point;Yaw=yaw;Attack=attack;Power=body.Power;_visited=body.Visited();
            _verticalSpeed=body.VerticalSpeed;
        }
        internal ProjectileSeed Child(float range)=>new ProjectileSeed(Family,Generation+1,range,true,(EntityId[])_visited.Clone(),_verticalSpeed);
    }

    public static class ProjectileEmission
    {
        public static float Offset(int index,int count,float spread)
            =>count<=1?0:spread>=360?index*360f/count: -spread*.5f+index*spread/(count-1);

        internal static bool Post(CombatWorld world,EntityId owner,int spec,SimVec3 point,float yaw,float attack,
            EntityId target,float power,ProjectileSeed seed)
        {
            if(seed.Generation>ProjectileFamily.MaxGeneration||!seed.Family.Reserve())
            {world.Events.Publish(new EvProjectileBehavior(owner,EntityId.Invalid,"rejected",point,EntityId.Invalid,0,seed.Generation,seed.Family.Origin));return false;}
            world.Intents.Post(new SpawnProjectileIntent(owner,spec,point,yaw,attack,target,power,seed));
            return true;
        }
    }

    public sealed class SplitProjectileEffect : IEffect
    {
        readonly int _count,_generations;
        readonly float _spread,_multiplier,_range;
        readonly bool _inheritRange;
        public SplitProjectileEffect(int count,float spread,int generations,float multiplier,bool inheritRange,float range)
        {_count=count;_spread=spread;_generations=generations;_multiplier=multiplier;_inheritRange=inheritRange;_range=range;}
        public void Apply(ref EffectContext context)
        {
            var hit=context.ProjectileHit;
            if(context.World==null||hit==null||hit.Generation>=_generations)return;
            float range=_inheritRange?hit.RemainingRange:_range;
            if(range<=.000001f)return;
            int emitted=0;
            for(int i=0;i<_count;i++)
                if(ProjectileEmission.Post(context.World,hit.Owner,hit.Definition.SpecId,hit.Point,
                    hit.Yaw+ProjectileEmission.Offset(i,_count,_spread),hit.Attack,EntityId.Invalid,
                    hit.Power*_multiplier,hit.Child(range)))emitted++;
            if(emitted>0)context.World.Events.Publish(new EvProjectileBehavior(hit.Owner,hit.Projectile,"split",hit.Point,
                EntityId.Invalid,emitted,hit.Generation+1,hit.Origin));
        }
    }
}
