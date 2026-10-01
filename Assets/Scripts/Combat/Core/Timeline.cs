using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum ClipKind : byte
    {
        CancelTag = 1,
        Move = 2,
        Hitbox = 3,
        IFrame = 4
    }

    /// <summary>
    /// 时间片定义：仅在 [Start, End) 内生效。CancelTag 打开期间授予 Cancel 标签，IFrame 授予 Invincible 标签，
    /// Hitbox 打开期间把 OnHit 效果包交给 HitDetectService 检测，Move 按本地坐标推进位移。
    /// </summary>
    [Serializable]
    public struct TimelineClip
    {
        public float Start;
        public float End;
        public ClipKind Kind;
        // MoveX/MoveY/MoveZ are authored in the actor's local frame: +Z advances along
        // the facing and +X is the actor's right (see TransformComp.YawDegrees).
        public float MoveX, MoveY, MoveZ;
        public float Steer;
        public float HitRadius;
        public float HitOffsetX, HitOffsetY, HitOffsetZ;
        public IEffect[] OnHit;
    }

    /// <summary>时间点效果包：时间轴推进到 Time 的那一帧整包投递。数组顺序即投递顺序，同一帧多个 payload 按下标依次执行。</summary>
    [Serializable]
    public struct TimelinePayload
    {
        public float Time;
        public IEffect[] Effects;
    }

    /// <summary>
    /// 一段技能时间轴的数据模板（只读共享，运行时状态全在 TimelinePlayer，不要挂在实体上改这里）。
    /// Clips 与 Payloads 的数组顺序就是执行顺序：同一帧到期的按数组下标从小到大处理。
    /// ControlTags 是由导演持有的角色控制标签；播放不会因 Duration 自动通知结束，
    /// 必须由调用方在 FlushPendingCloses 返回 true 后收尾。
    /// </summary>
    [Serializable]
    public sealed class TimelineSO
    {
        public static readonly HitProfileBake G1Melee = new HitProfileBake
        {
            // The baseline G1 bag owns the short melee hitstop. Knockdown is opt-in and
            // deliberately remains outside the default Season One profile.
            Damage = new DamageEffect { Coeff = 1f, CanCrit = true, UseSnapshotAtk = true, SourceHitstopFrames = 3, TargetHitstopFrames = 3 }
        };

        public TimelineId Id;
        public float Duration = 0.55f;
        public TagId[] ControlTags = Array.Empty<TagId>();
        public bool ScaleWithActionSpeed;
        public string AnimatorState;
        public TimelineClip[] Clips = Array.Empty<TimelineClip>();
        public TimelinePayload[] Payloads = Array.Empty<TimelinePayload>();

        /// <summary>S1 默认普通攻击：0.18s 开判定盒，并在此后依次投递音效与火球。</summary>
        public static TimelineSO G1()
        {
            return new TimelineSO
            {
                ControlTags = new[] { CommonTags.BlockMove, CommonTags.BlockRotate },
                Id = TimelineId.TL_G1,
                Duration = 0.55f,
                Clips = new[]
                {
                    new TimelineClip { Start = 0.12f, End = 0.40f, Kind = ClipKind.CancelTag },
                    new TimelineClip { Start = 0.08f, End = 0.28f, Kind = ClipKind.Move, MoveZ = 0.6f, Steer = 0f },
                    new TimelineClip
                    {
                        Start = 0.18f, End = 0.30f, Kind = ClipKind.Hitbox,
                        HitRadius = 0.8f, OnHit = G1Melee.Bake()
                    }
                },
                Payloads = new[]
                {
                    new TimelinePayload
                    {
                        Time = 0.18f,
                        Effects = new IEffect[] { new PlayCueEffect(101, "G1_Slash") }
                    },
                    new TimelinePayload
                    {
                        Time = 0.22f,
                        Effects = new IEffect[] { new SpawnProjectileEffect(CombatIds.Fireball) }
                    }
                }
            };
        }

        /// <summary>S1 技能 1：短前冲 + 短取消窗口，0.05s 同时投递音效与地面火 AoE。</summary>
        public static TimelineSO G2()
        {
            return new TimelineSO
            {
                ControlTags = new[] { CommonTags.BlockMove, CommonTags.BlockRotate },
                Id = TimelineId.TL_G2,
                Duration = 0.40f,
                Clips = new[]
                {
                    new TimelineClip { Start = 0.00f, End = 0.20f, Kind = ClipKind.CancelTag },
                    new TimelineClip { Start = 0.00f, End = 0.12f, Kind = ClipKind.Move, MoveZ = 0.25f, Steer = 0f }
                },
                Payloads = new[]
                {
                    new TimelinePayload
                    {
                        Time = 0.05f,
                        Effects = new IEffect[]
                        {
                            new PlayCueEffect(102, "G2"),
                            new SpawnAoeEffect(CombatIds.FireGround)
                        }
                    }
                }
            };
        }

        /// <summary>闪避：位移与无敌帧先手、取消窗口靠后，全程无 payload。</summary>
        public static TimelineSO Dodge()
        {
            return new TimelineSO
            {
                ControlTags = new[] { CommonTags.BlockMove, CommonTags.BlockRotate },
                Id = TimelineId.TL_Dodge,
                Duration = 0.40f,
                Clips = new[]
                {
                    new TimelineClip { Start = 0.00f, End = 0.28f, Kind = ClipKind.Move, MoveZ = 1.2f, Steer = 0f },
                    new TimelineClip { Start = 0.04f, End = 0.22f, Kind = ClipKind.IFrame },
                    new TimelineClip { Start = 0.24f, End = 0.40f, Kind = ClipKind.CancelTag }
                },
                Payloads = Array.Empty<TimelinePayload>()
            };
        }

        /// <summary>追踪弹起手：只有 0.02s 一发追踪弹 payload，没有 clip。</summary>
        public static TimelineSO Homing()
        {
            return new TimelineSO
            {
                ControlTags = new[] { CommonTags.BlockMove, CommonTags.BlockRotate },
                Id = TimelineId.TL_Homing,
                Duration = 0.20f,
                Payloads = new[]
                {
                    new TimelinePayload
                    {
                        Time = 0.02f,
                        Effects = new IEffect[] { new SpawnProjectileEffect(CombatIds.HomingBolt) }
                    }
                }
            };
        }
    }

    /// <summary>时间轴注册表：按 Id 索引，Id 非法或重复注册直接抛异常，让配置错误尽早暴露而不是静默覆盖。</summary>
    public sealed class TimelineLibrary
    {
        readonly Dictionary<int, TimelineSO> _map = new Dictionary<int, TimelineSO>(32);
        public int Count => _map.Count;
        public IEnumerable<TimelineSO> All => _map.Values;

        public void Register(TimelineSO so)
        {
            if (so == null || !so.Id.IsValid)
                throw new ArgumentException("Invalid TimelineSO");
            if (_map.ContainsKey(so.Id.Value))
                throw new InvalidOperationException("Duplicate timeline " + so.Id.Value);
            _map[so.Id.Value] = so;
        }

        public bool TryGet(TimelineId id, out TimelineSO so)
            => _map.TryGetValue(id.Value, out so);
    }

    /// <summary>
    /// 运行中 clip 的切片视图：StepStart/StepEnd 是本帧推进区间，ActiveStart/ActiveEnd 是它与 clip 区间 [Start, End) 的交集。
    /// Handler 只能拿 ActiveDelta 当 dt，用它才能保证跨帧补偿时不多算，也保证首个相交帧只结算实际重叠部分。
    /// </summary>
    public struct ClipRuntime
    {
        public Actor Self;
        public TimelineClip Clip;
        public float Start;
        public float End;
        public float StepStart;
        public float StepEnd;
        public float ActiveStart;
        public float ActiveEnd;
        public float ActiveDelta => ActiveEnd > ActiveStart ? ActiveEnd - ActiveStart : 0f;
    }

    /// <summary>clip 生命周期回调：Open 在首次与本帧相交时调用一次，Tick 只在 ActiveDelta 大于 0 时调用，Close 恰好调用一次（interrupted 表示被 Stop 提前打断）。</summary>
    public interface IClipHandler
    {
        void Open(in ClipRuntime rt);
        void Tick(in ClipRuntime rt, float dt);
        void Close(in ClipRuntime rt, bool interrupted);
    }

    /// <summary>按 ClipKind 分发 Handler；新增枚举值必须在这里补 case，否则抛 NotSupportedException。</summary>
    public static class ClipHandlerFactory
    {
        public static IClipHandler Create(ClipKind kind)
        {
            switch (kind)
            {
                case ClipKind.CancelTag: return new CancelTagClipHandler();
                case ClipKind.Move: return new MoveClipHandler();
                case ClipKind.Hitbox: return new HitboxClipHandler();
                case ClipKind.IFrame: return new IFrameClipHandler();
                default: throw new NotSupportedException(kind.ToString());
            }
        }
    }

    /// <summary>CancelTag：打开期间挂一层 Cancel 标签，让后续动作可以被取消窗口吃掉；关闭时按同一来源移除，不会误伤别的来源。</summary>
    public sealed class CancelTagClipHandler : IClipHandler
    {
        public void Open(in ClipRuntime rt)
            => rt.Self.GetComp<TagComp>().Add(CommonTags.Cancel, 1, TagSource.Effect("Clip.Cancel"));

        public void Tick(in ClipRuntime rt, float dt) { }

        public void Close(in ClipRuntime rt, bool interrupted)
            => rt.Self.GetComp<TagComp>().Remove(CommonTags.Cancel, 1, TagSource.Effect("Clip.Cancel"));
    }

    /// <summary>Move：打开瞬间把本地位移一次性旋转到世界系并按整段时长均摊，因此帧率变化不会改变总位移。</summary>
    public sealed class MoveClipHandler : IClipHandler
    {
        SimVec3 _worldTotal;
        float _duration = 1e-5f;

        /// <summary>打开时缓存整段位移与时长；FacingForSkillMove 允许技能起手先吸附朝向，之后的位移都基于这个朝向。</summary>
        public void Open(in ClipRuntime rt)
        {
            var loco = rt.Self.GetComp<LocomotionComp>();
            float yaw = loco.FacingForSkillMove();
            _worldTotal = RotateLocal(rt.Clip.MoveX, rt.Clip.MoveY, rt.Clip.MoveZ, yaw);
            _duration = rt.End - rt.Start;
            if (_duration < 1e-5f) _duration = 1e-5f;
            loco.SetClipSteer(rt.Clip.Steer);
        }

        /// <summary>按 dt/总时长 的比例发位移请求，k 夹到 1，避免单帧长于整段时超发。</summary>
        public void Tick(in ClipRuntime rt, float dt)
        {
            if (dt <= 0f) return;
            float k = dt / _duration;
            if (k > 1f) k = 1f;
            rt.Self.GetComp<LocomotionComp>().RequestSkillDelta(
                _worldTotal.X * k, _worldTotal.Y * k, _worldTotal.Z * k);
        }

        /// <summary>关闭时必须清掉 clip 转向，否则操控层的转向权重会残留到下一段动作。</summary>
        public void Close(in ClipRuntime rt, bool interrupted)
            => rt.Self.GetComp<LocomotionComp>().ClearClipSteer();

        static SimVec3 RotateLocal(float x, float y, float z, float yawDeg)
        {
            double r = yawDeg * Math.PI / 180.0;
            float c = (float)Math.Cos(r);
            float s = (float)Math.Sin(r);
            return new SimVec3(x * c + z * s, y, -x * s + z * c);
        }
    }

    /// <summary>Hitbox：打开期间由 HitDetectService 每帧查询圆范围，命中包只在这里烘焙一次，关闭立即失效。</summary>
    public sealed class HitboxClipHandler : IClipHandler
    {
        public void Open(in ClipRuntime rt)
        {
            rt.Self.GetComp<HitboxComp>().Open(
                rt.Clip.OnHit,
                rt.Clip.HitRadius,
                new SimVec3(rt.Clip.HitOffsetX, rt.Clip.HitOffsetY, rt.Clip.HitOffsetZ));
        }

        public void Tick(in ClipRuntime rt, float dt) { }

        public void Close(in ClipRuntime rt, bool interrupted)
            => rt.Self.GetComp<HitboxComp>().Close();
    }

    /// <summary>IFrame：打开期间挂 Invincible 标签，关闭按同来源移除一层，不会清掉其他来源（如 BeginIFrame）给的无敌。</summary>
    public sealed class IFrameClipHandler : IClipHandler
    {
        public void Open(in ClipRuntime rt)
            => rt.Self.GetComp<TagComp>().Add(CommonTags.Invincible, 1, TagSource.Effect("Clip.IFrame"));

        public void Tick(in ClipRuntime rt, float dt) { }

        public void Close(in ClipRuntime rt, bool interrupted)
            => rt.Self.GetComp<TagComp>().Remove(CommonTags.Invincible, 1, TagSource.Effect("Clip.IFrame"));
    }

    /// <summary>运行中 clip 的处理器 + 切片 + 「到期待关闭」标记；ClosePending 把关闭推迟到 FlushPendingCloses，避免遍历时改集合。</summary>
    struct LiveClip
    {
        public IClipHandler Handler;
        public ClipRuntime Rt;
        public bool ClosePending;
    }

    /// <summary>时间轴播放器：每个角色一份实例，严禁跨实体共享（_live/_clipOpened/_time 都是实例状态）。负责 clip 生命周期、payload 投递与结束收尾。</summary>
    public sealed class TimelinePlayer
    {
        readonly List<LiveClip> _live = new List<LiveClip>(8);
        bool[] _clipOpened = new bool[8];
        bool[] _payloadFired = new bool[8];
        bool _finishPending;
        TimelineSO _so;
        float _time;
        bool _playing;

        public bool IsPlaying => _playing;
        public float Time => _time;
        public float Duration => _so != null ? _so.Duration : 0f;
        public TimelineId Id => _so != null ? _so.Id : TimelineId.None;
        public TimelineSO Current => _so;

        /// <summary>开始播放新时间轴：先按 interrupted 关掉上一段，再重置两个标记数组；传空会抛异常。</summary>
        public void Play(TimelineSO so)
        {
            StopInternal(true);
            _so = so ?? throw new ArgumentNullException(nameof(so));
            _time = 0f;
            _playing = true;
            EnsureFlags(_so.Clips?.Length ?? 0, ref _clipOpened);
            EnsureFlags(_so.Payloads?.Length ?? 0, ref _payloadFired);
        }

        /// <summary>立即结束（按被打断语义关闭所有 clip）。</summary>
        public void Stop() => StopInternal(true);

        /// <summary>
        /// 推进一帧。触发规则统一是半开区间 (prev, now]：now 属于本帧，prev 已在上一帧处理过。
        /// 顺序固定：OpenIntersecting（本帧首次相交的 clip 立即 Open）→ TickLive（只对交集区间 Tick）
        /// → MarkDueClose（now 到达 End 的只标记待关）→ _time = next → FirePayloads → 到期置 _finishPending。
        /// 关闭延后到 FlushPendingCloses：Open 阶段可能新开 clip，当场移除会破坏 _live 遍历。
        /// </summary>
        public void Tick(float dt, Actor self)
        {
            if (!_playing || _so == null) return;
            if (dt < 0f) dt = 0f;
            float prev = _time;
            float next = _time + dt;
            if (next > _so.Duration) next = _so.Duration;
            if (next < prev) next = prev;
            OpenIntersecting(self, prev, next);
            TickLive(prev, next);
            MarkDueClose(next);
            _time = next;
            FirePayloads(self, prev);
            if (_time >= _so.Duration)
                _finishPending = true;
        }

        /// <summary>对与 (stepStart, stepEnd] 相交且未开过的 clip 立即 Open；_clipOpened 保证每段 clip 只开一次。先 Open 再算本帧 Tick 区间，所以起手帧不丢。</summary>
        void OpenIntersecting(Actor self, float stepStart, float stepEnd)
        {
            var clips = _so.Clips;
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
            {
                if (_clipOpened[i] || clips[i].End <= stepStart || clips[i].Start >= stepEnd)
                    continue;
                _clipOpened[i] = true;
                var rt = new ClipRuntime
                {
                    Self = self,
                    Clip = clips[i],
                    Start = clips[i].Start,
                    End = clips[i].End,
                    StepStart = stepStart,
                    StepEnd = stepEnd,
                    ActiveStart = Math.Max(stepStart, clips[i].Start),
                    ActiveEnd = Math.Min(stepEnd, clips[i].End)
                };
                var handler = ClipHandlerFactory.Create(clips[i].Kind);
                handler.Open(rt);
                _live.Add(new LiveClip { Handler = handler, Rt = rt, ClosePending = false });
            }
        }

        /// <summary>刷新每个运行中 clip 的本帧切片并 Tick；ActiveDelta 为 0 时跳过，避免给 Handler 传空 dt。</summary>
        void TickLive(float stepStart, float stepEnd)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                var live = _live[i];
                live.Rt.StepStart = stepStart;
                live.Rt.StepEnd = stepEnd;
                live.Rt.ActiveStart = Math.Max(stepStart, live.Rt.Start);
                live.Rt.ActiveEnd = Math.Min(stepEnd, live.Rt.End);
                _live[i] = live;
                if (live.Rt.ActiveDelta > 0f)
                    live.Handler.Tick(live.Rt, live.Rt.ActiveDelta);
            }
        }

        /// <summary>只标记不关闭：已到达 End 的 clip 置 ClosePending，真正 Close 留到 FlushPendingCloses，避免在 TickLive 遍历中改 _live。</summary>
        void MarkDueClose(float currentTime)
        {
            for (int i = 0; i < _live.Count; i++)
            {
                var live = _live[i];
                if (currentTime >= live.Rt.End)
                {
                    live.ClosePending = true;
                    _live[i] = live;
                }
            }
        }

        /// <summary>
        /// 收尾并返回「整段技能是否真正结束」：先关闭所有待关 clip；只有当时间轴到期且没有任何存活 clip 时才复位播放器并返回 true。
        /// 返回 true 的那一帧，调用方（SkillDirectorComp.FlushTimeline）才应通知状态机技能结束，否则会提前打断收尾动作。
        /// </summary>
        public bool FlushPendingCloses()
        {
            if (!_playing || _so == null)
                return false;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];
                if (!live.ClosePending) continue;
                live.Handler.Close(live.Rt, false);
                _live.RemoveAt(i);
            }

            if (!_finishPending || _live.Count != 0)
                return false;

            _finishPending = false;
            _playing = false;
            _so = null;
            _time = 0f;
            return true;
        }

        /// <summary>
        /// 按 (prev, now] 投递到期 payload；首帧额外兜底 Time == 0 的条目（半开区间会漏掉 t == 0）。
        /// Atk 与 target 都取自投递当帧：atk 是施法者当前最终 Atk，target 是行为树黑板里选中的目标（可为 null）。
        /// </summary>
        void FirePayloads(Actor self, float prev)
        {
            var payloads = _so.Payloads;
            if (payloads == null || self.World == null) return;
            float atk = 0f;
            if (self.TryGetComp<AttributeSet>(out var attr))
                atk = attr.GetFinal(AttrId.Atk);
            Actor target = null;
            if (self.TryGetComp<BehaviorTreeComp>(out var bt) && self.World.TryGetActor(bt.Board.Target, out var selected))
                target = selected;
            for (int i = 0; i < payloads.Length; i++)
            {
                if (_payloadFired[i]) continue;
                float t = payloads[i].Time;
                bool due = (t > prev && t <= _time) || (prev <= 0f && t <= _time && t >= 0f && !_payloadFired[i]);
                if (!due) continue;
                _payloadFired[i] = true;
                self.World.Deliver(payloads[i].Effects, self, target, atk);
            }
        }

        /// <summary>关闭所有存活 clip 后立即复位；这里不做结束通知，收尾由调用方决定。</summary>
        void StopInternal(bool interrupted)
        {
            for (int i = 0; i < _live.Count; i++)
                _live[i].Handler.Close(_live[i].Rt, interrupted);
            _live.Clear();
            _finishPending = false;
            _playing = false;
            _so = null;
            _time = 0f;
        }

        /// <summary>复用标记数组：长度不够才扩容，然后把用到的前缀清零，避免每帧新分配。</summary>
        static void EnsureFlags(int n, ref bool[] flags)
        {
            if (flags.Length < n) flags = new bool[n];
            for (int i = 0; i < flags.Length; i++) flags[i] = false;
        }
    }
}
