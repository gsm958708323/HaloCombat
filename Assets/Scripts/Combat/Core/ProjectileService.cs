using System;
using System.Collections.Generic;

namespace Combat.Core
{
    readonly struct ProjectileEndIntent
    {
        public readonly EntityId Source,Id;
        public readonly ProjectileDefinition Definition;
        public readonly ProjectileEndReason Reason;
        public readonly SimVec3 Point,Direction;
        public readonly float Attack,Power;
        public readonly SkillOrigin Origin;
        public ProjectileEndIntent(EntityId source,EntityId id,ProjectileDefinition definition,ProjectileEndReason reason,SimVec3 point,SimVec3 direction,float attack,float power,SkillOrigin origin)
        {Source=source;Id=id;Definition=definition;Reason=reason;Point=point;Direction=direction;Attack=attack;Power=power;Origin=origin;}
    }

    public sealed class ProjectileService
    {
        public const int MaxAlive=512;
        readonly CombatWorld _world;
        public ProjectileService(CombatWorld world)=>_world=world??throw new ArgumentNullException(nameof(world));
        public void Tick(float dt){DrainSpawns();MoveAndHit(Math.Max(0,dt));}

        void DrainSpawns()
        {
            int alive=0;
            foreach(var actor in _world.RegistryActive())if(actor.TryGetComp<ProjectileComp>(out _))alive++;
            _world.Intents.Drain<SpawnProjectileIntent>(intent=>
            {
                if(!_world.Projectiles.TryGet(intent.SpecId,out var def))return;
                if(!AreaTargets.TryLive(_world,intent.Owner,out var owner))return;
                var seed=intent.Seed;
                if(seed==null)
                {
                    var origin=owner.TryGetComp<SkillDirectorComp>(out var director)?director.CurrentOrigin:default;
                    var family=new ProjectileFamily(origin);family.Reserve();
                    seed=new ProjectileSeed(family,0,0,false,null);
                }
                if(alive>=MaxAlive)
                {_world.Events.Publish(new EvProjectileBehavior(intent.Owner,EntityId.Invalid,"rejected",intent.Origin,EntityId.Invalid,0,seed.Generation,seed.Family.Origin));return;}
                var id=_world.SpawnActor(new ActorSpawnSpec("projectile"),false);
                if(!_world.TryGetActor(id,out var projectile))return;
                alive++;
                var transform=projectile.GetComp<TransformComp>();var direction=LocomotionComp.ForwardFromYaw(intent.Yaw);
                float forward=seed.AtImpact?0:def.SpawnForward;
                float height=seed.AtImpact?intent.Origin.Y:intent.Origin.Y+(def.Motion?.Bounce?.LaunchHeight??0);
                var spawn=new SimVec3(intent.Origin.X+direction.X*forward,height,intent.Origin.Z+direction.Z*forward);
                var obstacle=_world.Collision.Trace(new SimVec3(intent.Origin.X,height,intent.Origin.Z),spawn,def.HitRadius);
                transform.Position=obstacle.Blocked?obstacle.Point:spawn;transform.YawDegrees=intent.Yaw;
                SpawnAoeEffect.CopyTeam(owner,projectile);
                float attack=def.SnapshotAtk?intent.SnapshotAtk:owner.TryGetComp<AttributeSet>(out var attributes)?attributes.GetFinal(AttrId.Atk):0;
                var body=projectile.GetComp<ProjectileComp>();body.Setup(def,intent.Owner,attack,intent.Target,intent.Power,seed);
                if(owner.TryGetComp<TrackedProjectilesComp>(out var tracker))tracker.Track(def.TrackingKey,id);
                _world.PublishSpawn(id,"projectile");
                if(obstacle.Blocked){Wall(projectile,body,obstacle);if(body.Exhausted)alive--;}
            });
        }

        void MoveAndHit(float dt)
        {
            // Newly emitted children are only drained at the next tick.
            foreach(var projectile in _world.RegistryActive())
            {
                if(!projectile.IsActive||_world.IsActorStopped(projectile)||!projectile.TryGetComp<ProjectileComp>(out var body)||body.Def==null||body.Exhausted)continue;
                _world.TryGetActor(body.OwnerId,out var owner);
                Steer(body,projectile.GetComp<TransformComp>(),dt,owner);
                Move(projectile,body,owner,dt);
            }
        }

