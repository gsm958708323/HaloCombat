using System;
using System.Collections.Generic;

namespace Combat.Core
{
    // A one-step request owns its original generation and clip lifetime. Stopping a
    // timeline before the query phase invalidates it, even if the actor slot is reused.
    readonly struct SpatialHitIntent
    {
        readonly SpatialHitState _state;
        readonly ClipRuntime _runtime;
        readonly EntityId _source;
        public SpatialHitIntent(SpatialHitState state,in ClipRuntime runtime)
        {_state=state;_runtime=runtime;_source=runtime.Self.Id;}
        public void Resolve()
        {if(_runtime.Self.IsActive&&_runtime.Self.Id==_source)_state.Resolve(_runtime);}
    }

    sealed class SpatialHitState
    {
        readonly HashSet<EntityId> _hits=new HashSet<EntityId>();
        bool _active=true,_volumeClosed;
        public bool Blocked {get;private set;}
        public void Resolve(in ClipRuntime runtime)
        {
            var self=runtime.Self;var world=self.World;
            if(!_active||world.IsActorStopped(self))return;
            bool dash=runtime.Clip.Kind==ClipKind.Dash;
            if(dash&&self.GetComp<TagComp>().Has(CommonTags.BlockSkillMotion))return;
            var position=self.GetComp<TransformComp>().Position;
            var origin=dash?self.GetComp<LocomotionComp>().MovementFrom:position;
            float length=dash?(float)Math.Sqrt(SpatialGeometry.DistanceSquared(origin,position)):runtime.Clip.Length;
            float yaw=dash&&length>.000001f?LocomotionComp.YawFromStick(new SimVec3(position.X-origin.X,0,position.Z-origin.Z)):MeleeClipHandler.Yaw(runtime);
            var shape=dash?HitShape.Capsule:runtime.Clip.Shape;
            float angle=dash?360:runtime.Clip.Angle;
            world.Events.Publish(new EvSpatialVolume(self.Id,runtime.Sequence,origin,yaw,shape,length,runtime.Clip.Width,angle,false));
            Apply(runtime,SpatialGeometry.Query(world,self,origin,yaw,shape,length,runtime.Clip.Width,angle),yaw);
            if(dash&&self.GetComp<LocomotionComp>().MovementContact.Blocked){Blocked=true;CloseVolume(runtime);}
        }
        void Apply(in ClipRuntime runtime,EntityId[] targets,float yaw)
        {
            var self=runtime.Self;var world=self.World;
            float attack=self.TryGetComp<AttributeSet>(out var attr)?attr.GetFinal(AttrId.Atk):0;
            foreach(var id in targets)
            {
                if(runtime.Clip.MaxTargets>0&&_hits.Count>=runtime.Clip.MaxTargets)break;
                if(!AreaTargets.TryLive(world,id,out var target)||!_hits.Add(id))continue;
                var point=target.GetComp<TransformComp>().Position;
                world.Intents.Post(new ApplyEffectsIntent(runtime.Clip.OnHit,self.Id,id,attack,0,point,true,runtime.Power,runtime.Origin,
                    LocomotionComp.ForwardFromYaw(yaw),true));
            }
        }
        public void Stop(in ClipRuntime runtime){_active=false;CloseVolume(runtime);}
        void CloseVolume(in ClipRuntime runtime)
        {
            if(_volumeClosed)return;_volumeClosed=true;
            var self=runtime.Self;
            self.World.Events.Publish(new EvSpatialVolume(self.Id,runtime.Sequence,self.GetComp<TransformComp>().Position,0,runtime.Clip.Shape,0,0,0,true));
        }
    }

    public sealed class MeleeClipHandler : IClipHandler
    {
        readonly SpatialHitState _hits=new SpatialHitState();
        public void Open(in ClipRuntime runtime) { }
        public void Tick(in ClipRuntime runtime,float dt)
        {if(dt>0)runtime.Self.World.Intents.Post(new SpatialHitIntent(_hits,runtime));}
        public void Close(in ClipRuntime runtime,bool interrupted)=>_hits.Stop(runtime);
        internal static float Yaw(in ClipRuntime runtime)=>runtime.Aim.HasYaw?runtime.Aim.Yaw:runtime.Self.GetComp<TransformComp>().YawDegrees;
    }

    public sealed class DashClipHandler : IClipHandler
    {
        readonly SpatialHitState _hits=new SpatialHitState();
        public void Open(in ClipRuntime runtime) { }
        public void Tick(in ClipRuntime runtime,float dt)
        {
            if(dt<=0||_hits.Blocked||runtime.Self.GetComp<TagComp>().Has(CommonTags.BlockSkillMotion))return;
            var self=runtime.Self;
            float yaw=MeleeClipHandler.Yaw(runtime);
            var direction=LocomotionComp.ForwardFromYaw(yaw);
            float distance=runtime.Clip.Length*dt/(runtime.Clip.End-runtime.Clip.Start);
            var loco=self.GetComp<LocomotionComp>();loco.SetClipSteer(0);
            loco.RequestSkillDelta(direction.X*distance,0,direction.Z*distance,"dash");
            self.World.Intents.Post(new SpatialHitIntent(_hits,runtime));
        }
        public void Close(in ClipRuntime runtime,bool interrupted)
        {
            _hits.Stop(runtime);
            runtime.Self.GetComp<LocomotionComp>().ClearClipSteer();
            if(interrupted)runtime.Self.GetComp<LocomotionComp>().ClearPendingSkill();
        }
    }
}
