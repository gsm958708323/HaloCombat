using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class TrackedProjectilesComp : Comp
    {
        readonly Dictionary<string,EntityId> _tracked=new Dictionary<string,EntityId>(System.StringComparer.Ordinal);
        public void Track(string key,EntityId id){if(!string.IsNullOrEmpty(key))_tracked[key]=id;}
        public bool TryFind(string key,out Actor projectile)
        {
            projectile=null;
            if(string.IsNullOrEmpty(key)||!_tracked.TryGetValue(key,out var id))return false;
            if(Self.World.TryGetActor(id,out projectile)&&projectile.IsActive&&projectile.TryGetComp<ProjectileComp>(out var body)&&!body.Exhausted&&body.OwnerId==Self.Id)return true;
            _tracked.Remove(key);projectile=null;return false;
        }
        public void ClearIf(string key,EntityId id){if(!string.IsNullOrEmpty(key)&&_tracked.TryGetValue(key,out var stored)&&stored==id)_tracked.Remove(key);}
        protected override void OnDetach()=>_tracked.Clear();
    }
    public readonly struct EvProjectileTeleport
    {
        public readonly EntityId Source,Projectile;
        public readonly string Key,Result;
        public readonly SimVec3 Point;
        public readonly bool Moved;
        public EvProjectileTeleport(EntityId source,EntityId projectile,string key,SimVec3 point,bool moved,string result)
        {Source=source;Projectile=projectile;Key=key;Point=point;Moved=moved;Result=result;}
    }
    public sealed class TeleportTrackedProjectileEffect : IEffect
    {
        readonly string _key;
        public TeleportTrackedProjectileEffect(string key)=>_key=key;
        public void Apply(ref EffectContext context)
        {
            var source=context.Source;
            if(source==null||!source.TryGetComp<TrackedProjectilesComp>(out var tracker)||!tracker.TryFind(_key,out var projectile))return;
            var point=projectile.GetComp<TransformComp>().Position;point.Y=context.World.Motor.GroundY;
            bool moved=source.TryGetComp<LocomotionComp>(out var loco)&&context.World.Collision.IsFree(point,loco.CollisionRadius)&&loco.RequestTeleport(point);
            context.World.Events.Publish(new EvProjectileTeleport(source.Id,projectile.Id,_key,point,moved,moved?"Teleported":"Destination blocked"));
            if(!moved)return;
            tracker.ClearIf(_key,projectile.Id);context.World.RequestDespawn(projectile.Id);
        }
    }
}
