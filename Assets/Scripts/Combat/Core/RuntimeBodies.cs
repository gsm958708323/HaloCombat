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
        public int MaxHits = 1;
        public bool SnapshotAtk = true;
        public int HostileMask;
        public int CueId;
        public float SpawnForward = 0.4f;
        public float HomingRate;
        public float HomingMaxTurn;
        public bool HomingRetarget;
        public float HomingAcquireRadius = 12f;
        public ProjectileChainDefinition Chain;
        public ProjectileMotionDefinition Motion;
        public string TrackingKey;
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
        public int GroundBouncesLeft { get; set; }
        public int WallBouncesLeft { get; set; }
        public int HitCount { get; set; }
        public bool Exhausted { get; set; }
        public EntityId HomingTarget { get; set; }

        public void Setup(ProjectileDefinition def, EntityId owner, float snapshotAtk, EntityId homingTarget = default, float power = 1,ProjectileSeed seed=null)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            Power = power;
            Family=seed?.Family??new ProjectileFamily(default);
            Generation=seed?.Generation??0;
            RangeLimit=seed!=null&&seed.Range>0?seed.Range:def.Range>0?def.Range:double.PositiveInfinity;
            ChainsLeft=def.Chain?.ExtraChains??0;LegHits=0;
            Returning=false;VerticalSpeed=seed?.VerticalSpeed??def.Motion?.Bounce?.VerticalSpeed??0;
            SampledSpeed=0;
            GroundBouncesLeft=def.Motion?.Bounce?.Count??0;WallBouncesLeft=def.Motion?.WallBounces??0;
            Age = 0f;
            Distance = 0;
            HitCount = 0;
            Exhausted = false;
            HomingTarget = homingTarget;
            _hits.Clear();
            if(seed!=null)foreach(var id in seed.Ancestors)_hits.Add(id);
        }

        public bool TryRecord(EntityId id)
        {
            if (!id.IsValid || Exhausted) return false;
            return _hits.Add(id);
        }
        public bool HasHit(EntityId id)=>_hits.Contains(id);
        internal EntityId[] Visited()
        {
            var result=new EntityId[_hits.Count];_hits.CopyTo(result);return result;
        }

        protected override void OnDetach()
        {
            if(Self.World!=null&&Self.World.TryGetActor(OwnerId,out var owner)&&owner.TryGetComp<TrackedProjectilesComp>(out var tracker))tracker.ClearIf(Def?.TrackingKey,Self.Id);
            _hits.Clear();
            Def = null;
            Exhausted = true;
            Power = 1;
            Family=null;
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
            _inside = (def != null && def.TrackOccupancy) ? new HashSet<long>() : null;
        }

        protected override void OnDetach()
        {
            _inside?.Clear();
            Def = null;
            Power = 1; Ending = true;
        }
    }

}
