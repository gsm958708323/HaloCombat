using System;

namespace Combat.Core
{
    public readonly struct EvAoeAbsorbed
    {
        public readonly EntityId Area,Projectile,Owner;
        public readonly int Count;
        public readonly float Radius;
        public readonly SimVec3 Velocity;
        public readonly SkillOrigin Origin;
        public EvAoeAbsorbed(EntityId area,EntityId projectile,EntityId owner,int count,float radius,SimVec3 velocity,SkillOrigin origin)
        {Area=area;Projectile=projectile;Owner=owner;Count=count;Radius=radius;Velocity=velocity;Origin=origin;}
    }

    // These interactions consume runtime bodies; shared definitions remain unchanged.
    internal static class AoeInteractions
    {
        internal static bool Move(CombatWorld world,Actor area,AoeComp body,float dt)
        {
            if(!body.Def.MoveForward)return true;
            var transform=area.GetComp<TransformComp>();var forward=LocomotionComp.ForwardFromYaw(transform.YawDegrees);
            var desired=new SimVec3(transform.Position.X+(forward.X*body.Def.MoveSpeed+body.Velocity.X)*dt,transform.Position.Y,
                transform.Position.Z+(forward.Z*body.Def.MoveSpeed+body.Velocity.Z)*dt);
            var resolved=world.Collision.Constrain(transform.Position,desired,body.Radius,out var hit);
            if(hit.Blocked&&body.Def.RemoveOnObstacle)return false;
            transform.Position=resolved;return true;
        }
        internal static void Absorb(CombatWorld world,Actor area,AoeComp body,Actor owner)
        {
            if(!body.Def.AbsorbProjectiles||!AreaTargets.TryLive(world,body.OwnerId,out _)||!area.TryGetComp<TeamComp>(out var team))return;
            var center=area.GetComp<TransformComp>().Position;
            foreach(var projectile in world.RegistryActive())
            {
                if(body.Ending||!area.IsActive)return;
                if(!projectile.IsActive||!projectile.TryGetComp<ProjectileComp>(out var shot)||shot.Def==null||shot.Exhausted||
                    !projectile.TryGetComp<TeamComp>(out var shotTeam)||team.TeamId!=shotTeam.TeamId)continue;
                var transform=projectile.GetComp<TransformComp>();float radius=body.Radius+shot.Def.HitRadius;
                if(SpatialGeometry.DistanceSquared(center,transform.Position)>radius*radius)continue;
                var forward=LocomotionComp.ForwardFromYaw(transform.YawDegrees);float speed=(float)ProjectileKinematics.Speed(shot);
                var velocity=new SimVec3(forward.X*speed,0,forward.Z*speed);
                body.Absorb(velocity);shot.Exhausted=true;
                var absorbed=new EvAoeAbsorbed(area.Id,projectile.Id,body.OwnerId,body.AbsorbedProjectiles,body.Radius,body.Velocity,body.Origin);
                var ended=new EvProjectileEnded(shot.OwnerId,projectile.Id,ProjectileEndReason.Absorbed,transform.Position);
                world.RequestDespawn(projectile.Id);projectile.SetActive(false);
                world.Events.Publish(ended);world.Events.Publish(absorbed);
                if(body.Ending||!area.IsActive||area.Id!=absorbed.Area)return;
                if(body.Def.CueId!=0)world.Events.Publish(new EvCue(body.Def.CueId,body.OwnerId,"ProjectileAbsorb"));
                AoePulse.DeliverBag(world,body.Def.OnAbsorb,owner,null,body.SnapshotAtk,center,body.Power,body.Origin);
            }
        }
    }
}