        void Move(Actor projectile,ProjectileComp body,Actor owner,float dt)
        {
            var def=body.Def;var transform=projectile.GetComp<TransformComp>();
            ProjectileKinematics.SampleStep(body);
            double timeLeft=Math.Min(dt,Math.Max(0,def.Lifetime-body.Age));
            while(projectile.IsActive&&!body.Exhausted)
            {
                if(body.Distance+.000001>=body.RangeLimit){Terminate(projectile,body,ProjectileEndReason.Range);return;}
                if(body.Age+.000001>=def.Lifetime){Terminate(projectile,body,ProjectileEndReason.Lifetime);return;}
                var motion=def.Motion;
                if(!body.Returning&&motion!=null&&motion.ReturnAfter>=0&&body.Age+.0000001>=motion.ReturnAfter)
                {
                    body.Returning=true;FaceOwner(transform,owner);
                    _world.Events.Publish(new EvProjectileBehavior(body.OwnerId,projectile.Id,"returning",transform.Position,body.OwnerId,1,body.Generation,body.Family.Origin));
                }
                double groundTime=ProjectileKinematics.GroundTime(body,transform,_world.Motor.GroundY);
                if(groundTime<=.00000001){if(!Ground(projectile,body,owner))return;continue;}
                if(timeLeft<=.000000001)return;
                double elapsed=Math.Min(timeLeft,groundTime);
                if(!body.Returning&&motion!=null&&motion.ReturnAfter>=0)elapsed=Math.Min(elapsed,Math.Max(0,motion.ReturnAfter-body.Age));
                double distance=Math.Min(ProjectileKinematics.Distance(body,elapsed),Math.Max(0,body.RangeLimit-body.Distance));
                elapsed=ProjectileKinematics.Time(body,distance,elapsed);
                if(distance<=.000000001)elapsed=Math.Min(timeLeft,groundTime);
                var direction=LocomotionComp.ForwardFromYaw(transform.YawDegrees);var from=transform.Position;
                var to=ProjectileKinematics.Position(body,from,direction,elapsed,distance);
                var obstacle=_world.Collision.Trace(from,to,def.HitRadius);
                float catchFraction=2;
                if(body.Returning&&owner!=null&&CollisionGeometry.CircleEntry(from,to,owner.GetComp<TransformComp>().Position,motion.CatchRadius,out var caught))catchFraction=caught;
                float limit=obstacle.Blocked?Math.Min(obstacle.Fraction,catchFraction):Math.Min(1,catchFraction);
                double spentDistance=0,spentTime=0;
                bool redirected=false;
                foreach(var hit in LineTargeting.Query(_world,owner,from,to,def.HitRadius,def.HostileMask))
                {
                    if(hit.Fraction>=limit-.000001f&&(obstacle.Blocked||catchFraction<=1))break;
                    if(!AreaTargets.TryLive(_world,hit.Target,out var victim))continue;
                    double path=distance*hit.Fraction-spentDistance;
                    double time=ProjectileKinematics.Time(body,path,elapsed-spentTime);
                    if(!body.CanHit(hit.Target,body.Age+time))continue;
                    if(def.PassInvulnerable&&(victim.GetComp<TagComp>().Has(CommonTags.Invincible)||victim.TryGetComp<HealthComp>(out var health)&&health.InIFrame))
                    {if(owner!=null&&owner.TryGetComp<SkillDirectorComp>(out var director))director.GrantComboConfirm(victim,body.Family.Origin);continue;}
                    ProjectileKinematics.Advance(body,transform,transform.Position,direction,time,path);
                    if(!body.TryRecord(hit.Target))continue;
                    spentDistance+=path;spentTime+=time;timeLeft-=time;body.HitCount++;body.LegHits++;
                    float attack=Attack(body,owner);
                    var context=new ProjectileHitContext(projectile,body,transform.Position,transform.YawDegrees,attack);
                    _world.Deliver(def.OnHit,owner,victim,attack,transform.Position,direction,power:body.Power,origin:body.Family.Origin,projectileHit:context);
                    if(!projectile.IsActive||body.Exhausted)return;
                    if(def.MaxHits<=0||body.LegHits<def.MaxHits)continue;
                    if(body.Distance+.000001>=body.RangeLimit){Terminate(projectile,body,ProjectileEndReason.HitLimit);return;}
                    if(body.ChainsLeft<=0||body.Returning){Terminate(projectile,body,ProjectileEndReason.HitLimit);return;}
                    var next=ChainTarget(body,owner,transform.Position);
                    if(!next.IsValid){Terminate(projectile,body,ProjectileEndReason.ChainExhausted);return;}
                    if(!AreaTargets.TryLive(_world,next,out var target))continue;
                    body.ChainsLeft--;body.LegHits=0;body.HomingTarget=next;
                    var point=target.GetComp<TransformComp>().Position;
                    transform.YawDegrees=LocomotionComp.YawFromStick(new SimVec3(point.X-transform.Position.X,0,point.Z-transform.Position.Z));
                    _world.Events.Publish(new EvProjectileBehavior(body.OwnerId,projectile.Id,"chain",transform.Position,next,1,body.Generation,body.Family.Origin));
                    redirected=true;break;
                }
                if(redirected)continue;
                double tailDistance=Math.Max(0,distance*limit-spentDistance);
                double tailTime=limit<1?ProjectileKinematics.Time(body,tailDistance,elapsed-spentTime):Math.Max(0,elapsed-spentTime);
                ProjectileKinematics.Advance(body,transform,transform.Position,direction,tailTime,tailDistance);timeLeft-=tailTime;
                if(obstacle.Blocked&&obstacle.Fraction<=catchFraction){if(Wall(projectile,body,obstacle))continue;return;}
                if(catchFraction<=1){Terminate(projectile,body,ProjectileEndReason.Returned);return;}
                if(body.Distance+.000001>=body.RangeLimit){Terminate(projectile,body,ProjectileEndReason.Range);return;}
                if(body.Age+.000001>=def.Lifetime){Terminate(projectile,body,ProjectileEndReason.Lifetime);return;}
                if(groundTime<=elapsed+.00000001){if(!Ground(projectile,body,owner))return;continue;}
            }
        }

