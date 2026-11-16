using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class ProjectileDefinition
    {
        public int SpecId;
        public float Speed = 14f;
        public float Lifetime = 2f;
        public float Range;
        public float HitRadius = 0.3f;
        public bool SnapshotAtk = true;
        public int HostileMask;
        public int CueId;
        public string View;
        public float SpawnForward = 0.4f;
        public float HomingRate;
        public float HomingMaxTurn;
        public bool HomingRetarget;
        public float HomingAcquireRadius = 12f;
        public ProjectileMotionDefinition Motion;
        public string TrackingKey;
        public float SameTargetDelay;
        public bool PassInvulnerable;
        public bool RemoveOnObstacle=true;
        public ProjectileContactMode ContactMode;
        public bool HitOncePerLeg;
        public ProjectileFlightDefinition Flight;
        public IEffect[] OnFlight=Array.Empty<IEffect>();
        public IEffect[] OnHit = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
        public IEffect[] OnWall = Array.Empty<IEffect>();
        public IEffect[] OnRange = Array.Empty<IEffect>();
        public IEffect[] OnEnd = Array.Empty<IEffect>();
        public IEffect[] OnBounce = Array.Empty<IEffect>();
        public IEffect[] OnReturned = Array.Empty<IEffect>();
        public IEffect[] OnGround = Array.Empty<IEffect>();
    }

    public sealed class AoeDefinition
    {
        public int SpecId;
        public float Radius = 1.3f;
        public float Duration = 2f;
        public float PulseInterval = 0.45f;
        public bool PulseOnSpawn = true;
        public bool TrackOccupancy;
        public int MaxTargets;
        public int HostileMask;
        public int CueId;
        public bool MoveForward;
        public string View;
        public float MoveSpeed;
        public bool RemoveOnObstacle=true;
        public bool AbsorbProjectiles;
        public float ProjectileAbsorbForce;
        public float ProjectileRadiusScale;
        public IEffect[] OnAbsorb=Array.Empty<IEffect>();
        public IEffect[] OnPulse = Array.Empty<IEffect>();
        public IEffect[] OnEnter = Array.Empty<IEffect>();
        public IEffect[] OnExit = Array.Empty<IEffect>();
        public IEffect[] OnStay = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
    }

    public sealed class ProjectileCatalog
    {
        readonly Dictionary<int, ProjectileDefinition> _map = new Dictionary<int, ProjectileDefinition>(8);
        public void Register(ProjectileDefinition def)
        {
            if (def == null || def.SpecId == 0) throw new ArgumentException("ProjectileDefinition");
            _map[def.SpecId] = def;
        }

        public bool TryGet(int specId, out ProjectileDefinition def) => _map.TryGetValue(specId, out def);
    }

    public sealed class AoeCatalog
    {
        readonly Dictionary<int, AoeDefinition> _map = new Dictionary<int, AoeDefinition>(8);
        public void Register(AoeDefinition def)
        {
            if (def == null || def.SpecId == 0) throw new ArgumentException("AoeDefinition");
            _map[def.SpecId] = def;
        }

        public bool TryGet(int specId, out AoeDefinition def) => _map.TryGetValue(specId, out def);
    }

    public sealed class ProjectileComp : Comp
    {
        readonly HashSet<EntityId> _hits = new HashSet<EntityId>();
        readonly Dictionary<EntityId,double> _nextHit=new Dictionary<EntityId,double>();
        readonly HashSet<EntityId> _returnHits=new HashSet<EntityId>(),_ancestors=new HashSet<EntityId>();
        readonly Dictionary<ChainProjectileEffect,int> _chainUses=new Dictionary<ChainProjectileEffect,int>();
        internal long NextFlightIndex {get;set;}
        internal int FlightFrame {get;set;}=int.MinValue;
        internal int FlightDelivered {get;set;}
        internal bool FlightLimited {get;set;}
        public EntityId OwnerId { get; private set; }
        public float SnapshotAtk { get; private set; }
        public float Power { get; private set; } = 1;
        public ProjectileDefinition Def { get; private set; }
        public double Age { get; set; }
        public double Distance { get; set; }
        public double RangeLimit { get; private set; }
        public ProjectileFamily Family { get; private set; }
        public int Generation { get; private set; }
        public int ChainsLeft { get; set; }
        public int LegHits { get; set; }
        public bool Returning { get; set; }
        public double VerticalSpeed { get; set; }
        public double SampledSpeed {get;internal set;}
        public float GroundY {get;private set;}
        public SimVec3 Velocity {get;internal set;}
        public int GroundBouncesLeft { get; set; }
        public int WallBouncesLeft { get; set; }
        public int HitCount { get; set; }
        public bool Exhausted { get; set; }
        public EntityId HomingTarget { get; set; }

        public void Setup(ProjectileDefinition def, EntityId owner, float snapshotAtk, EntityId homingTarget = default, float power = 1,ProjectileSeed seed=null,float groundY=0)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            Power = power;
            Family=seed?.Family??new ProjectileFamily(default);
            Generation=seed?.Generation??0;
            RangeLimit=seed!=null&&seed.Range>0?seed.Range:def.Range>0?def.Range:double.PositiveInfinity;
            _chainUses.Clear();ChainsLeft=0;LegHits=0;
            foreach(var effect in def.OnHit)if(effect is ChainProjectileEffect chain)ChainsLeft=Math.Max(ChainsLeft,chain.Count);
            Returning=false;VerticalSpeed=seed?.VerticalSpeed??def.Motion?.Bounce?.VerticalSpeed??0;
            SampledSpeed=0;
            GroundBouncesLeft=def.Motion?.ScheduledBounce?.Phases.Length??def.Motion?.Bounce?.Count??0;WallBouncesLeft=def.Motion?.WallBounces??0;GroundY=groundY;Velocity=SimVec3.Zero;
            Age = 0f;
            Distance = 0;
            HitCount = 0;
            Exhausted = false;
            HomingTarget = homingTarget;
            _hits.Clear();
            _nextHit.Clear();
            _returnHits.Clear();_ancestors.Clear();NextFlightIndex=1;FlightFrame=int.MinValue;FlightDelivered=0;FlightLimited=false;
            if(seed!=null)foreach(var id in seed.Ancestors){_hits.Add(id);_ancestors.Add(id);}
        }

        public bool TryRecord(EntityId id)
        {
            if(!CanHit(id,Age))return false;
            _hits.Add(id);if(Returning)_returnHits.Add(id);float delay=Def?.SameTargetDelay??0;if(delay>0)_nextHit[id]=Age+delay;return true;
        }
        public bool CanHit(EntityId id,double atAge)=>id.IsValid&&!Exhausted&&!_ancestors.Contains(id)&&
            (Def?.HitOncePerLeg==true&&Returning?!_returnHits.Contains(id):
                !_hits.Contains(id)||(Def?.SameTargetDelay??0)>0&&_nextHit.TryGetValue(id,out double next)&&atAge+.0000001>=next);
        public bool HasHit(EntityId id)=>_hits.Contains(id);
        internal bool CanChain(ChainProjectileEffect effect)=>!_chainUses.TryGetValue(effect,out int count)||count<effect.Count;
        internal void ConsumeChain(ChainProjectileEffect effect)
        {_chainUses.TryGetValue(effect,out int count);_chainUses[effect]=count+1;ChainsLeft=Math.Max(0,effect.Count-count-1);}
        internal EntityId[] Visited()
        {
            var result=new EntityId[_hits.Count];_hits.CopyTo(result);return result;
        }

        protected override void OnDetach()
        {
            if(Self.World!=null&&Self.World.TryGetActor(OwnerId,out var owner)&&owner.TryGetComp<TrackedProjectilesComp>(out var tracker))tracker.ClearIf(Def?.TrackingKey,Self.Id);
            _hits.Clear();
            _nextHit.Clear();
            _chainUses.Clear();
            _returnHits.Clear();_ancestors.Clear();NextFlightIndex=1;FlightFrame=int.MinValue;FlightDelivered=0;FlightLimited=false;
            Def = null;
            Exhausted = true;
            Power = 1;
            Family=null;
            GroundY=0;SampledSpeed=0;Velocity=SimVec3.Zero;
        }
    }

    public sealed class AoeComp : Comp
    {
        HashSet<long> _inside;
        public EntityId OwnerId { get; private set; }
        public float SnapshotAtk { get; private set; }
        public float Power { get; private set; } = 1;
        public AoeDefinition Def { get; private set; }
        public double Age { get; set; }
        public long NextPulse { get; set; }
        public bool Ending { get; set; }
        public int BornFrame { get; private set; }
        public SkillOrigin Origin {get;private set;}
        public HashSet<long> Inside => _inside;
        public float BaseRadius {get;private set;}
        public float Radius=>BaseRadius*(1+AbsorbedProjectiles*(Def?.ProjectileRadiusScale??0));
        public int AbsorbedProjectiles {get;private set;}
        public SimVec3 Velocity {get;private set;}
        internal void Absorb(SimVec3 velocity)
        {AbsorbedProjectiles++;Velocity=new SimVec3(Velocity.X+velocity.X*Def.ProjectileAbsorbForce,0,Velocity.Z+velocity.Z*Def.ProjectileAbsorbForce);}

        public void Setup(AoeDefinition def, EntityId owner, float snapshotAtk, int bornFrame, float power = 1,SkillOrigin origin=default)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            Power = power; Ending = false;
            Origin=origin;
            Age = 0f;
            NextPulse = 1;
            BornFrame = bornFrame;
            BaseRadius=def?.Radius??0;AbsorbedProjectiles=0;Velocity=SimVec3.Zero;
            _inside = (def != null && def.TrackOccupancy) ? new HashSet<long>() : null;
        }

        protected override void OnDetach()
        {
            _inside?.Clear();
            Def = null;
            Power = 1; Ending = true;
            BaseRadius=0;AbsorbedProjectiles=0;Velocity=SimVec3.Zero;OwnerId=EntityId.Invalid;Origin=default;
        }
    }

}
