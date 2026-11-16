using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Tests
{
    sealed class ProjectileMotionTestWorld
    {
        sealed class Factory:IActorFactory
        {
            public Actor Create(in ActorSpawnSpec spec)
            {
                var actor=new Actor();actor.AddComp(new TransformComp());actor.AddComp(new TagComp());actor.AddComp(new TeamComp(spec.BlueprintId=="owner"?1:2));
                if(spec.BlueprintId=="projectile"){actor.AddComp(new ProjectileComp());return actor;}
                if(spec.BlueprintId=="aoe"){actor.AddComp(new AoeComp());return actor;}
                var attributes=new AttributeSet();attributes.InitFighterDefaults();attributes.SetBase(AttrId.MaxHp,1000);attributes.SetBase(AttrId.Hp,1000);attributes.SetBase(AttrId.Def,0);
                actor.AddComp(attributes);return actor;
            }
            public void Release(Actor actor)=>actor.ResetForPool();
        }
        public CombatWorld World {get;}
        public EntityId Owner {get;}
        public readonly List<EvProjectileEnded> Ended=new List<EvProjectileEnded>();
        public readonly List<EvProjectileBehavior> Behaviors=new List<EvProjectileBehavior>();
        public readonly List<EffectContext> Captures=new List<EffectContext>();
        public readonly List<SimVec3> SpawnPositions=new List<SimVec3>();
        public int DamageCount {get;private set;}
        public ProjectileMotionTestWorld(ProjectileDefinition definition,ObstacleDefinition[] obstacles=null,SkillEventCatalog skillEvents=null)
        {
            World=new CombatWorld(new Factory(),obstacles:obstacles,skillEvents:skillEvents);var catalog=new ProjectileCatalog();catalog.Register(definition);World.ReplaceCatalogs(catalog,null,null);
            Owner=Spawn("owner",0);
            World.Events.Subscribe<EvProjectileEnded>(e=>Ended.Add(e));World.Events.Subscribe<EvProjectileBehavior>(e=>Behaviors.Add(e));
            World.Events.Subscribe<EvDamage>(e=>DamageCount++);
            World.Events.Subscribe<EvEntitySpawn>(e=>{if(e.BlueprintId=="projectile"&&World.TryGetActor(e.Id,out var actor))SpawnPositions.Add(actor.GetComp<TransformComp>().Position);});
        }
        public EntityId Spawn(string type,float x,float z=0)
        {var id=World.SpawnActor(new ActorSpawnSpec(type));World.TryGetActor(id,out var actor);actor.GetComp<TransformComp>().Position=new SimVec3(x,0,z);return id;}
        public void Fire(float yaw=0)=>World.Intents.Post(new SpawnProjectileIntent(Owner,1,default,yaw,0));
        public Actor Body=>World.RegistryActive().FirstOrDefault(a=>a.TryGetComp<ProjectileComp>(out _));
        public void Advance(float duration,int parts)
        {for(int i=0;i<parts;i++)World.Tick(duration/parts);}
        public sealed class Capture:IEffect
        {
            readonly List<EffectContext> _contexts;
            public Capture(List<EffectContext> contexts)=>_contexts=contexts;
            public void Apply(ref EffectContext context)=>_contexts.Add(context);
        }
        public static void Pierce(ProjectileDefinition definition,int maximum)
        {definition.OnHit=definition.OnHit.Where(e=>!(e is PierceEffect)).Concat(maximum==1?new IEffect[0]:new IEffect[]{new PierceEffect(maximum)}).ToArray();}
        public static ProjectileDefinition Definition()=>new ProjectileDefinition {SpecId=1,SpawnForward=0,Speed=4,Range=100,Lifetime=10,HitRadius=.1f,
            OnHit=new IEffect[]{new DamageEffect{Flat=10,Coeff=0,FireOnHurted=false},new PierceEffect(0)}};
    }
}