        float Attack(ProjectileComp body,Actor owner)=>body.Def.SnapshotAtk?body.SnapshotAtk:
            owner!=null&&owner.TryGetComp<AttributeSet>(out var attr)?attr.GetFinal(AttrId.Atk):body.SnapshotAtk;

        EntityId ChainTarget(ProjectileComp body,Actor owner,SimVec3 from)
        {
            var definition=body.Def.Chain;
            if(definition==null)return EntityId.Invalid;
            var candidates=new List<EntityId>();
            foreach(var id in AreaTargets.Query(_world,from,definition.Radius,owner,body.Def.HostileMask,0))
                if(!body.HasHit(id)&&AreaTargets.TryLive(_world,id,out var actor)&&
                    !_world.Collision.Trace(from,actor.GetComp<TransformComp>().Position,body.Def.HitRadius).Blocked)candidates.Add(id);
            candidates.Sort((a,b)=>
            {
                if(definition.Mode==ChainTargetMode.Nearest)
                {
                    _world.TryGetActor(a,out var aa);_world.TryGetActor(b,out var bb);
                    int distance=SpatialGeometry.DistanceSquared(from,aa.GetComp<TransformComp>().Position).CompareTo(SpatialGeometry.DistanceSquared(from,bb.GetComp<TransformComp>().Position));
                    if(distance!=0)return distance;
                }
                int index=a.Index.CompareTo(b.Index);return index!=0?index:a.Generation.CompareTo(b.Generation);
            });
            if(candidates.Count==0)return EntityId.Invalid;
            int selected=definition.Mode==ChainTargetMode.Random?Math.Min(candidates.Count-1,(int)(_world.Random.Next01()*candidates.Count)):0;
            return candidates[selected];
        }

        void Steer(ProjectileComp body,TransformComp transform,float dt,Actor owner)
        {
            if(body.Returning){FaceOwner(transform,owner);return;}
            var def=body.Def;if(def.HomingRate<=0)return;
            if(!body.HomingTarget.IsValid)body.HomingTarget=Acquire(transform.Position,owner,body);
            else if(!AreaTargets.TryLive(_world,body.HomingTarget,out _))
            {if(!def.HomingRetarget)return;body.HomingTarget=Acquire(transform.Position,owner,body);}
            if(!AreaTargets.TryLive(_world,body.HomingTarget,out var target))return;
            var point=target.GetComp<TransformComp>().Position;
            float wanted=LocomotionComp.YawFromStick(new SimVec3(point.X-transform.Position.X,0,point.Z-transform.Position.Z));
            float delta=wanted-transform.YawDegrees;while(delta>180)delta-=360;while(delta< -180)delta+=360;
            float step=def.HomingRate*dt;if(def.HomingMaxTurn>0)step=Math.Min(step,def.HomingMaxTurn);
            transform.YawDegrees+=Math.Max(-step,Math.Min(step,delta));
        }
        EntityId Acquire(SimVec3 point,Actor owner,ProjectileComp body)
        {
            var def=body.Def;var targets=AreaTargets.Query(_world,point,def.HomingAcquireRadius,owner,def.HostileMask,0);
            float best=float.MaxValue;EntityId result=EntityId.Invalid;
            foreach(var id in targets)
                if(!body.HasHit(id)&&AreaTargets.TryLive(_world,id,out var target))
                {float distance=SpatialGeometry.DistanceSquared(point,target.GetComp<TransformComp>().Position);if(distance<best){best=distance;result=id;}}
            return result;
        }

