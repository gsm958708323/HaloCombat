using System;
using System.Collections.Generic;

namespace Combat.Core
{
    // One contact owns these requests. Effects submit decisions before the delivery
    // service decides whether to end, continue straight, or redirect the projectile.
    public sealed class DeliveryContact
    {
        public int HitIndex {get;}
        public bool Continue {get;private set;}
        public DeliveryContact(int hitIndex){HitIndex=hitIndex;}
        public void RequestPierce(int maximum)
        {if(maximum==0||HitIndex<maximum)Continue=true;}
    }

    public sealed class PierceEffect : IEffect
    {
        public int MaxHits {get;}
        public PierceEffect(int maxHits=0)
        {if(maxHits<0)throw new ArgumentOutOfRangeException(nameof(maxHits));MaxHits=maxHits;}
        public void Apply(ref EffectContext context)=>context.Contact?.RequestPierce(MaxHits);
    }

    public sealed class ChainProjectileEffect : IEffect
    {
        public int Count {get;}
        public float Radius {get;}
        public ChainTargetMode Mode {get;}
        public ChainProjectileEffect(int count,float radius,ChainTargetMode mode=ChainTargetMode.Nearest)
        {Count=count;Radius=radius;Mode=mode;}
        public void Apply(ref EffectContext context)
        {
            // Selection is deferred until all contact effects ran. Piercing has
            // precedence for the current segment, independent of node ordering.
            if(context.ProjectileHit!=null&&context.ProjectileHit.Redirect==null)
                context.ProjectileHit.Redirect=this;
        }
        internal bool Redirect(CombatWorld world,Actor projectile,ProjectileComp body,out ProjectileEndReason reason)
        {
            reason=ProjectileEndReason.HitLimit;
            if(body.Returning||!body.CanChain(this))return false;
            world.TryGetActor(body.OwnerId,out var owner);
            var from=projectile.GetComp<TransformComp>().Position;
            var candidates=new List<EntityId>();
            foreach(var id in AreaTargets.Query(world,from,Radius,owner,body.Def.HostileMask,0))
                if(!body.HasHit(id)&&AreaTargets.TryLive(world,id,out var actor)&&
                    !world.Collision.Trace(from,actor.GetComp<TransformComp>().Position,body.Def.HitRadius).Blocked)candidates.Add(id);
            candidates.Sort((a,b)=>
            {
                if(Mode==ChainTargetMode.Nearest)
                {
                    world.TryGetActor(a,out var aa);world.TryGetActor(b,out var bb);
                    int distance=SpatialGeometry.DistanceSquared(from,aa.GetComp<TransformComp>().Position).CompareTo(SpatialGeometry.DistanceSquared(from,bb.GetComp<TransformComp>().Position));
                    if(distance!=0)return distance;
                }
                int index=a.Index.CompareTo(b.Index);return index!=0?index:a.Generation.CompareTo(b.Generation);
            });
            if(candidates.Count==0){reason=ProjectileEndReason.ChainExhausted;return false;}
            int selected=Mode==ChainTargetMode.Random?Math.Min(candidates.Count-1,(int)(world.Random.Next01()*candidates.Count)):0;
            var next=candidates[selected];if(!AreaTargets.TryLive(world,next,out var target))return false;
            body.ConsumeChain(this);body.LegHits=0;body.HomingTarget=next;
            var point=target.GetComp<TransformComp>().Position;
            projectile.GetComp<TransformComp>().YawDegrees=LocomotionComp.YawFromStick(new SimVec3(point.X-from.X,0,point.Z-from.Z));
            world.Events.Publish(new EvProjectileBehavior(body.OwnerId,projectile.Id,"chain",from,next,1,body.Generation,body.Family.Origin));
            return true;
        }
    }
}
