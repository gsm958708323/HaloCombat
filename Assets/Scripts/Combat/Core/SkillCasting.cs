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
        public readonly bool HasYaw;
        public SkillAim(EntityId target, SimVec3 point, float yaw, bool hasPoint = true)
        { Target = target; Point = point; Yaw = yaw; HasPoint = hasPoint;HasYaw=true; }
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
        public bool AllowMovement;
        public float AimRange;
        public int AmmoCost;
        public int RequiredBuffId, BuffStackCost;
        public float ReturnWindow;
        public SkillNodeId AmmoFallback,RecastSkill;
        public string RecastProjectile,RequiredProjectile;
        public HitFeedbackLevel HitFeedback;
        public IEffect[] OperationEffects;
    }

    public sealed class SkillCatalog
    {
        readonly Dictionary<int, SkillDefinition> _definitions = new Dictionary<int, SkillDefinition>();
        public IEnumerable<SkillDefinition> All => _definitions.Values;
        public void Register(SkillDefinition value)
        {
            if (value == null || !value.Id.IsValid || (value.OperationEffects==null?!value.Timeline.IsValid:value.Timeline.IsValid||value.OperationEffects.Length==0||value.Mode!=SkillCastMode.Instant||value.Charge!=null))
                throw new ArgumentException("Invalid skill definition.");
            if (value.RequiredBuffId < 0 || value.BuffStackCost < 0 || (value.BuffStackCost > 0 && value.RequiredBuffId == 0))
                throw new ArgumentException("Invalid skill buff requirement.");
            if (float.IsNaN(value.ReturnWindow) || float.IsInfinity(value.ReturnWindow) || value.ReturnWindow < 0 ||
                (value.ReturnWindow > 0 && (value.Mode != SkillCastMode.Instant || !(value.Cooldown > 0))))
                throw new ArgumentException("Invalid skill origin return window.");
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
        public bool Unlimited {get;}
        public override bool WantsTick => true;
        public EnergyComp(float capacity, float recovery, float? initialEnergy = null,bool unlimited=false)
        { Capacity = Math.Max(0f, capacity); Current = Math.Max(0, Math.Min(Capacity, initialEnergy ?? Capacity)); Recovery = Math.Max(0f, recovery);Unlimited=unlimited; }
        public bool CanSpend(float amount) => amount >= 0f && (Unlimited||Current >= amount);
        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;
            if(!Unlimited)Current -= amount;
            return true;
        }
        public float SpendDuration(float rate, float duration)
        {
            if (Unlimited||rate <= 0f) return duration;
            float allowed = Math.Min(duration, Current / rate);
            Current = Math.Max(0, Current - allowed * rate);
            return allowed;
        }
        public override void Tick(float dt) => Current = Math.Min(Capacity, Current + Recovery * dt);
    }

    // Shared by player, AI, debug slots and the preview. All mutable timers belong to the actor.
    public sealed class SkillCastComp : Comp
    {
        SkillCatalog _catalog;
        public void ReplaceCatalog(SkillCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _returnAnchors.Clear();
        }
        readonly List<SkillReturnAnchor> _returnAnchors = new List<SkillReturnAnchor>();
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
        public SkillNodeId LastAcceptedSkill {get;private set;}
        bool IsActive => _active != null && _director.IsPlaying && _director.CurrentSkill.Equals(_active.Id);
        public bool IsChanneling => IsActive && _active.Mode == SkillCastMode.Channel;
        public bool IsCharging => IsActive && _director.IsCharging;
        public float ChargeProgress => IsCharging ? _director.ChargeProgress : 0;
        public float CastPower => IsActive ? _director.CastPower : 1;
        public bool AllowsMovement=>IsActive&&_active.AllowMovement;
        public SkillCastComp(SkillCatalog catalog) => _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        public HitFeedbackLevel FeedbackFor(SkillNodeId skill)=>_catalog.TryGet(skill,out var definition)?definition.HitFeedback:HitFeedbackLevel.None;
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
        public bool TryCast(SkillNodeId id, in SkillAim aim, InputToken input = default, bool allowReturn = true, bool cancelWindowVerified = false)
        {
            LastRejection = null;LastAcceptedSkill=SkillNodeId.None;
            if (allowReturn)
            {
                int anchor = FindReturnAnchor(input, id);
                if (anchor >= 0) return TryReturn(anchor);
            }
            if(Self.World.IsActorStopped(Self))return Reject("Actor is in hitstop.");
            if(aim.HasYaw&&!Finite(aim.Yaw)||aim.HasPoint&&(!Finite(aim.Point.X)||!Finite(aim.Point.Y)||!Finite(aim.Point.Z)))return Reject("Aim must be finite.");
            if (IsChanneling || IsCharging) return Reject("A held cast is already active.");
            if(!SkillCastRouting.TryResolve(Self,_catalog,id,out var definition,out var routeError))return Reject(routeError);
            var requested = id;
            id=definition.Id;
            if (CooldownLeft(id) > 0f) return Reject("Cooldown.");
            if (_director.IsPlaying && !cancelWindowVerified && !_tags.Has(CommonTags.Cancel)) return Reject("Cancel window is closed.");
            if (_tags.Has(CommonTags.Dead) || _tags.Has(CommonTags.Stunned) || _tags.Has(CommonTags.Downed) || _tags.Has(CommonTags.Silence)||_tags.Has(CommonTags.BlockSkill))
                return Reject("Casting is blocked by actor state.");
            if (definition.EnergyCost > 0f && (_energy == null || !_energy.CanSpend(definition.EnergyCost)))
                return Reject("Insufficient energy.");
            if(definition.AmmoCost>0&&(_ammo==null||!_ammo.CanSpend(definition.AmmoCost)))return Reject("Insufficient ammunition.");
            if(!string.IsNullOrEmpty(definition.RequiredProjectile)&&(!Self.TryGetComp<TrackedProjectilesComp>(out var tracked)||!tracked.TryFind(definition.RequiredProjectile,out _)))return Reject("Required projectile is unavailable.");
            BuffComp buffs = null;
            if (definition.RequiredBuffId > 0 && (!Self.TryGetComp(out buffs) ||
                buffs.StacksOf(definition.RequiredBuffId) < Math.Max(1, definition.BuffStackCost)))
                return Reject("Insufficient required buff stacks.");
            bool refreshOnTrigger=definition.OperationEffects==null&&(definition.Mode!=SkillCastMode.Instant||
                definition.TargetMode==SkillTargetMode.Random||definition.TargetMode==SkillTargetMode.Direction);
            if (!SkillTargeting.TryResolve(Self, definition, aim, out var resolved,!refreshOnTrigger)) return Reject("No valid target.");
            if (definition.AimRange > 0)
            {
                var origin = Self.GetComp<TransformComp>().Position;
                if (!resolved.HasPoint || SpatialGeometry.DistanceSquared(origin, resolved.Point) > definition.AimRange * definition.AimRange + .0001f)
                    return Reject("Destination is outside the displacement range.");
                float radius=Self.TryGetComp<LocomotionComp>(out var loco)?loco.CollisionRadius:0;
                if(!Self.World.Collision.IsFree(resolved.Point,radius))return Reject("Destination overlaps an obstacle.");
            }
            var returnOrigin = definition.ReturnWindow > 0 ? Self.GetComp<TransformComp>().Position : default;
            if(definition.OperationEffects!=null)
            {
                CommitCast(definition, buffs, requested, input, returnOrigin, new SkillOrigin(id,0,caster:Self.Id));
                Actor target=null;if(resolved.Target.IsValid)Self.World.TryGetActor(resolved.Target,out target);
                float attack=Self.TryGetComp<AttributeSet>(out var attributes)?attributes.GetFinal(AttrId.Atk):0;
                Self.World.Deliver(definition.OperationEffects,Self,target,attack,resolved.HasPoint?(SimVec3?)resolved.Point:null,
                    resolved.HasYaw?(SimVec3?)LocomotionComp.ForwardFromYaw(resolved.Yaw):null,power:_basePower,
                    origin:new SkillOrigin(id,0,caster:Self.Id));
                LastAcceptedSkill=id;Self.World.Events.Publish(new EvSkillOperation(Self.Id,id));return true;
            }
            // Paid buffs must be debited before the remaining buffs observe the cast.
            // Preserve the existing resource/hook order for ordinary casts.
            Action commit = definition.BuffStackCost > 0 || definition.ReturnWindow > 0
                ? () => CommitCast(definition, buffs, requested, input, returnOrigin, _director.CurrentOrigin)
                : (Action)null;
            if (!_director.Play(id, definition.Timeline, resolved, AdvanceBudget, PayPulse,
                refreshOnTrigger ? ResolveAim : (Func<bool,SkillAim?>)null, definition.Charge,_basePower,commit))
                return Reject("Activity rejected casting.");
            _active = definition; _inputAim = aim;
            if (commit == null) CommitCast(definition, buffs, requested, input, returnOrigin, _director.CurrentOrigin);
            LastAcceptedSkill=id;Self.World.Events.Publish(new EvSkillStarted(Self.Id,id));
            return true;
        }
        void CommitCast(SkillDefinition definition, BuffComp buffs, SkillNodeId requested, InputToken input, SimVec3 position, in SkillOrigin origin)
        {
            if (definition.EnergyCost > 0) _energy.TrySpend(definition.EnergyCost);
            if (definition.AmmoCost > 0) _ammo.TrySpend(definition.AmmoCost);
            _readyAt[definition.Id.Value] = _time + definition.Cooldown;
            float window = Math.Min(definition.ReturnWindow, definition.Cooldown);
            if (window > 0)
            {
                int previous = FindReturnAnchor(input, requested);
                if (previous >= 0) _returnAnchors.RemoveAt(previous);
                _returnAnchors.Add(new SkillReturnAnchor(requested, input, origin, position, _time + window));
            }
            if (definition.BuffStackCost > 0) buffs.TryConsumeStacks(definition.RequiredBuffId, definition.BuffStackCost);
        }
        int FindReturnAnchor(InputToken input, SkillNodeId requested)
        {
            for (int i = 0; i < _returnAnchors.Count; i++)
            {
                var anchor = _returnAnchors[i];
                if (anchor.ExpiresAt > _time && (input.IsValid ? anchor.Input == input : !anchor.Input.IsValid && anchor.RequestedSkill.Equals(requested)))
                    return i;
            }
            return -1;
        }
        public SkillReturnAnchor[] ReturnAnchors()
        {
            if (_returnAnchors.Count == 0 || _tags.Has(CommonTags.Dead)) return Array.Empty<SkillReturnAnchor>();
            var result = new SkillReturnAnchor[_returnAnchors.Count];
            for (int i = 0; i < result.Length; i++) result[i] = _returnAnchors[i].WithRemaining(_time);
            return result;
        }
        internal bool HasInputReturnAnchor(InputToken input)
            => input.IsValid && FindReturnAnchor(input, SkillNodeId.None) >= 0;

        // The input driver checks explicit presses before resolving combo edges.
        public bool TryReturnFromInput(InputToken input, out bool accepted)
        {
            accepted = false;
            if (!input.IsValid) return false;
            int index = FindReturnAnchor(input, SkillNodeId.None);
            if (index < 0) return false;
            accepted = TryReturn(index);
            return true;
        }
        bool TryReturn(int index)
        {
            LastRejection = null;
            LastAcceptedSkill = SkillNodeId.None;
            if (!_director.CanStartSkill) return Reject("Returning is blocked by actor state.");
            if (_director.IsPlaying) return Reject("Returning requires an idle skill director.");
            var anchor = _returnAnchors[index];
            if (!Self.TryGetComp<LocomotionComp>(out var motor) || !motor.RequestTeleport(anchor.Position))
                return Reject("Return destination is blocked.");
            _returnAnchors.RemoveAt(index);
            LastAcceptedSkill = anchor.Origin.Skill;
            Self.World.Events.Publish(new EvSkillReturned(Self.Id, anchor.Origin, anchor.Position));
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
        SkillAim? ResolveAim(bool sample) => SkillTargeting.TryResolve(Self, _active, _inputAim, out var aim,sample) ? aim : (SkillAim?)null;
        bool Reject(string reason) { LastRejection = reason; return false; }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        public override void Tick(float dt)
        {
            _time += dt;
            if (_returnAnchors.Count == 0) return;
            if (_tags.Has(CommonTags.Dead)) _returnAnchors.Clear();
            else _returnAnchors.RemoveAll(anchor => anchor.ExpiresAt <= _time);
        }
        protected override void OnDetach()
        { _returnAnchors.Clear(); _readyAt.Clear(); _time = 0f; _basePower=1; _active = null;LastAcceptedSkill=SkillNodeId.None; _director = null; _energy = null; _ammo=null; _tags = null; }
    }

    public readonly struct SkillReturnAnchor
    {
        public readonly SkillNodeId RequestedSkill;
        public readonly InputToken Input;
        public readonly SkillOrigin Origin;
        public readonly SimVec3 Position;
        public readonly float Remaining;
        internal readonly float ExpiresAt;

        internal SkillReturnAnchor(SkillNodeId requestedSkill, InputToken input, SkillOrigin origin, SimVec3 position, float expiresAt, float remaining = 0)
        {
            RequestedSkill = requestedSkill;
            Input = input;
            Origin = origin;
            Position = position;
            ExpiresAt = expiresAt;
            Remaining = remaining;
        }
        internal SkillReturnAnchor WithRemaining(float time)
            => new SkillReturnAnchor(RequestedSkill, Input, Origin, Position, ExpiresAt, ExpiresAt - time);
    }

    public readonly struct EvSkillReturned
    {
        public readonly EntityId Source;
        public readonly SkillOrigin Origin;
        public readonly SimVec3 Position;
        public EvSkillReturned(EntityId source, SkillOrigin origin, SimVec3 position)
        { Source = source; Origin = origin; Position = position; }
    }

    public readonly struct EvSkillOperation
    {
        public readonly EntityId Source;
        public readonly SkillNodeId Skill;
        public EvSkillOperation(EntityId source,SkillNodeId skill){Source=source;Skill=skill;}
    }
}
