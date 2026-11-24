using System;
using System.Linq;

namespace Combat.Core
{
    public static class ConstructActor
    {
        public static Actor Create()
        {
            var actor=new Actor();actor.AddComp(new TransformComp());actor.AddComp(new TagComp());actor.AddComp(new TeamComp(0));
            var attributes=new AttributeSet();attributes.InitFighterDefaults();actor.AddComp(attributes);
            actor.AddComp(new HealthComp());actor.AddComp(new BuffComp());actor.AddComp(new StateMachineComp());actor.AddComp(new LocomotionComp());actor.AddComp(new ConstructComp());return actor;
        }
    }
    public sealed class SpawnConstructEffect : IEffect
    {
        readonly int _spec;
        readonly EffectCenter _center;
        public SpawnConstructEffect(int spec,EffectCenter center){_spec=spec;_center=center;}
        public void Apply(ref EffectContext context)
        {
            var world=context.World;var source=context.Source;
            if(world==null||source==null||!AreaTargets.TryLive(world,source.Id,out _)||!EffectPosition.TryResolve(context,_center,out var origin))return;
            if(!world.Constructs.TryGet(_spec,out var definition))throw new InvalidOperationException("Unknown construct "+_spec);
            float yaw=context.HasDir?LocomotionComp.YawFromStick(context.Dir):source.GetComp<TransformComp>().YawDegrees;
            var forward=LocomotionComp.ForwardFromYaw(yaw);origin=new SimVec3(origin.X+forward.X*definition.ForwardOffset,world.Motor.GroundY,origin.Z+forward.Z*definition.ForwardOffset);
            for(int i=0;i<definition.Count;i++)
            {
                var bodies=world.RegistryActive().Where(a=>a.TryGetComp<ConstructComp>(out var c)&&!c.Ending).ToArray();
                if(bodies.Length>=128||bodies.Count(a=>a.GetComp<ConstructComp>().OwnerId==source.Id&&a.GetComp<ConstructComp>().Def?.SpecId==_spec)>=definition.MaxAlive)
                {Reject(context,bodies.Length>=128?"WorldLimit":"OwnerLimit");break;}
                var point=origin;
                if(definition.Count>1){var d=LocomotionComp.ForwardFromYaw(yaw+i*360f/definition.Count);point=new SimVec3(point.X+d.X*definition.Spacing,point.Y,point.Z+d.Z*definition.Spacing);}
                if(!world.Collision.IsFree(point,definition.Radius)){Reject(context,"BlockedPosition");continue;}
                var id=world.SpawnActor(new ActorSpawnSpec("construct"),false);world.TryGetActor(id,out var actor);
                var tf=actor.GetComp<TransformComp>();tf.Position=point;tf.YawDegrees=yaw;
                actor.GetComp<ConstructComp>().Setup(definition,source.Id,context.Power>0?context.Power:1,context.Origin);
                world.PublishSpawn(id,"construct");world.Events.Publish(new EvConstructLifecycle(id,source.Id,_spec,ConstructEventKind.Spawned,definition.Group,context.Origin));
                world.Deliver(definition.OnSpawn,source,actor,context.SnapshotAtk,point,power:context.Power,origin:context.Origin);
            }
        }
        void Reject(in EffectContext context,string reason)=>context.World.Events.Publish(new EvConstructLifecycle(EntityId.Invalid,context.Source.Id,_spec,ConstructEventKind.Rejected,reason,context.Origin));
    }
    public sealed class ChainConstructsEffect : IEffect
    {
        readonly string _group;
        readonly float _radius,_damage;
        public ChainConstructsEffect(string group,float radius,float damage){_group=group;_radius=radius;_damage=damage;}
        public void Apply(ref EffectContext context)
        {
            if(context.World==null||!EffectPosition.TryResolve(context,EffectCenter.Point,out var point))return;
            var source=context.Target!=null&&context.Target.TryGetComp<ConstructComp>(out var dying)&&dying.Ending?context.Target:context.Source;
            foreach(var actor in context.World.RegistryActive())
            {
                if(actor==source||!actor.TryGetComp<ConstructComp>(out var body)||body.Ending||body.Def?.Group!=_group||
                    actor.GetComp<TagComp>().Has(CommonTags.Dead)||SpatialGeometry.DistanceSquared(point,actor.GetComp<TransformComp>().Position)>_radius*_radius)continue;
                var ignition=source!=null&&source.TryGetComp<ConstructComp>(out _)?source:actor;
                context.World.Deliver(new IEffect[]{new DamageEffect{Flat=_damage,Coeff=0,IgnoreDef=true,CanCrit=false,IncludeBonuses=false,FireOnHurted=false}},ignition,actor,0,point,origin:context.Origin);
            }
        }
    }
}