        static void FaceOwner(TransformComp transform,Actor owner)
        {
            if(owner==null)return;var position=owner.GetComp<TransformComp>().Position;
            transform.YawDegrees=LocomotionComp.YawFromStick(new SimVec3(position.X-transform.Position.X,0,position.Z-transform.Position.Z));
        }
        bool Ground(Actor projectile,ProjectileComp body,Actor owner)
        {
            var transform=projectile.GetComp<TransformComp>();var point=transform.Position;point=new SimVec3(point.X,_world.Motor.GroundY,point.Z);transform.Position=point;
            double speed=Math.Max(0,-body.VerticalSpeed)*body.Def.Motion.Bounce.Restitution;
            if(body.GroundBouncesLeft<=0||speed<.0001){Terminate(projectile,body,ProjectileEndReason.Ground);return false;}
            body.GroundBouncesLeft--;body.VerticalSpeed=speed;
            BounceEffects(projectile,body,owner,"ground-bounce",point);return projectile.IsActive&&!body.Exhausted;
        }
        bool Wall(Actor projectile,ProjectileComp body,ObstacleHit obstacle)
        {
            var transform=projectile.GetComp<TransformComp>();var direction=LocomotionComp.ForwardFromYaw(transform.YawDegrees);
            bool reflect=body.WallBouncesLeft>0&&SpatialGeometry.DistanceSquared(default,obstacle.Normal)>.5f;
            _world.Events.Publish(new EvObstacleHit(body.OwnerId,projectile.Id,obstacle.Id,transform.Position,obstacle.Normal,reflect?"projectile-bounce":"projectile"));
            if(!reflect){Terminate(projectile,body,ProjectileEndReason.Wall);return false;}
            body.WallBouncesLeft--;float dot=direction.X*obstacle.Normal.X+direction.Z*obstacle.Normal.Z;
            transform.YawDegrees=LocomotionComp.YawFromStick(new SimVec3(direction.X-2*dot*obstacle.Normal.X,0,direction.Z-2*dot*obstacle.Normal.Z));
            var point=transform.Position;transform.Position=new SimVec3(point.X+obstacle.Normal.X*.0001f,point.Y,point.Z+obstacle.Normal.Z*.0001f);
            _world.TryGetActor(body.OwnerId,out var owner);BounceEffects(projectile,body,owner,"wall-bounce",point);return projectile.IsActive&&!body.Exhausted;
        }
        void BounceEffects(Actor projectile,ProjectileComp body,Actor owner,string kind,SimVec3 point)
        {
            _world.Events.Publish(new EvProjectileBehavior(body.OwnerId,projectile.Id,kind,point,EntityId.Invalid,1,body.Generation,body.Family.Origin));
            _world.Deliver(body.Def.OnBounce,owner,null,Attack(body,owner),point,LocomotionComp.ForwardFromYaw(projectile.GetComp<TransformComp>().YawDegrees),power:body.Power,origin:body.Family.Origin);
        }
        void Terminate(Actor projectile,ProjectileComp body,ProjectileEndReason reason)
        {
            if(body.Exhausted)return;body.Exhausted=true;
            var transform=projectile.GetComp<TransformComp>();
            _world.Intents.Post(new ProjectileEndIntent(body.OwnerId,projectile.Id,body.Def,reason,transform.Position,
                LocomotionComp.ForwardFromYaw(transform.YawDegrees),Attack(body,_world.TryGetActor(body.OwnerId,out var owner)?owner:null),body.Power,body.Family.Origin));
            _world.RequestDespawn(projectile.Id);projectile.SetActive(false);
        }

        public void FinishTerminations()
        {
            _world.Intents.Drain<ProjectileEndIntent>(end=>
            {
                _world.TryGetActor(end.Source,out var owner);
                _world.Events.Publish(new EvProjectileEnded(end.Source,end.Id,end.Reason,end.Point));
                var effects=end.Reason==ProjectileEndReason.Wall?end.Definition.OnWall:
                    end.Reason==ProjectileEndReason.Range?end.Definition.OnRange:
                    end.Reason==ProjectileEndReason.Lifetime?end.Definition.OnExpire:
                    end.Reason==ProjectileEndReason.Returned?end.Definition.OnReturned:
                    end.Reason==ProjectileEndReason.Ground?end.Definition.OnGround:null;
                _world.Deliver(effects,owner,null,end.Attack,end.Point,end.Direction,power:end.Power,origin:end.Origin);
                var context=new EffectContext{World=_world,Source=owner,SnapshotAtk=end.Attack,Point=end.Point,HasPoint=true,
                    Dir=end.Direction,HasDir=true,Power=end.Power,Origin=end.Origin};
                if(end.Reason==ProjectileEndReason.Wall)_world.DispatchSkillEvent(SkillEventKind.Wall,context);
                if(end.Reason==ProjectileEndReason.Range)_world.DispatchSkillEvent(SkillEventKind.Range,context);
                _world.Deliver(end.Definition.OnEnd,owner,null,end.Attack,end.Point,end.Direction,power:end.Power,origin:end.Origin);
                _world.DispatchSkillEvent(SkillEventKind.ProjectileEnd,context);
            });
        }
    }
}
