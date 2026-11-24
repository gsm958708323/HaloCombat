using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum DirectorStopReason : byte
    {
        Finished = 0,
        Replaced = 1,
        Hit = 2,
        Dead = 3,
        Manual = 4,
        Detach = 5,
        Knockdown = 6,
        Released = 7,
        EnergyDepleted = 8,
        TargetLost = 9,
        EarlyRelease = 10,
        InputCancelled = 11
    }

    public enum SkillSlot : byte { Normal = 0, Skill1 = 1, Skill2 = 2 }

    public sealed class TeamComp : Comp
    {
        public int TeamId { get; private set; }
        public TeamComp(int teamId) => TeamId = teamId;
        public void SetTeam(int teamId) => TeamId = teamId;
        public bool IsHostileTo(TeamComp other) => other != null && TeamId != other.TeamId;
    }

    public sealed class HealthComp : Comp
    {
        TagComp _tags;
        float _iframe;
        public override bool WantsTick => true;
        public bool InIFrame => _iframe > 0f;

        protected override void OnAttach() => _tags = Self.GetComp<TagComp>();
        protected override void OnDetach() { _iframe = 0f; _tags = null; }

        public void BeginIFrame(float seconds)
        {
            if (seconds <= 0f || _tags == null) return;
            if (_iframe <= 0f)
                _tags.Add(CommonTags.Invincible, 1, TagSource.Effect("IFrame"));
            if (seconds > _iframe) _iframe = seconds;
        }

        public override void Tick(float dt)
        {
            if (_iframe <= 0f) return;
            _iframe -= dt;
            if (_iframe > 0f) return;
            _iframe = 0f;
            _tags.Remove(CommonTags.Invincible, 1, TagSource.Effect("IFrame.End"));
        }
    }

    public sealed class HitboxComp : Comp
    {
        readonly HashSet<long> _recorded = new HashSet<long>();
        public bool IsOpen { get; private set; }
        public float Radius { get; private set; }
        public SimVec3 LocalOffset { get; private set; }
        public IEffect[] BakedOnHit { get; private set; } = Array.Empty<IEffect>();
        public SkillOrigin Origin {get;private set;}

        public void Open(IEffect[] onHit, float radius, in SimVec3 localOffset,SkillOrigin origin=default)
        {
            Close();
            IsOpen = true;
            BakedOnHit = onHit ?? Array.Empty<IEffect>();
            Radius = radius > 0f ? radius : 0.8f;
            LocalOffset = localOffset;
            Origin=origin;
            _recorded.Clear();
        }

        public void Close()
        {
            IsOpen = false;
            BakedOnHit = Array.Empty<IEffect>();
            Radius = 0f;
            LocalOffset = SimVec3.Zero;
            Origin=default;
            _recorded.Clear();
        }

        public static long Pack(EntityId id)
            => ((long)id.Index << 32) | (uint)id.Generation;

        public static EntityId Unpack(long packed)
            => new EntityId((int)(packed >> 32), (int)(uint)packed);

        public bool TryRecord(EntityId id)
        {
            if (!IsOpen || !id.IsValid) return false;
            return _recorded.Add(Pack(id));
        }

        protected override void OnDetach() => Close();
    }

    public sealed class LoadoutComp : Comp
    {
        struct Slot
        {
            public bool Occupied;
            public SkillNodeId Skill;
            public TimelineId Timeline;
        }

        readonly Slot[] _slots = new Slot[3];

        public void EquipSkill(SkillSlot slot, SkillNodeId skill, TimelineId timeline)
        {
            if (!skill.IsValid)
                throw new ArgumentException("EquipSkill");
            _slots[(int)slot] = new Slot { Occupied = true, Skill = skill, Timeline = timeline };
        }

        public void Unequip(SkillSlot slot) => _slots[(int)slot] = default;

        public bool TryGet(SkillSlot slot, out SkillNodeId skill, out TimelineId timeline)
        {
            var s = _slots[(int)slot];
            skill = s.Skill;
            timeline = s.Timeline;
            return s.Occupied;
        }

        public void EquipNormalG1G2Defaults()
        {
            EquipSkill(SkillSlot.Normal, SkillNodeId.G1, TimelineId.TL_G1);
            EquipSkill(SkillSlot.Skill1, SkillNodeId.G2, TimelineId.TL_G2);
        }

        protected override void OnDetach()
        {
            for (int i = 0; i < _slots.Length; i++)
                _slots[i] = default;
        }
    }

    public sealed class SkillDirectorComp : Comp
    {
        TimelineLibrary _library;
        public void ReplaceLibrary(TimelineLibrary library)=>_library=library??throw new ArgumentNullException(nameof(library));
        readonly TimelinePlayer _player = new TimelinePlayer();
        StateMachineComp _fsm;
        TagComp _tags;
        LocomotionComp _loco;
        SkillNodeId _currentSkill = SkillNodeId.None;
        TagLease _comboConfirm;
        readonly List<TagLease> _controls=new List<TagLease>();
        readonly Dictionary<string, int> _cancelChannels = new Dictionary<string, int>(StringComparer.Ordinal);
        public float TimelineTime=>_player.Time;
        public float TimelineDuration=>_player.Definition?.Duration??0;
        public string Animation=>IsPlaying?_player.Definition?.Animation:null;
        public float ActionRate=>IsPlaying&&_player.Definition.ScaleWithActionSpeed?Math.Max(.1f,Self.GetComp<AttributeSet>().GetFinal(AttrId.ActionSpeed)):1;
        public TagId[] ActiveControls=>IsPlaying?_player.Definition.ControlTags:Array.Empty<TagId>();
        public bool HasComboConfirmation=>_comboConfirm!=null;
        public bool IsCancelChannelOpen(string channel)
            => !string.IsNullOrEmpty(channel) && _cancelChannels.TryGetValue(channel, out int count) && count > 0;
        internal void AddCancelChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel)) return;
            _cancelChannels[channel] = _cancelChannels.TryGetValue(channel, out int count) ? count + 1 : 1;
        }
        internal void RemoveCancelChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel) || !_cancelChannels.TryGetValue(channel, out int count)) return;
            if (count <= 1) _cancelChannels.Remove(channel);
            else _cancelChannels[channel] = count - 1;
        }
        public bool CanStartSkill=>Self!=null&&Self.World!=null&&!Self.World.IsActorStopped(Self)&&
            (_tags==null||!_tags.Has(CommonTags.Dead)&&!_tags.Has(CommonTags.Stunned)&&!_tags.Has(CommonTags.Downed)&&!_tags.Has(CommonTags.Silence)&&!_tags.Has(CommonTags.BlockSkill));

        public SkillNodeId CurrentSkill => _currentSkill;
        public SkillOrigin CurrentOrigin { get; private set; }
        public bool IsPlaying => _player.IsPlaying;
        public bool IsCharging => _player.IsCharging;
        public float ChargeProgress => _player.ChargeProgress;
        public float CastPower => _player.IsCharging ? _player.ChargePower : _player.Power;
        public override bool WantsTick => true;

        public SkillDirectorComp(TimelineLibrary library)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _player.Released += PublishChargeRelease;
        }

        protected override void OnAttach()
        {
            _fsm = Self.GetComp<StateMachineComp>();
            Self.TryGetComp(out _tags);
            Self.TryGetComp(out _loco);
        }

        protected override void OnDetach()
        {
            Stop(DirectorStopReason.Detach);
            _fsm = null;
            _tags = null;
            _loco = null;
        }

        public bool Play(SkillNodeId skill, TimelineId timelineId, SkillAim aim = default,
            Func<float, float> advanceBudget = null, Func<int, bool> payPulse = null, Func<bool,SkillAim?> aimResolver = null,
            ChargeDefinition charge = null,float power=1,Action onAccepted = null)
        {
            if(!CanStartSkill)return false;
            if(aim.HasYaw&&(float.IsNaN(aim.Yaw)||float.IsInfinity(aim.Yaw)))return false;
            if (!_library.TryGet(timelineId, out var so))
                throw new InvalidOperationException("Missing timeline " + timelineId);

            if (!_fsm.TryEnter(ActivityId.Attack, new ActivityEnterArgs { Reason = "PlaySkill" }))
                return false;

            if (_player.IsPlaying)
                Stop(DirectorStopReason.Replaced);
            else ConsumeComboConfirm("Replaced");
            if(aim.HasYaw)_loco?.RequestAimYaw(aim.Yaw);
            _loco?.SnapForCast();

            _currentSkill = skill;
            CurrentOrigin = new SkillOrigin(skill,Self.World.NextCastId(),caster:Self.Id);
            _player.Play(so, aim, advanceBudget, payPulse, aimResolver, charge,power,CurrentOrigin);
            foreach(var tag in so.ControlTags??Array.Empty<TagId>())if(_tags!=null)_controls.Add(_tags.Acquire(tag));
            onAccepted?.Invoke();
            if (Self.TryGetComp<BuffComp>(out var buffs))
                buffs.DispatchOnOwnerCast();
            return true;
        }

        public void Stop(DirectorStopReason reason)
        {
            ConsumeComboConfirm(reason.ToString());
            var skill = _currentSkill;
            bool wasPlaying = _player.IsPlaying;
            _player.Stop();
            _cancelChannels.Clear();
            ReleaseControls();
            _currentSkill = SkillNodeId.None;
            _loco?.ClearClipSteer();
            _loco?.ClearPendingSkill();
            if (wasPlaying) Self.World?.Events.Publish(new EvSkillEnded(Self.Id, skill, reason,CurrentOrigin));
            if (reason == DirectorStopReason.Released || reason == DirectorStopReason.Manual || reason == DirectorStopReason.InputCancelled)
                _fsm?.NotifyActivityFinished(ActivityId.Attack, reason.ToString());
        }

        public override void Tick(float dt)
        {
            if (!_player.IsPlaying) return;
            _player.Tick(dt*ActionRate, Self);
            FinishIfStopped();
        }

        public void FlushTimeline()
        {
            if(!IsPlaying||Self.World.IsActorStopped(Self))return;
            _player.FlushPendingCloses();FinishIfStopped();
        }

        public void ReleaseCharge()
        {
            if (!_player.IsCharging) return;
            _player.ReleaseCharge();
            FinishIfStopped();
        }

        void PublishChargeRelease(float power) => Self.World?.Events.Publish(new EvSkillReleased(Self.Id, _currentSkill, power));

        void FinishIfStopped()
        {
            if (!_player.IsPlaying)
            {
                var skill = _currentSkill;
                ConsumeComboConfirm("Finished");
                ReleaseControls();
                _currentSkill = SkillNodeId.None;
                var aim=_player.LastAim;var origin=CurrentOrigin;float power=_player.Power;
                var reason=_player.EndReason;
                _fsm.NotifyActivityFinished(ActivityId.Attack, "TimelineFinished");
                Self.World?.Events.Publish(new EvSkillEnded(Self.Id, skill, reason,origin));
                if(reason==DirectorStopReason.Finished&&Self.World!=null)
                {
                    Self.World.TryGetActor(aim.Target,out var target);
                    Self.World.DispatchSkillEvent(SkillEventKind.Completed,new EffectContext{World=Self.World,Source=Self,Target=target,
                        SnapshotAtk=Self.TryGetComp<AttributeSet>(out var attr)?attr.GetFinal(AttrId.Atk):0,Power=power,Origin=origin,
                        Point=aim.Point,HasPoint=aim.HasPoint,Dir=LocomotionComp.ForwardFromYaw(aim.Yaw),HasDir=aim.HasPoint});
                }
            }
        }
        public void GrantComboConfirm(Actor target,in SkillOrigin origin)
        {
            if(!IsPlaying||_comboConfirm!=null||Self==null||!Self.IsActive||_tags==null||origin.CastId<=0||
                origin.CastId!=CurrentOrigin.CastId||origin.Skill!=CurrentOrigin.Skill||origin.Caster!=Self.Id||
                target==null||!target.IsActive||target.World!=Self.World||target.GetComp<TagComp>().Has(CommonTags.Dead)||
                !Self.TryGetComp<TeamComp>(out var team)||!target.TryGetComp<TeamComp>(out var other)||!team.IsHostileTo(other))return;
            _comboConfirm=_tags.Acquire(CommonTags.ComboConfirm);Self.World.Events.Publish(new EvComboConfirmation(Self.Id,target.Id,origin,"Granted"));
        }
        public void ConsumeComboConfirm(string reason="Lookup")
        {
            if(_comboConfirm==null)return;_comboConfirm.Dispose();_comboConfirm=null;
            Self?.World?.Events.Publish(new EvComboConfirmation(Self.Id,EntityId.Invalid,CurrentOrigin,reason));
        }
        void ReleaseControls(){foreach(var lease in _controls)lease.Dispose();_controls.Clear();}
    }

    public sealed class PlayerCombatDriverComp : Comp
    {
        StateMachineComp _fsm;
        ComboComp _combo;
        SkillDirectorComp _director;
        LocomotionComp _loco;
        InputBufferComp _input;
        TagComp _tags;

        public override bool WantsTick => true;

        protected override void OnAttach()
        {
            _fsm = Self.GetComp<StateMachineComp>();
            _combo = Self.GetComp<ComboComp>();
            _director = Self.GetComp<SkillDirectorComp>();
            _loco = Self.GetComp<LocomotionComp>();
            _input = Self.GetComp<InputBufferComp>();
            _tags = Self.GetComp<TagComp>();
        }

        protected override void OnDetach()
        {
            _fsm = null;
            _combo = null;
            _director = null;
            _loco = null;
            _input = null;
            _tags = null;
        }

        public override void Tick(float dt)
        {
            if (_tags.Has(CommonTags.Dead) || _tags.Has(CommonTags.Stunned) || _tags.Has(CommonTags.Downed))
                return;

            if (_input.TryPeek(out var token) && token.Equals(InputToken.Jump))
            {
                if (_tags.Has(CommonTags.Grounded))
                {
                    _input.Consume();
                    _loco.ImpulseJump();
                    return;
                }
            }

            if (_input.TryPeek(out token) && token.Equals(Season2Tokens.Dodge))
            {
                if (_tags.Has(CommonTags.Silence))
                    return;

                if (_director.Play(SkillNodeId.Dodge, TimelineId.TL_Dodge))
                    _input.Consume();
                return;
            }

            if (!_combo.TryResolve(out var resolved))
                return;
            if(_director.Play(resolved.ToSkill, resolved.Timeline))_combo.ConsumeInput();
        }
    }
}
