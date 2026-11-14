using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public readonly struct SkillAim
    {
        public readonly EntityId Target;
        public readonly SimVec3 Point;
        public readonly float Yaw;
        public readonly bool HasPoint;
        public SkillAim(EntityId target, SimVec3 point, float yaw, bool hasPoint = true)
        { Target = target; Point = point; Yaw = yaw; HasPoint = hasPoint; }
    }

    public sealed class SkillDefinition
    {
        public SkillNodeId Id;
        public TimelineId Timeline;
        public string Key;
        public string Name;
        public float Cooldown;
        public float EnergyCost;
        public SkillTargetMode TargetMode;
        public float TargetRange;
        public SkillCastMode Mode;
        public ChargeDefinition Charge;
        public float EnergyPerSecond;
        public bool CostPerPulse;
        public float AimRange;
        public int AmmoCost;
        public SkillNodeId AmmoFallback,RecastSkill;
        public string RecastProjectile,RequiredProjectile;
    }

    public sealed class SkillCatalog
    {
        readonly Dictionary<int, SkillDefinition> _definitions = new Dictionary<int, SkillDefinition>();
        public IEnumerable<SkillDefinition> All => _definitions.Values;
        public void Register(SkillDefinition value)
        {
            if (value == null || !value.Id.IsValid || !value.Timeline.IsValid)
                throw new ArgumentException("Invalid skill definition.");
            if (_definitions.ContainsKey(value.Id.Value)) throw new ArgumentException("Duplicate skill id " + value.Id.Value);
            _definitions.Add(value.Id.Value, value);
        }
        public bool TryGet(SkillNodeId id, out SkillDefinition value) => _definitions.TryGetValue(id.Value, out value);
    }

    public sealed class EnergyComp : Comp
    {
        public float Capacity { get; }
        public float Current { get; private set; }
        public float Recovery { get; }
        public override bool WantsTick => true;
        public EnergyComp(float capacity, float recovery, float? initialEnergy = null)
        { Capacity = Math.Max(0f, capacity); Current = Math.Max(0, Math.Min(Capacity, initialEnergy ?? Capacity)); Recovery = Math.Max(0f, recovery); }
        public bool CanSpend(float amount) => amount >= 0f && Current >= amount;
        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;
            Current -= amount;
            return true;
        }
        public float SpendDuration(float rate, float duration)
        {
            if (rate <= 0f) return duration;
            float allowed = Math.Min(duration, Current / rate);
            Current = Math.Max(0, Current - allowed * rate);
            return allowed;
        }
        public override void Tick(float dt) => Current = Math.Min(Capacity, Current + Recovery * dt);
    }

    // Shared by player, AI, debug slots and the preview. All mutable timers belong to the actor.
    public sealed class SkillCastComp : Comp
    {
        readonly SkillCatalog _catalog;
        readonly Dictionary<int, float> _readyAt = new Dictionary<int, float>();
        SkillDirectorComp _director;
        EnergyComp _energy;
        AmmoComp _ammo;
        TagComp _tags;
        SkillDefinition _active;
        SkillAim _inputAim;
        float _time;
        float _basePower=1;
        public override bool WantsTick => true;
        public string LastRejection { get; private set; }
        bool IsActive => _active != null && _director.IsPlaying && _director.CurrentSkill.Equals(_active.Id);
        public bool IsChanneling => IsActive && _active.Mode == SkillCastMode.Channel;
        public bool IsCharging => IsActive && _director.IsCharging;
        public float ChargeProgress => IsCharging ? _director.ChargeProgress : 0;
        public float CastPower => IsActive ? _director.CastPower : 1;
        public SkillCastComp(SkillCatalog catalog) => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        public void SetBasePower(float power)
        {if(!Finite(power)||power<=0)throw new ArgumentOutOfRangeException(nameof(power));_basePower=power;}
        protected override void OnAttach()
        {
            _director = Self.GetComp<SkillDirectorComp>();
            _tags = Self.GetComp<TagComp>();
            Self.TryGetComp(out _energy);
            Self.TryGetComp(out _ammo);
        }
        public float CooldownLeft(SkillNodeId id)
            => _readyAt.TryGetValue(id.Value, out var until) ? Math.Max(0f, until - _time) : 0f;
        public bool TryCast(SkillNodeId id, in SkillAim aim)
        {
            LastRejection = null;
            if(Self.World.IsActorStopped(Self))return Reject("Actor is in hitstop.");
            if(aim.HasPoint&&(!Finite(aim.Point.X)||!Finite(aim.Point.Y)||!Finite(aim.Point.Z)||!Finite(aim.Yaw)))return Reject("Aim must be finite.");
            if (IsChanneling || IsCharging) return Reject("A held cast is already active.");
            if(!SkillCastRouting.TryResolve(Self,_catalog,id,out var definition,out var routeError))return Reject(routeError);
            id=definition.Id;
            if (CooldownLeft(id) > 0f) return Reject("Cooldown.");
            if (_director.IsPlaying && !_tags.Has(CommonTags.Cancel)) return Reject("Cancel window is closed.");
            if (_tags.Has(CommonTags.Dead) || _tags.Has(CommonTags.Stunned) || _tags.Has(CommonTags.Downed) || _tags.Has(CommonTags.Silence))
                return Reject("Casting is blocked by actor state.");
            if (definition.EnergyCost > 0f && (_energy == null || !_energy.CanSpend(definition.EnergyCost)))
                return Reject("Insufficient energy.");
            if(definition.AmmoCost>0&&(_ammo==null||!_ammo.CanSpend(definition.AmmoCost)))return Reject("Insufficient ammunition.");
            if(!string.IsNullOrEmpty(definition.RequiredProjectile)&&(!Self.TryGetComp<TrackedProjectilesComp>(out var tracked)||!tracked.TryFind(definition.RequiredProjectile,out _)))return Reject("Required projectile is unavailable.");
            if (!SkillTargeting.TryResolve(Self, definition, aim, out var resolved)) return Reject("No valid target.");
            if (definition.AimRange > 0)
            {
                var origin = Self.GetComp<TransformComp>().Position;
                if (!resolved.HasPoint || SpatialGeometry.DistanceSquared(origin, resolved.Point) > definition.AimRange * definition.AimRange + .0001f)
                    return Reject("Destination is outside the displacement range.");
                float radius=Self.TryGetComp<LocomotionComp>(out var loco)?loco.CollisionRadius:0;
                if(!Self.World.Collision.IsFree(resolved.Point,radius))return Reject("Destination overlaps an obstacle.");
            }
            if (!_director.Play(id, definition.Timeline, resolved, AdvanceBudget, PayPulse,
                definition.Mode != SkillCastMode.Instant ? ResolveAim : (Func<SkillAim?>)null, definition.Charge,_basePower))
                return Reject("Activity rejected casting.");
            _active = definition; _inputAim = aim;
            if (definition.EnergyCost > 0f) _energy.TrySpend(definition.EnergyCost);
            if(definition.AmmoCost>0)_ammo.TrySpend(definition.AmmoCost);
            _readyAt[id.Value] = _time + definition.Cooldown;
            Self.World.Events.Publish(new EvSkillStarted(Self.Id,id));
            return true;
        }
        public void UpdateAim(in SkillAim aim) => _inputAim = aim;
        public void Release()
        {
            if (IsChanneling) _director.Stop(DirectorStopReason.Released);
            else if (IsCharging) _director.ReleaseCharge();
        }
        public void CancelHeld() { if (IsChanneling || IsCharging) _director.Stop(DirectorStopReason.InputCancelled); }
        float AdvanceBudget(float duration)
        {
            if (_tags.Has(CommonTags.Dead) || _tags.Has(CommonTags.Stunned) || _tags.Has(CommonTags.Downed) || _tags.Has(CommonTags.Silence)) return 0;
            if ((IsChanneling || IsCharging) && Self.TryGetComp<LocomotionComp>(out var loco)) loco.SetClipSteer(1);
            if (_active == null || _active.EnergyPerSecond <= 0 || (!IsChanneling && !IsCharging)) return duration;
            return _energy == null ? 0 : _energy.SpendDuration(_active.EnergyPerSecond, duration);
        }
        bool PayPulse(int pulse)
        {
            if(pulse==0||_active==null||!_active.CostPerPulse)return true;
            if(_active.EnergyCost>0&&(_energy==null||!_energy.CanSpend(_active.EnergyCost))||_active.AmmoCost>0&&(_ammo==null||!_ammo.CanSpend(_active.AmmoCost)))return false;
            if(_active.EnergyCost>0)_energy.TrySpend(_active.EnergyCost);if(_active.AmmoCost>0)_ammo.TrySpend(_active.AmmoCost);return true;
        }
        SkillAim? ResolveAim() => SkillTargeting.TryResolve(Self, _active, _inputAim, out var aim) ? aim : (SkillAim?)null;
        bool Reject(string reason) { LastRejection = reason; return false; }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public override void Tick(float dt) => _time += dt;
        protected override void OnDetach()
        { _readyAt.Clear(); _time = 0f; _basePower=1; _active = null; _director = null; _energy = null; _ammo=null; _tags = null; }
    }
}
