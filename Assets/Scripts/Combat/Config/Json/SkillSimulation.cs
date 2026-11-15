using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    // One simulation implementation is used by the local host and the Unity test scene.
    public sealed partial class SkillSimulation : IDisposable
    {
        public const float Step = 0.02f;
        readonly CombatContentSnapshot _content;
        public IReadOnlyDictionary<string,RuntimeViewDefinition> Views=>_content.Runtime?.Views;
        readonly Dictionary<long, string> _names = new Dictionary<long, string>();
        readonly Dictionary<long, string> _kinds = new Dictionary<long, string>();
        readonly Dictionary<long,(HitFeedbackLevel Level,int Step)> _hurts=new Dictionary<long,(HitFeedbackLevel,int)>();
        readonly List<SkillEventSnapshot> _events = new List<SkillEventSnapshot>();
        readonly List<SkillTrailSnapshot> _trails = new List<SkillTrailSnapshot>();
        readonly List<SkillAreaSnapshot> _areas = new List<SkillAreaSnapshot>();
        readonly List<SkillRaySnapshot> _rays=new List<SkillRaySnapshot>();
        readonly Dictionary<int,SkillSpatialSnapshot> _volumes = new Dictionary<int,SkillSpatialSnapshot>();
        readonly Dictionary<string, EntityId> _sceneActors = new Dictionary<string, EntityId>();
        readonly Dictionary<EntityId,string> _sceneIds=new Dictionary<EntityId,string>();
        readonly CombatWorld _world;
        readonly EntityId _caster;
        int _eventSequence, _steps, _casts, _spawned, _hits, _spawnedAreas,_spawnedSummons,_spawnedConstructs;
        float _damage;
        int _chains,_splits;
        bool _disposed;
        SimVec3 _movement;

        public SkillSimulation(SkillProjectDocument project, CombatContentSnapshot content)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            var preview = project?.Preview;
            SkillSceneValidation.Validate(preview);
            InitializeRecording(project);
            _trace=new AuthorExecutionTrace(content.Sources);
            _world = new CombatWorld(new SkillSimulationFactory(content, preview), random: new SeededRandom(preview.Seed), cues: content.Runtime?.Data.Cues??content.Cues,motor:content.Runtime?.Data.Motor,obstacles:SkillSceneValidation.Obstacles(preview),skillEvents:content.SkillEvents,effectObserver:_trace);
            _trace.Executing+=e=>Record("execute",Name(e.Source),e.Node.NodeKind,e.Origin,node:e.Node,sourceId:e.Source,targetId:e.Target);
            _world.ReplaceCatalogs(content.Projectiles, content.Aoes, content.Summons);
            _world.ReplaceConstructs(content.Constructs);
            _world.Events.Subscribe<EvEntitySpawn>(OnSpawn);
            _world.Events.Subscribe<EvDamage>(OnDamage);
            _world.Events.Subscribe<EvHurt>(e=>{_hurts[HitboxComp.Pack(e.Target)]=(e.Feedback,_steps);Record("hurt",Name(e.Target),e.Feedback.ToString().ToLowerInvariant(),e.Origin);});
            _world.Events.Subscribe<EvHitstop>(e=>Record("hitstop",Name(e.Source),e.Scope==HitstopScope.World?"World "+e.Frames+" frames":"Source "+e.SourceFrames+" / target "+e.TargetFrames));
            _world.Events.Subscribe<EvComboConfirmation>(e=>Record("combo-confirmation",Name(e.Source),e.Result,e.Origin));
            _world.Events.Subscribe<EvComboResolved>(e=>Record(e.Resolved?"combo-resolved":"combo-unmatched",Name(e.Source),e.Input+" -> "+(e.Entry??"None")+(e.ConsumedConfirmation?" / confirmation consumed":""),e.Origin));
            _world.Events.Subscribe<EvSkillEvent>(e=>Record(e.Rejected?"skill-event-rejected":"skill-"+e.Kind.ToString().ToLowerInvariant(),Name(e.Source),Name(e.Target),e.Origin,e.Damage));
            _world.Events.Subscribe<EvHeal>(e=>Record("heal",Name(e.Target),e.Amount.ToString("F1",System.Globalization.CultureInfo.InvariantCulture)));
            _world.Events.Subscribe<EvShield>(e=>Record("shield",Name(e.Target),e.Amount.ToString("F1",System.Globalization.CultureInfo.InvariantCulture),e.Origin));
            _world.Events.Subscribe<EvBuffLifecycle>(OnBuff);
            _world.Events.Subscribe<EvSkillOperation>(e=>
            {_content.Skills.TryGet(e.Skill,out var skill);Record("operation",Name(e.Source),skill?.Name??e.Skill.ToString(),new SkillOrigin(e.Skill,0,caster:e.Source),node:_content.Sources.Definition("skill",e.Skill.Value),sourceId:e.Source);});
            _world.Events.Subscribe<EvSkillStarted>(e=>
            {_casts++;_content.Skills.TryGet(e.Skill,out var skill);Record("cast",Name(e.Source),skill?.Name??e.Skill.ToString(),OriginOf(e.Source),node:_content.Sources.Definition("skill",e.Skill.Value),sourceId:e.Source);});
            _world.Events.Subscribe<EvSummonLifecycle>(e=>Record("summon-"+e.Kind.ToString().ToLowerInvariant(),Name(e.Owner),e.Detail??Name(e.Id)));
            _world.Events.Subscribe<EvConstructLifecycle>(e=>Record("construct-"+e.Kind.ToString().ToLowerInvariant(),Name(e.Owner),e.Detail+" / "+Name(e.Id),e.Origin,node:_content.Sources.Definition("construct",e.SpecId),sourceId:e.Owner,bodyId:e.Id));
            _world.Events.Subscribe<EvEntityCleanup>(OnCleanup);
            _world.Events.Subscribe<EvAreaResolved>(OnArea);
            _world.Events.Subscribe<EvSpatialVolume>(OnVolume);
            _world.Events.Subscribe<EvRayResolved>(OnRay);
            _world.Events.Subscribe<EvObstacleHit>(e=>Record("collision",Name(e.Source),e.Kind+" -> "+e.Obstacle,node:BodySource(e.Body),sourceId:e.Source,bodyId:e.Body));
            _world.Events.Subscribe<EvProjectileEnded>(e=>Record("projectile-end",Name(e.Source),e.Reason.ToString(),OriginOf(e.Id),node:BodySource(e.Id),sourceId:e.Source,bodyId:e.Id));
            _world.Events.Subscribe<EvProjectileTeleport>(e=>Record(e.Moved?"projectile-teleport":"projectile-teleport-blocked",Name(e.Source),e.Key+" / "+e.Result));
            _world.Events.Subscribe<EvAmmoChanged>(e=>Record("ammunition",Name(e.Source),e.Reason+" "+e.Delta+" -> "+e.Current+"/"+e.Capacity));
            _world.Events.Subscribe<EvProjectileBehavior>(e=>
            {
                if(e.Kind=="chain")_chains++;if(e.Kind=="split")_splits++;
                Record("projectile-"+e.Kind,Name(e.Source),e.Kind=="chain"||e.Kind=="returning"?Name(e.Target):
                    e.Kind=="split"||e.Kind=="rejected"?e.Count+" queued / generation "+e.Generation:e.Kind+" / y="+e.Point.Y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),e.Origin,node:BodySource(e.Projectile),sourceId:e.Source,targetId:e.Target,bodyId:e.Projectile);
            });
            _world.Events.Subscribe<EvAoeLifecycle>(e => Record("area-" + e.Kind.ToString().ToLowerInvariant(), Name(e.Source), Name(e.Target.IsValid ? e.Target : e.Id),OriginOf(e.Id),node:BodySource(e.Id),sourceId:e.Source,targetId:e.Target,bodyId:e.Id));
            _world.Events.Subscribe<EvAoeAbsorbed>(e=>Record("area-absorbed",Name(e.Area),Name(e.Projectile)+" / count="+e.Count+" / radius="+e.Radius.ToString("F3",System.Globalization.CultureInfo.InvariantCulture),e.Origin));
            _world.Events.Subscribe<EvSkillEnded>(OnSkillEnded);
            _world.Events.Subscribe<EvSkillReleased>(e => Record("released", Name(e.Source), "Power " + e.Power.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
            _world.Events.Subscribe<EvCue>(e => Record(e.Playback==CuePlayback.Once?"cue":"cue-"+e.Playback.ToString().ToLowerInvariant(), Name(e.Source), e.CueId+(e.InstanceKey==null?"":" / "+e.InstanceKey),e.Origin));
            _caster = Spawn("caster", preview.Caster);
            foreach (var target in preview.Targets) Spawn("target", target);
        }

        EntityId Spawn(string blueprint, PreviewActorDocument initial)
        {
            var id = _world.SpawnActor(new ActorSpawnSpec(blueprint), false);
            _world.TryGetActor(id, out var actor);
            actor.GetComp<TransformComp>().Position = new SimVec3(initial.X, 0, initial.Z);
            actor.GetComp<LocomotionComp>().CollisionRadius=initial.CollisionRadius;
            if(initial.Team.HasValue)actor.GetComp<TeamComp>().SetTeam(initial.Team.Value);
            var attr = actor.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, initial.Hp); attr.SetBase(AttrId.Hp, initial.Hp);
            attr.SetBase(AttrId.Atk, initial.Atk); attr.SetBase(AttrId.Def, initial.Def);
            attr.SetBase(AttrId.Shield,initial.Shield);attr.SetBase(AttrId.DmgDealMul,initial.DamageDealt);attr.SetBase(AttrId.DmgTakenMul,initial.DamageTaken);
            attr.SetBase(AttrId.ActionSpeed,initial.ActionSpeed);
            _names[HitboxComp.Pack(id)] = initial.Id;
            _sceneActors.Add(initial.Id, id);
            _sceneIds.Add(id,initial.Id);
            _world.PublishSpawn(id, blueprint);
            return id;
        }

        public SkillCastResult Cast(string key, float x, float z)
        {
            EnsureAlive();
            if (!SkillSceneValidation.Finite(x) || !SkillSceneValidation.Finite(z))
                throw new ContentValidationException("$.aim", "Aim must be finite.");
            var skill = _content.Skills.All.FirstOrDefault(s => s.Key == key)
                ?? throw new ContentValidationException("$.skillId", "Unknown skill.");
            if (!_world.TryGetActor(_caster, out var caster)) return new SkillCastResult(false, "Caster is unavailable.");
            var origin = caster.GetComp<TransformComp>().Position;
            var point = new SimVec3(x, 0, z);
            float heading = LocomotionComp.YawFromStick(new SimVec3(point.X - origin.X, 0, point.Z - origin.Z));
            if(caster.TryGetComp<PlayerAimComp>(out var aiming)){aiming.Valid=true;aiming.Aim=new SkillAim(EntityId.Invalid,point,heading);}
            var cast = caster.GetComp<SkillCastComp>();
            bool accepted = cast.TryCast(skill.Id, new SkillAim(EntityId.Invalid, point, heading));
            if(!accepted)Record("rejected",skill.Name,cast.LastRejection,node:_content.Sources.Definition("skill",skill.Id.Value),sourceId:caster.Id);
            return new SkillCastResult(accepted, cast.LastRejection);
        }

        public void Release()
        {
            EnsureAlive();
            if (_world.TryGetActor(_caster, out var caster)) caster.GetComp<SkillCastComp>().Release();
        }
        public void Input(string action,float x,float z)
        {
            EnsureAlive();if(_content.Runtime==null)throw new ContentValidationException("$.runtime.combos","Combo input requires runtime content.");
            if(string.IsNullOrWhiteSpace(action)||action.Length>128)throw new ContentValidationException("$.input","Expected an input identity up to128 characters.");
            Aim(x,z);
            if(!_world.TryGetActor(_caster,out var caster)||caster.GetComp<TagComp>().Has(CommonTags.Dead))return;
            caster.GetComp<InputBufferComp>().Push(new InputToken(action));Record("input",Name(_caster),action);
        }

        public void CancelInput()
        {
            EnsureAlive(); _movement = SimVec3.Zero;
            if (_world.TryGetActor(_caster, out var caster)) caster.GetComp<SkillCastComp>().CancelHeld();
            if(caster!=null&&caster.TryGetComp<InputBufferComp>(out var input))input.Clear();
            if(caster!=null&&caster.TryGetComp<PlayerAimComp>(out var state)){state.Valid=false;state.PrimaryHeld=false;}
        }

        public void RequestHitstop(int frames)
        {
            EnsureAlive();
            if (frames < 0 || frames > 1000) throw new ContentValidationException("$.frames", "Expected 0..1000 hitstop frames.");
            _world.RequestHitstop(frames);
        }
        public void Remove(string id)
        {
            EnsureAlive();
            if(!long.TryParse(id,out long packed))throw new ContentValidationException("$.entityId","Expected a runtime entity identity.");
            var entity=HitboxComp.Unpack(packed);
            if(!_world.TryGetActor(entity,out _))throw new ContentValidationException("$.entityId","Entity is no longer active.");
            _world.RequestDespawn(entity);
        }
        public bool RemoveBuff(string actorId,int instanceId)
        {
            EnsureAlive();
            if(!long.TryParse(actorId,out var packed)||!_world.TryGetActor(HitboxComp.Unpack(packed),out var actor)||!actor.TryGetComp<BuffComp>(out var buffs))
                throw new ContentValidationException("$.actorId","Unknown buff owner identity.");
            return buffs.RemoveInstance(instanceId);
        }

        public void Aim(float x, float z)
        {
            EnsureAlive();
            if (!SkillSceneValidation.Finite(x) || !SkillSceneValidation.Finite(z)) throw new ContentValidationException("$.aim", "Aim must be finite.");
            if (!_world.TryGetActor(_caster, out var caster)) return;
            var origin = caster.GetComp<TransformComp>().Position;
            var aim=new SkillAim(EntityId.Invalid,new SimVec3(x,0,z),LocomotionComp.YawFromStick(new SimVec3(x-origin.X,0,z-origin.Z)));
            caster.GetComp<SkillCastComp>().UpdateAim(aim);
            if(caster.TryGetComp<PlayerAimComp>(out var state)){state.Valid=true;state.Aim=aim;}
        }

        public void Move(float x, float z)
        {
            EnsureAlive();
            if (!SkillSceneValidation.Finite(x) || !SkillSceneValidation.Finite(z) || Math.Abs(x) > 1 || Math.Abs(z) > 1)
                throw new ContentValidationException("$.move", "Expected finite stick values in [-1, 1].");
            _movement = new SimVec3(x, 0, z);
        }

        public void Place(string id, float x, float z,string expectedEntityId=null)
        {
            EnsureAlive();
            if (!SkillSceneValidation.Finite(x) || !SkillSceneValidation.Finite(z)) throw new ContentValidationException("$.place", "Position must be finite.");
            if (id == null || !_sceneActors.TryGetValue(id, out var entity) || !_world.TryGetActor(entity, out var actor))
                throw new ContentValidationException("$.actorId", "Unknown scene actor.");
            if(expectedEntityId!=null&&expectedEntityId!=HitboxComp.Pack(entity).ToString())throw new ContentValidationException("$.entityId","Scene actor identity no longer matches this entity generation.");
            if(actor.GetComp<TagComp>().Has(CommonTags.Dead))throw new ContentValidationException("$.actorId","Cannot reposition a dead scene actor.");
            float y=actor.GetComp<TransformComp>().Position.Y;
            if(!actor.GetComp<LocomotionComp>().TryReposition(new SimVec3(x,y,z)))throw new ContentValidationException("$.place","Actor destination overlaps an obstacle.");
            Record("placement",Name(entity),"Scene position updated",sourceId:entity);
        }

        public void Interrupt(bool death = false)
        {
            EnsureAlive(); _movement = SimVec3.Zero;
            if (_world.TryGetActor(_caster, out var caster))
                caster.GetComp<StateMachineComp>().TryEnter(death ? ActivityId.Dead : ActivityId.Hit,
                    new ActivityEnterArgs { Reason = "SkillLabInput", HitDuration = .35f });
        }

        public void Advance(int steps)
        {
            EnsureAlive();
            if (steps < 1 || steps > 500) throw new ContentValidationException("$.steps", "Expected 1..500.");
            for (int i = 0; i < steps; i++)
            {
                if (_world.TryGetActor(_caster, out var caster)) caster.GetComp<LocomotionComp>().RequestMoveIntent(_movement.X, _movement.Z);
                var previous=caster?.GetComp<TransformComp>().Position;
                _steps++; _world.Tick(Step);
                foreach (var actor in _world.RegistryActive())
                    if (actor.TryGetComp<ProjectileComp>(out _))
                    {
                        var p = actor.GetComp<TransformComp>().Position;
                        _trails.Add(new SkillTrailSnapshot { Entity = HitboxComp.Pack(actor.Id).ToString(), X = p.X, Z = p.Z, Step = _steps });
                    }
                if (_trails.Count > 4000) _trails.RemoveRange(0, _trails.Count - 4000);
                if(previous.HasValue&&_world.TryGetActor(_caster,out caster))
                {
                    var position=caster.GetComp<TransformComp>().Position;
                    if(SpatialGeometry.DistanceSquared(previous.Value,position)>.000001f)
                        _trails.Add(new SkillTrailSnapshot {Entity=HitboxComp.Pack(caster.Id).ToString(),X=position.X,Z=position.Z,Step=_steps});
                }
                _areas.RemoveAll(area => _steps - area.Step > 25);
                _rays.RemoveAll(ray=>_steps-ray.Step>25);
            }
        }

        public SkillSimulationSnapshot Snapshot()
        {
            EnsureAlive();
            var actors = _world.RegistryActive().Select(a =>
            {
                var p = a.GetComp<TransformComp>().Position;
                a.TryGetComp<AttributeSet>(out var attr); a.TryGetComp<ProjectileComp>(out var projectile); a.TryGetComp<AoeComp>(out var area);
                a.TryGetComp<SummonComp>(out var summon);a.TryGetComp<ConstructComp>(out var construct);string blueprint=summon?.Def?.BlueprintId??construct?.Def?.View;
                _content.Blueprints.TryGet(blueprint,out var actorBlueprint);
                var owner=projectile?.OwnerId??area?.OwnerId??summon?.OwnerId??construct?.OwnerId??EntityId.Invalid;
                bool hurt=_hurts.TryGetValue(HitboxComp.Pack(a.Id),out var reaction);
                a.TryGetComp<SkillDirectorComp>(out var director);
                return new SkillActorSnapshot
                {
                    Id = HitboxComp.Pack(a.Id).ToString(),SceneId=_sceneIds.TryGetValue(a.Id,out var sceneId)?sceneId:null,Team=a.TryGetComp<TeamComp>(out var team)?team.TeamId:0, Name = Name(a.Id), Kind = _kinds[HitboxComp.Pack(a.Id)], X = p.X,Y=p.Y, Z = p.Z,Author=ActorSource(a),
                    View=projectile?.Def?.View??area?.Def?.View??construct?.Def?.View,Yaw=a.GetComp<TransformComp>().YawDegrees,
                    ViewScale=area?.Def!=null&&area.Def.Radius>0?area.Radius/area.Def.Radius:1,
                    Hp = attr?.GetBase(AttrId.Hp), MaxHp = attr?.GetFinal(AttrId.MaxHp), Radius = projectile?.Def?.HitRadius ?? area?.Radius ?? (a.TryGetComp<LocomotionComp>(out var locomotion)?locomotion.CollisionRadius:.4f),
                    Remaining = projectile!=null?(float?)Math.Max(0,projectile.Def.Lifetime-projectile.Age):area?.Def != null ? area.Def.Duration>0?(float?)Math.Max(0, area.Def.Duration - area.Age):null : summon!=null&&summon.Lifetime>0?(float?)Math.Max(0,summon.Lifetime-summon.Age):null,
                    Dead = a.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead),
                    Owner=owner.IsValid?Name(owner):null,Blueprint=blueprint,Skill=projectile!=null?SkillName(projectile.Family.Origin):area!=null?SkillName(area.Origin):construct!=null?SkillName(construct.Origin):actorBlueprint?.SkillKey,Energy=a.TryGetComp<EnergyComp>(out var energy)?(float?)energy.Current:null,
                    Group=construct?.Def?.Group,DamageCap=construct?.Def?.DamageCap,NextDecay=construct?.NextDecay,
                    Stopped=_world.IsActorStopped(a),HitstopFrames=a.Time.HitstopLeft,LogicalTime=a.Time.Time,HurtLevel=hurt?reaction.Level.ToString().ToLowerInvariant():null,HurtAge=hurt?(float?)(_steps-reaction.Step)*Step:null,
                    ComboConfirmed=director?.HasComboConfirmation??false,CurrentSkill=director!=null&&director.IsPlaying?SkillName(director.CurrentOrigin):null,
                    ActionTime=director!=null&&director.IsPlaying?(float?)director.TimelineTime:null,ActionDuration=director!=null&&director.IsPlaying?(float?)director.TimelineDuration:null,ActionRate=director?.ActionRate??1,
                    ControlTags=director?.ActiveControls.Select(tag=>tag.Value).ToArray()??Array.Empty<int>(),
                    BufferedInput=a.TryGetComp<InputBufferComp>(out var buffer)&&buffer.TryPeek(out var token)?token.Action:null,
                    Ammo=a.TryGetComp<AmmoComp>(out var ammo)?(int?)ammo.Current:null,TrackingKey=projectile?.Def?.TrackingKey,
                    CastId=projectile?.Family.Origin.CastId??area?.Origin.CastId??construct?.Origin.CastId??(director!=null&&director.IsPlaying?director.CurrentOrigin.CastId:0),Generation=projectile?.Generation??0,ChainsLeft=projectile?.ChainsLeft??0,
                    Distance=projectile!=null?(float?)projectile.Distance:null,Range=projectile!=null&&!double.IsInfinity(projectile.RangeLimit)?(float?)projectile.RangeLimit:null,
                    Speed=projectile!=null?(float?)ProjectileKinematics.Speed(projectile):null,VerticalSpeed=projectile?.Def.Motion?.Bounce!=null||projectile?.Def.Motion?.ScheduledBounce!=null?(float?)projectile.VerticalSpeed:null,
                    Returning=projectile?.Returning??false,GroundBouncesLeft=projectile?.GroundBouncesLeft??0,WallBouncesLeft=projectile?.WallBouncesLeft??0,
                    AbsorbedProjectiles=area?.AbsorbedProjectiles??0,AreaVelocityX=area?.Velocity.X,AreaVelocityZ=area?.Velocity.Z,
                    Buffs=a.TryGetComp<BuffComp>(out var buffs)?buffs.Snapshot().Select(b=>new SkillBuffSnapshot{InstanceId=b.InstanceId,Id=b.Key??b.BuffId.ToString(),Name=b.Name??b.Key??b.BuffId.ToString(),
                        Source=Name(b.Source),Stacks=b.Stacks,Remaining=b.Remaining}).ToArray():Array.Empty<SkillBuffSnapshot>(),
                    Tags=a.TryGetComp<TagComp>(out var tagValues)?tagValues.Snapshot():new Dictionary<int,int>(),Attributes=Attributes(attr)
                };
            }).ToArray();
            _world.TryGetActor(_caster, out var caster);
            var cast = caster?.GetComp<SkillCastComp>();
            return new SkillSimulationSnapshot
            {
                Step = _steps, Time = _steps * Step, Actors = actors, Events = _events.ToArray(), Trails = _trails.ToArray(), Areas = _areas.ToArray(), Volumes=_volumes.Values.ToArray(),
                Obstacles=_world.Collision.Obstacles.Select(o=>new SkillObstacleSnapshot{Id=o.Id,X=o.X,Z=o.Z,Width=o.Width,Depth=o.Depth}).ToArray(),Rays=_rays.ToArray(),
                Energy = caster?.GetComp<EnergyComp>().Current ?? 0,
                Ammo=caster?.GetComp<AmmoComp>().Current??0,AmmoCapacity=caster?.GetComp<AmmoComp>().Capacity??0,
                Channeling = cast?.IsChanneling ?? false,
                Charging = cast?.IsCharging ?? false, ChargeProgress = cast?.ChargeProgress ?? 0, CastPower = cast?.CastPower ?? 1,
                Cooldowns = _content.Skills.All.ToDictionary(s => s.Key, s => cast?.CooldownLeft(s.Id) ?? 0),
                Statistics = new SkillStatistics { Damage = _damage, Hits = _hits, Casts = _casts, Projectiles = _spawned, Areas = _spawnedAreas,Summons=_spawnedSummons,Constructs=_spawnedConstructs,Chains=_chains,Splits=_splits }
            };
        }

        void OnSpawn(EvEntitySpawn e)
        {
            _kinds[HitboxComp.Pack(e.Id)]=e.BlueprintId;
            if(e.BlueprintId=="projectile")_spawned++;if(e.BlueprintId=="aoe")_spawnedAreas++;
            if(e.BlueprintId=="construct")
            { _spawnedConstructs++;if(_world.TryGetActor(e.Id,out var actor))_names[HitboxComp.Pack(e.Id)]=(actor.GetComp<ConstructComp>().Def.Group)+"#"+e.Id.Index; }
            if(e.BlueprintId=="summon")
            {
                _spawnedSummons++;
                if(_world.TryGetActor(e.Id,out var actor)&&actor.TryGetComp<SummonComp>(out var body)&&_content.Blueprints.TryGet(body.Def?.BlueprintId,out var blueprint))
                    _names[HitboxComp.Pack(e.Id)]=(blueprint.Name??blueprint.Id)+"#"+e.Id.Index;
            }
            _world.TryGetActor(e.Id,out var spawned);
            var location=ActorSource(spawned);if(location!=null)_bodySources[HitboxComp.Pack(e.Id)]=location;
            var spawnedOrigin=OriginOf(e.Id);if(spawnedOrigin.CastId>0)_bodyOrigins[HitboxComp.Pack(e.Id)]=spawnedOrigin;
            Record("spawn",Name(e.Id),e.BlueprintId,OriginOf(e.Id),node:ActorSource(spawned),sourceId:e.Owner,bodyId:e.Id);
        }
        void OnDamage(EvDamage e)
        { if(e.Amount>0)_hits++; _damage += e.Amount; Record(e.IsCrit ? "critical" : "damage", Name(e.Source), Name(e.Target),e.Origin,e.Result); }
        static Dictionary<string,float> Attributes(AttributeSet attr)
        {
            var result=new Dictionary<string,float>();if(attr==null)return result;
            foreach(AttrId id in Enum.GetValues(typeof(AttrId)))result[id.ToString()]=attr.GetFinal(id);return result;
        }
        void OnBuff(EvBuffLifecycle e)
        {
            _content.Buffs.TryGet(e.BuffId,out var spec);
            string text=(spec?.Name??e.BuffId.ToString())+" ×"+e.Stacks+" → "+Name(e.Owner);
            if(e.Kind==BuffEventKind.Removed)text+=" / "+e.Reason;
            Record("buff-"+e.Kind.ToString().ToLowerInvariant(),Name(e.Source),text,node:_content.Sources.Definition("buff",e.BuffId),sourceId:e.Source,targetId:e.Owner);
        }
        void OnArea(EvAreaResolved e)
        {
            Record("area", Name(e.Source), e.TargetCount + " targets");
            _areas.Add(new SkillAreaSnapshot { Sequence = _eventSequence, Step = _steps, X = e.Point.X, Z = e.Point.Z, Radius = e.Radius, Targets = e.TargetCount });
            if (_areas.Count > 100) _areas.RemoveAt(0);
        }
        void OnVolume(EvSpatialVolume e)
        {
            if(e.Closed){_volumes.Remove(e.Sequence);Record("action-close",Name(e.Source),e.Shape.ToString(),e.SkillOrigin,node:_content.Sources.Site(e.DiagnosticSite),sourceId:e.Source);return;}
            if(!_volumes.ContainsKey(e.Sequence))Record("action-open",Name(e.Source),e.Shape.ToString(),e.SkillOrigin,node:_content.Sources.Site(e.DiagnosticSite),sourceId:e.Source);
            _volumes[e.Sequence]=new SkillSpatialSnapshot {Sequence=e.Sequence,Source=Name(e.Source),Shape=e.Shape.ToString().ToLowerInvariant(),
                X=e.Origin.X,Z=e.Origin.Z,Yaw=e.Yaw,Length=e.Length,Width=e.Width,Angle=e.Angle};
        }
        void OnRay(EvRayResolved e)
        {
            Record("ray",Name(e.Source),e.Hits+" hits");
            _rays.Add(new SkillRaySnapshot {Sequence=_eventSequence,Step=_steps,FromX=e.From.X,FromZ=e.From.Z,ToX=e.To.X,ToZ=e.To.Z,Width=e.Width,Hits=e.Hits});
            if(_rays.Count>100)_rays.RemoveAt(0);
        }
        void OnSkillEnded(EvSkillEnded e)
        {
            Record(e.Reason == DirectorStopReason.Finished ? "completed" : "cancelled", Name(e.Source), e.Reason.ToString(),e.Origin,node:_content.Sources.Definition("skill",e.Skill.Value),sourceId:e.Source);
            if (e.Source==_caster&&(e.Reason == DirectorStopReason.Hit || e.Reason == DirectorStopReason.Dead || e.Reason == DirectorStopReason.Knockdown))
                _movement = SimVec3.Zero;
        }
        void OnCleanup(EvEntityCleanup e)
        {
            Record("cleanup", Name(e.Id), e.Reason,OriginOf(e.Id),node:BodySource(e.Id),bodyId:e.Id);_bodySources.Remove(HitboxComp.Pack(e.Id));_bodyOrigins.Remove(HitboxComp.Pack(e.Id));
            _names.Remove(HitboxComp.Pack(e.Id)); _kinds.Remove(HitboxComp.Pack(e.Id));
            _hurts.Remove(HitboxComp.Pack(e.Id));
        }
        string Name(EntityId id) => _names.TryGetValue(HitboxComp.Pack(id), out var name) ? name : id.ToString();
        string SkillName(SkillOrigin origin)=>_content.Skills.TryGet(origin.Skill,out var skill)?skill.Key:null;
        void Record(string kind, string source, string result,SkillOrigin origin=default,DamageResult damage=null,AuthorNodeSource node=null,EntityId sourceId=default,EntityId targetId=default,EntityId bodyId=default)
        {
            var execution=_trace.Current;
            if(origin.CastId==0&&execution.Node!=null)origin=execution.Origin;
            _events.Add(new SkillEventSnapshot { Sequence = ++_eventSequence,Step=_steps, Time = _steps * Step, Kind = kind, Source = source, Result = result,CastId=origin.CastId,Skill=SkillName(origin),
                Node=node??execution.Node,ExecutionId=execution.Id,ParentExecutionId=execution.Parent,SourceId=Packed(damage?.Source??(sourceId.IsValid?sourceId:execution.Source)),
                TargetId=Packed(damage?.Target??(targetId.IsValid?targetId:execution.Target)),BodyId=Packed(bodyId),
                HitIndex=damage?.HitIndex??0,Damage=damage==null?null:new SkillDamageSnapshot {Kind=damage.Kind.ToString().ToLowerInvariant(),Target=Name(damage.Target),Base=damage.Base,Scaled=damage.Scaled,
                    AfterDefense=damage.AfterDefense,AfterCritical=damage.AfterCritical,Final=damage.Final,Absorbed=damage.Absorbed,HpLost=damage.HpLost,Overkill=damage.Overkill,Killed=damage.Killed,Feedback=damage.Feedback.ToString().ToLowerInvariant()} });
            if (_events.Count > 500) _events.RemoveAt(0);
        }
        void EnsureAlive() { if (_disposed) throw new ObjectDisposedException(nameof(SkillSimulation)); }
        public void Dispose()
        {
            if (_disposed) return;
            foreach (var actor in _world.RegistryActive())
            { if (actor.TryGetComp<SkillDirectorComp>(out var director)) director.Stop(DirectorStopReason.Detach); _world.RequestDespawn(actor.Id); }
            _world.Tick(0);
            _events.Clear(); _trails.Clear(); _areas.Clear(); _rays.Clear(); _volumes.Clear(); _names.Clear(); _kinds.Clear(); _hurts.Clear(); _sceneActors.Clear();_sceneIds.Clear();
            _bodySources.Clear();_bodyOrigins.Clear();
            _disposed = true;
        }
    }
}
