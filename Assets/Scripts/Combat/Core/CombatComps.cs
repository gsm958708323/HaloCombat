using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>技能停止原因，只用于让调用方区分上下文（表现/日志），不参与结算。</summary>
    public enum DirectorStopReason : byte
    {
        Finished = 0,
        Replaced = 1,
        Hit = 2,
        Dead = 3,
        Manual = 4,
        Detach = 5,
        Knockdown = 6
    }

    /// <summary>技能槽位：普通攻击 + 两个技能槽。</summary>
    public enum SkillSlot : byte { Normal = 0, Skill1 = 1, Skill2 = 2 }

    /// <summary>阵营归属。运行时体（子弹/AoE）也带它，只为让伤害归属与敌我过滤正确；敌我判定就是 TeamId 不相等，没有阵营关系表。</summary>
    public sealed class TeamComp : Comp
    {
        public int TeamId { get; private set; }
        public TeamComp(int teamId) => TeamId = teamId;
        public void SetTeam(int teamId) => TeamId = teamId;
        public bool IsHostileTo(TeamComp other) => other != null && TeamId != other.TeamId;
    }

    /// <summary>无敌帧计时与「是否无敌」的持有者。Hp 本体存在 AttributeSet，这里不保存血量。</summary>
    public sealed class HealthComp : Comp
    {
        TagComp _tags;
        float _iframe;
        public override bool WantsTick => true;
        public bool InIFrame => _iframe > 0f;

        // Source BulletState.CanHit skips any target with immuneTime > 0, so an
        // invulnerable body is passed through without consuming the projectile.
        /// <summary>无敌帧计时或 Invincible 标签任一为真即不可命中，供子弹与受击判定提前跳过。</summary>
        public bool IsInvulnerable
        {
            get
            {
                if (_iframe > 0f) return true;
                var tags = _tags;
                return tags != null && tags.Has(CommonTags.Invincible);
            }
        }

        protected override void OnAttach() => _tags = Self.GetComp<TagComp>();
        protected override void OnDetach() { _iframe = 0f; _tags = null; }

        /// <summary>延长无敌：与已有计时取较大者（不叠加），从无到有时加一层 Invincible 标签；计时归零才由 Tick 移除。</summary>
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

    /// <summary>攻击判定盒：开启期间由 HitDetectService 每帧查询；_recorded 保证一次开启内同一目标只结算一次，Open/Close 都会清空记录。</summary>
    public sealed class HitboxComp : Comp
    {
        readonly HashSet<long> _recorded = new HashSet<long>();
        public bool IsOpen { get; private set; }
        public float Radius { get; private set; }
        public SimVec3 LocalOffset { get; private set; }
        public IEffect[] BakedOnHit { get; private set; } = Array.Empty<IEffect>();

        /// <summary>开启判定：先 Close 清掉上一次记录，再烘焙效果包、半径（非正回落到 0.8）与本地偏移。</summary>
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

        /// <summary>把 EntityId 压成 long（Index 在高 32 位、Generation 在低 32 位），以便放进 HashSet 去重。</summary>
        public static long Pack(EntityId id)
            => ((long)id.Index << 32) | (uint)id.Generation;

        public static EntityId Unpack(long packed)
            => new EntityId((int)(packed >> 32), (int)(uint)packed);

        /// <summary>本次开启内首次记录该目标返回 true；未开启、非法 Id 或已记录返回 false。</summary>
        public bool TryRecord(EntityId id)
        {
            if (!IsOpen || !id.IsValid) return false;
            return _recorded.Add(Pack(id));
        }

        protected override void OnDetach() => Close();
    }

    /// <summary>技能装载表：槽位映射到技能与时间轴，重复装备同一槽位直接覆盖；EquipNormalG1G2Defaults 是 S1 默认配置。</summary>
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

    /// <summary>派生条目：输入、前置技能、必需标签三者同时满足才算命中，Priority 更大者优先。</summary>
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

    /// <summary>派生结果（值类型），查表失败时调用方拿到 default。</summary>
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

    /// <summary>派生表：TryResolve 取满足条件里优先级最高的条目；PreSkills 为空表示只接受「当前没有技能」的起手。</summary>
    public sealed class ComboTableSO
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

    /// <summary>派生组件：从输入缓冲 peek 一个 token 去查表，命中后才 Consume——失败不消耗输入，允许后续逻辑继续使用同一个 token。</summary>
    public sealed class ComboComp : Comp
    {
        readonly ComboTableSO _table;
        InputBufferComp _input;
        TagComp _tags;
        SkillDirectorComp _director;

        public ComboComp(ComboTableSO table)
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

        /// <summary>尝试产出一条派生；当前技能取 director.CurrentSkill，没有 director 时按 None 处理。</summary>
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

    /// <summary>
    /// 技能导演：持有一份每角色独立的 TimelinePlayer（严禁跨实体共享），负责播放守卫、冷却、空中/目标限制、时间轴推进与收尾通知。
    /// Play(skill, timelineId) 只做通用守卫；Play(skill) 走技能目录，额外检查冷却、CanUseInAir 与 RequiresTarget。
    /// Timeline control restrictions are leased tags, released on every stop path.
    /// </summary>
    public sealed class SkillDirectorComp : Comp
    {
        readonly TimelineLibrary _library;
        readonly SkillCatalog _skills;
        readonly TimelinePlayer _player = new TimelinePlayer();
        readonly Dictionary<int, float> _cooldownUntil = new Dictionary<int, float>(8);
        StateMachineComp _fsm;
        TagComp _tags;
        LocomotionComp _loco;
        SkillNodeId _currentSkill = SkillNodeId.None;
        SkillAnimationMode _currentAnimationMode = SkillAnimationMode.Attack;

        public SkillNodeId CurrentSkill => _currentSkill;
        public SkillAnimationMode CurrentAnimationMode => _currentAnimationMode;
        public float CurrentTime => _player.Time;
        public float CurrentDuration => _player.Duration;
        public bool UsesSkillCatalog => _skills != null;
        public bool IsPlaying => _player.IsPlaying;
        TagLease _controls;
        public bool CanStartSkill => !Self.World.IsActorStopped(Self) && (_tags == null || !_tags.Has(CommonTags.BlockSkill));
        public string AnimatorState => _player.Current != null ? _player.Current.AnimatorState : string.Empty;
        public override bool WantsTick => true;

        public SkillDirectorComp(TimelineLibrary library, SkillCatalog skills = null)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _skills = skills;
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
            _cooldownUntil.Clear();
        }

        /// <summary>按显式时间轴播放（跳过技能目录的冷却/前置检查，仍受死亡/眩晕/倒地/沉默守卫）。</summary>
        public bool Play(SkillNodeId skill, TimelineId timelineId)
            => PlayInternal(skill, timelineId, SkillAnimationMode.Attack);

        /// <summary>播放核心：死亡/眩晕/倒地/沉默直接拒绝；时间轴缺失抛异常（属配置错误，不该静默失败）；播放前请求朝向吸附并中断上一段。</summary>
        bool PlayInternal(SkillNodeId skill, TimelineId timelineId, SkillAnimationMode animationMode)
        {
            if (!CanStartSkill) return false;
            if (!_library.TryGet(timelineId, out var so))
                throw new InvalidOperationException("Missing timeline " + timelineId);

            if (_player.IsPlaying) Stop(DirectorStopReason.Replaced);
            _loco?.SnapForCast();

            if (!_fsm.TryEnter(ActivityId.Attack, new ActivityEnterArgs { Reason = "PlaySkill" })) return false;
            _currentSkill = skill;
            _currentAnimationMode = animationMode;
            _controls = _tags != null ? _tags.Acquire(so.ControlTags) : default;
            _player.Play(so);
            if (Self.TryGetComp<BuffComp>(out var buffs))
                buffs.DispatchOnOwnerCast();
            return true;
        }

        /// <summary>走技能目录播放：先做冷却/空中/目标前置检查，成功后才写入冷却时间。</summary>
        public bool Play(SkillNodeId skill)
        {
            if (_skills == null)
                throw new InvalidOperationException("Skill catalog is not installed");
            var definition = _skills.Require(skill);
            if (!CanPlay(definition))
                return false;
            if (!PlayInternal(skill, definition.Timeline, definition.AnimationMode))
                return false;
            if (definition.Cooldown > 0f && Self.World != null)
                _cooldownUntil[skill.Value] = Self.Time.Time + definition.Cooldown;
            return true;
        }

        /// <summary>技能目录播放的前置条件：冷却未到、不限制空中时才能在空中用、RequiresTarget 时行为树必须有合法目标。</summary>
        bool CanPlay(SkillDefinition definition)
        {
            if (definition == null)
                return false;
            if (Self.World != null && _cooldownUntil.TryGetValue(definition.Id.Value, out var readyAt) &&
                Self.Time.Time < readyAt)
                return false;
            if (!definition.CanUseInAir && _tags != null && _tags.Has(CommonTags.Airborne))
                return false;
            if (definition.RequiresTarget && Self.TryGetComp<BehaviorTreeComp>(out var bt) &&
                !CondHasTarget.IsTargetValid(new BtTick(Self, Self.World, bt.Board, Self.World != null ? Self.World.Time.Delta : 0f)))
                return false;
            return true;
        }

        /// <summary>立即停表并清空当前技能；reason 只用于调用方区分上下文。</summary>
        public void Stop(DirectorStopReason reason)
        {
            _player.Stop();
            _controls.Release();
            _controls = default;
            _currentSkill = SkillNodeId.None;
            _currentAnimationMode = SkillAnimationMode.Attack;
        }

        /// <summary>按 ScaleWithActionSpeed 把 ActionSpeed 折进 dt（下限 0.1，防止 0 速度把时间轴卡死），再交给自己的播放器。</summary>
        public override void Tick(float dt)
        {
            if (!_player.IsPlaying) return;
            float scale = 1f;
            if (_player.Current != null && _player.Current.ScaleWithActionSpeed &&
                Self.TryGetComp<AttributeSet>(out var attr))
                scale = Math.Max(.1f, attr.GetFinal(AttrId.ActionSpeed));
            _player.Tick(dt * scale, Self);
        }

        /// <summary>
        /// 由外部（状态机/结束帧）调用：只有播放器确认「整段到期且所有 clip 已关闭」时才返回 true，
        /// 此时才复位当前技能并通知状态机 Attack 结束；否则直接返回，避免提前打断收尾。
        /// </summary>
        public void FlushTimeline()
        {
            if (Self.Time.IsStopped || !_player.FlushPendingCloses())
                return;
            _controls.Release();
            _controls = default;
            _currentSkill = SkillNodeId.None;
            _currentAnimationMode = SkillAnimationMode.Attack;
            _fsm.NotifyActivityFinished(ActivityId.Attack, "TimelineFinished");
        }
    }

    /// <summary>玩家操作驱动：按输入缓冲里的 token 依次处理跳跃、闪避、派生技能；死亡/眩晕/倒地一票否决。跳跃要求 Grounded，闪避被沉默拒绝且失败不消耗输入。</summary>
    public sealed class PlayerCombatDriverComp : Comp
    {
        readonly PlayerCombatConfig _config;
        StateMachineComp _fsm;
        ComboComp _combo;
        SkillDirectorComp _director;
        LocomotionComp _loco;
        InputBufferComp _input;
        TagComp _tags;

        public override bool WantsTick => true;

        public PlayerCombatDriverComp(PlayerCombatConfig config = null)
        {
            _config = config;
        }

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

        /// <summary>每帧优先级：跳跃 → 闪避 → 派生；处理过的分支直接 return，避免同一 token 在一帧内被多条规则重复消费。</summary>
        public override void Tick(float dt)
        {
            if (!_director.CanStartSkill) return;

            var jumpInput = _config != null ? _config.JumpInput : InputToken.Jump;
            if (_input.TryPeek(out var token) && token.Equals(jumpInput))
            {
                if (_tags.Has(CommonTags.Grounded))
                {
                    _input.Consume();
                    _loco.ImpulseJump();
                    return;
                }
            }

            var dodgeInput = _config != null ? _config.DodgeInput : Season2Tokens.Dodge;
            var dodgeSkill = _config != null ? _config.DodgeSkill : SkillNodeId.Dodge;
            if (_input.TryPeek(out token) && token.Equals(dodgeInput))
            {
                if (_tags.Has(CommonTags.Silence))
                    return;

                var played = _config != null && !_config.HasDodge
                    ? false
                    : (_config != null ? _director.Play(dodgeSkill) : _director.Play(dodgeSkill, TimelineId.TL_Dodge));
                if (played)
                    _input.Consume();
                return;
            }

            if (!_combo.TryResolve(out var resolved))
                return;
            if (_director.UsesSkillCatalog)
                _director.Play(resolved.ToSkill);
            else
                _director.Play(resolved.ToSkill, resolved.Timeline);
        }
    }
}
