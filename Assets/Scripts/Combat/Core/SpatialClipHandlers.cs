using System.Collections.Generic;

namespace Combat.Core
{
    sealed class SpatialHitState
    {
        readonly HashSet<EntityId> _hits = new HashSet<EntityId>();
        public void Apply(in ClipRuntime runtime, EntityId[] targets)
        {
            var self = runtime.Self; var world = self.World;
            float attack = self.TryGetComp<AttributeSet>(out var attr) ? attr.GetFinal(AttrId.Atk) : 0;
            foreach(var id in targets)
            {
                if(runtime.Clip.MaxTargets>0&&_hits.Count>=runtime.Clip.MaxTargets)break;
                if(!AreaTargets.TryLive(world,id,out var target)||!_hits.Add(id))continue;
                var point = target.GetComp<TransformComp>().Position;
                world.Intents.Post(new ApplyEffectsIntent(runtime.Clip.OnHit,self.Id,id,attack,0,point,true,runtime.Power,runtime.Origin,
                    LocomotionComp.ForwardFromYaw(MeleeClipHandler.Yaw(runtime)),true));
            }
        }
    }

    public sealed class MeleeClipHandler : IClipHandler
    {
        readonly SpatialHitState _hits = new SpatialHitState();
        public void Open(in ClipRuntime runtime) => Publish(runtime,false);
        public void Tick(in ClipRuntime runtime,float dt)
        {
            if(dt<=0)return;
            var self=runtime.Self; var origin=self.GetComp<TransformComp>().Position;
            float yaw=Yaw(runtime);
            Publish(runtime,false);
            _hits.Apply(runtime,SpatialGeometry.Query(self.World,self,origin,yaw,runtime.Clip.Shape,runtime.Clip.Length,runtime.Clip.Width,runtime.Clip.Angle));
        }
        public void Close(in ClipRuntime runtime,bool interrupted) => Publish(runtime,true);
        internal static float Yaw(in ClipRuntime runtime) => runtime.Aim.HasPoint?runtime.Aim.Yaw:runtime.Self.GetComp<TransformComp>().YawDegrees;
        static void Publish(in ClipRuntime runtime,bool closed)
        {
            var origin=runtime.Self.GetComp<TransformComp>().Position;
            runtime.Self.World.Events.Publish(new EvSpatialVolume(runtime.Self.Id,runtime.Sequence,origin,Yaw(runtime),runtime.Clip.Shape,
                runtime.Clip.Length,runtime.Clip.Width,runtime.Clip.Angle,closed));
        }
    }

    public sealed class DashClipHandler : IClipHandler
    {
        readonly SpatialHitState _hits = new SpatialHitState();
        bool _blocked,_closed;
        public void Open(in ClipRuntime runtime) { }
        public void Tick(in ClipRuntime runtime,float dt)
        {
            if(dt<=0||_blocked||runtime.Self.GetComp<TagComp>().Has(CommonTags.BlockSkillMotion))return;
            var self=runtime.Self;var origin=self.GetComp<TransformComp>().Position;
            float yaw=MeleeClipHandler.Yaw(runtime);
            var direction=LocomotionComp.ForwardFromYaw(yaw);
            float distance=runtime.Clip.Length*dt/(runtime.Clip.End-runtime.Clip.Start);
            var loco=self.GetComp<LocomotionComp>();loco.SetClipSteer(0);
            var destination=new SimVec3(origin.X+direction.X*distance,origin.Y,origin.Z+direction.Z*distance);
            destination=self.World.Collision.Constrain(origin,destination,loco.CollisionRadius,out var obstacle);
            distance=(float)System.Math.Sqrt(SpatialGeometry.DistanceSquared(origin,destination));
            loco.RequestSkillDelta(destination.X-origin.X,0,destination.Z-origin.Z);
            self.World.Events.Publish(new EvSpatialVolume(self.Id,runtime.Sequence,origin,yaw,HitShape.Capsule,distance,runtime.Clip.Width,360,false));
            _hits.Apply(runtime,SpatialGeometry.Query(self.World,self,origin,yaw,HitShape.Capsule,distance,runtime.Clip.Width,360));
            if(obstacle.Blocked)
            {
                _blocked=true;self.World.Events.Publish(new EvObstacleHit(self.Id,self.Id,obstacle.Id,obstacle.Point,obstacle.Normal,"dash"));
                CloseVolume(runtime);
            }
        }
        public void Close(in ClipRuntime runtime,bool interrupted)
        {
            var self=runtime.Self;
            CloseVolume(runtime);
            self.GetComp<LocomotionComp>().ClearClipSteer();
            if(interrupted)self.GetComp<LocomotionComp>().ClearPendingSkill();
        }
        void CloseVolume(in ClipRuntime runtime)
        {if(_closed)return;_closed=true;var self=runtime.Self;self.World.Events.Publish(new EvSpatialVolume(self.Id,runtime.Sequence,self.GetComp<TransformComp>().Position,0,HitShape.Capsule,0,0,0,true));}
    }
}
