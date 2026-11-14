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

        public void Open(IEffect[] onHit, float radius, in SimVec3 localOffset)
        {
            Close();
            IsOpen = true;
            BakedOnHit = onHit ?? Array.Empty<IEffect>();
            Radius = radius > 0f ? radius : 0.8f;
            LocalOffset = localOffset;
            _recorded.Clear();
        }

        public void Close()
        {
            IsOpen = false;
            BakedOnHit = Array.Empty<IEffect>();
            Radius = 0f;
            LocalOffset = SimVec3.Zero;
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
            if (!skill.IsValid || !timeline.IsValid)
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

    [Serializable]
    public struct ComboEntry
    {
        public SkillNodeId[] PreSkills;
        public InputToken Input;
        public int[] RequiredTags;
        public int Priority;
        public SkillNodeId ToSkill;
        public TimelineId Timeline;
    }

    public readonly struct ComboResolveResult
    {
        public readonly SkillNodeId ToSkill;
        public readonly TimelineId Timeline;
        public readonly int Priority;
        public ComboResolveResult(SkillNodeId toSkill, TimelineId timeline, int priority)
        {
            ToSkill = toSkill;
            Timeline = timeline;
            Priority = priority;
        }
    }

    public sealed class ComboTableDefinition
    {
        public ComboEntry[] Entries = Array.Empty<ComboEntry>();

        public bool TryResolve(SkillNodeId currentSkill, in InputToken input, TagComp tags, out ComboResolveResult result)
        {
            ComboEntry? best = null;
            int bestPri = int.MinValue;
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                if (!e.Input.Equals(input)) continue;
                if (!PreMatches(e.PreSkills, currentSkill)) continue;
                if (!TagsMatch(e.RequiredTags, tags)) continue;
                if (e.Priority < bestPri) continue;
                best = e;
                bestPri = e.Priority;
            }

            if (best.HasValue)
            {
                var e = best.Value;
                result = new ComboResolveResult(e.ToSkill, e.Timeline, e.Priority);
                return true;
            }

            result = default;
            return false;
        }

        static bool PreMatches(SkillNodeId[] pres, SkillNodeId current)
        {
            if (pres == null || pres.Length == 0)
                return !current.IsValid;
            for (int i = 0; i < pres.Length; i++)
                if (pres[i] == current) return true;
            return false;
        }

        static bool TagsMatch(int[] required, TagComp tags)
        {
            if (required == null || required.Length == 0) return true;
            for (int i = 0; i < required.Length; i++)
                if (!tags.Has(new TagId(required[i]))) return false;
            return true;
        }
    }

    public sealed class ComboComp : Comp
    {
        readonly ComboTableDefinition _table;
        InputBufferComp _input;
        TagComp _tags;
        SkillDirectorComp _director;

        public ComboComp(ComboTableDefinition table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        protected override void OnAttach()
        {
            _input = Self.GetComp<InputBufferComp>();
            _tags = Self.GetComp<TagComp>();
            _director = Self.GetComp<SkillDirectorComp>();
        }

        protected override void OnDetach()
        {
            _input = null;
            _tags = null;
            _director = null;
        }

        public bool TryResolve(out ComboResolveResult result)
        {
            result = default;
            if (_input == null || !_input.TryPeek(out var token))
                return false;
            var current = _director != null ? _director.CurrentSkill : SkillNodeId.None;
            if (!_table.TryResolve(current, token, _tags, out result))
                return false;
            _input.Consume();
            return true;
        }
    }

    public sealed class SkillDirectorComp : Comp
    {
        readonly TimelineLibrary _library;
        readonly TimelinePlayer _player = new TimelinePlayer();
        StateMachineComp _fsm;
        TagComp _tags;
        LocomotionComp _loco;
        SkillNodeId _currentSkill = SkillNodeId.None;

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
            Func<float, float> advanceBudget = null, Func<int, bool> payPulse = null, Func<SkillAim?> aimResolver = null,
            ChargeDefinition charge = null,float power=1)
        {
            if(Self.World!=null&&Self.World.IsActorStopped(Self))return false;
            if (_tags != null &&
                (_tags.Has(CommonTags.Dead) || _tags.Has(CommonTags.Stunned) ||
                 _tags.Has(CommonTags.Downed) || _tags.Has(CommonTags.Silence)))
                return false;
            if (!_library.TryGet(timelineId, out var so))
                throw new InvalidOperationException("Missing timeline " + timelineId);

            if (!_fsm.TryEnter(ActivityId.Attack, new ActivityEnterArgs { Reason = "PlaySkill" }))
                return false;

            _loco?.RequestSnapYaw();
            if (_player.IsPlaying)
                _player.Stop();

            _currentSkill = skill;
            CurrentOrigin = new SkillOrigin(skill,Self.World.NextCastId(),caster:Self.Id);
            _player.Play(so, aim, advanceBudget, payPulse, aimResolver, charge,power,CurrentOrigin);
            if (Self.TryGetComp<BuffComp>(out var buffs))
                buffs.DispatchOnOwnerCast();
            return true;
        }

        public void Stop(DirectorStopReason reason)
        {
            var skill = _currentSkill;
            bool wasPlaying = _player.IsPlaying;
            _player.Stop();
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
            _player.Tick(dt, Self);
            FinishIfStopped();
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
            _director.Play(resolved.ToSkill, resolved.Timeline);
        }
    }
}
