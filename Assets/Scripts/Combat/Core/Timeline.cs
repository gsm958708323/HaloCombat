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

    [Serializable]
    public struct TimelineClip
    {
        public float Start;
        public float End;
        public ClipKind Kind;
        public float MoveX, MoveY, MoveZ;
        public float Steer;
        public float HitRadius;
        public float HitOffsetX, HitOffsetY, HitOffsetZ;
        public IEffect[] OnHit;
    }

    [Serializable]
    public struct TimelinePayload
    {
        public float Time;
        public IEffect[] Effects;
    }

    [Serializable]
    public sealed class TimelineSO
    {
        public static readonly HitProfileBake G1Melee = new HitProfileBake
        {
            // The baseline G1 bag owns the short melee hitstop. Knockdown is opt-in and
            // deliberately remains outside the default Season One profile.
            Damage = new DamageEffect { Coeff = 1f, CanCrit = true, UseSnapshotAtk = true, HitstopFrames = 3 }
        };

        public TimelineId Id;
        public float Duration = 0.55f;
        public TimelineClip[] Clips = Array.Empty<TimelineClip>();
        public TimelinePayload[] Payloads = Array.Empty<TimelinePayload>();

        public static TimelineSO G1()
        {
            return new TimelineSO
            {
                Id = TimelineId.TL_G1,
                Duration = 0.55f,
                Clips = new[]
                {
                    new TimelineClip { Start = 0.12f, End = 0.40f, Kind = ClipKind.CancelTag },
                    new TimelineClip { Start = 0.08f, End = 0.28f, Kind = ClipKind.Move, MoveX = 0.6f, Steer = 0f },
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

        public static TimelineSO G2()
        {
            return new TimelineSO
            {
                Id = TimelineId.TL_G2,
                Duration = 0.40f,
                Clips = new[]
                {
                    new TimelineClip { Start = 0.00f, End = 0.20f, Kind = ClipKind.CancelTag },
                    new TimelineClip { Start = 0.00f, End = 0.12f, Kind = ClipKind.Move, MoveX = 0.25f, Steer = 0f }
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

        public static TimelineSO Dodge()
        {
            return new TimelineSO
            {
                Id = TimelineId.TL_Dodge,
                Duration = 0.40f,
                Clips = new[]
                {
                    new TimelineClip { Start = 0.00f, End = 0.28f, Kind = ClipKind.Move, MoveX = 1.2f, Steer = 0f },
                    new TimelineClip { Start = 0.04f, End = 0.22f, Kind = ClipKind.IFrame },
                    new TimelineClip { Start = 0.24f, End = 0.40f, Kind = ClipKind.CancelTag }
                },
                Payloads = Array.Empty<TimelinePayload>()
            };
        }

        public static TimelineSO Homing()
        {
            return new TimelineSO
            {
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

    public interface IClipHandler
    {
        void Open(in ClipRuntime rt);
        void Tick(in ClipRuntime rt, float dt);
        void Close(in ClipRuntime rt, bool interrupted);
    }

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

    public sealed class CancelTagClipHandler : IClipHandler
    {
        public void Open(in ClipRuntime rt)
            => rt.Self.GetComp<TagComp>().Add(CommonTags.Cancel, 1, TagSource.Effect("Clip.Cancel"));

        public void Tick(in ClipRuntime rt, float dt) { }

        public void Close(in ClipRuntime rt, bool interrupted)
            => rt.Self.GetComp<TagComp>().Remove(CommonTags.Cancel, 1, TagSource.Effect("Clip.Cancel"));
    }

    public sealed class MoveClipHandler : IClipHandler
    {
        SimVec3 _worldTotal;
        float _duration = 1e-5f;

        public void Open(in ClipRuntime rt)
        {
            var loco = rt.Self.GetComp<LocomotionComp>();
            float yaw = loco.FacingForSkillMove();
            _worldTotal = RotateLocal(rt.Clip.MoveX, rt.Clip.MoveY, rt.Clip.MoveZ, yaw);
            _duration = rt.End - rt.Start;
            if (_duration < 1e-5f) _duration = 1e-5f;
            loco.SetClipSteer(rt.Clip.Steer);
        }

        public void Tick(in ClipRuntime rt, float dt)
        {
            if (dt <= 0f) return;
            float k = dt / _duration;
            if (k > 1f) k = 1f;
            rt.Self.GetComp<LocomotionComp>().RequestSkillDelta(
                _worldTotal.X * k, _worldTotal.Y * k, _worldTotal.Z * k);
        }

        public void Close(in ClipRuntime rt, bool interrupted)
            => rt.Self.GetComp<LocomotionComp>().ClearClipSteer();

        static SimVec3 RotateLocal(float x, float y, float z, float yawDeg)
        {
            double r = yawDeg * Math.PI / 180.0;
            float c = (float)Math.Cos(r);
            float s = (float)Math.Sin(r);
            return new SimVec3(x * c - z * s, y, x * s + z * c);
        }
    }

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

    public sealed class IFrameClipHandler : IClipHandler
    {
        public void Open(in ClipRuntime rt)
            => rt.Self.GetComp<TagComp>().Add(CommonTags.Invincible, 1, TagSource.Effect("Clip.IFrame"));

        public void Tick(in ClipRuntime rt, float dt) { }

        public void Close(in ClipRuntime rt, bool interrupted)
            => rt.Self.GetComp<TagComp>().Remove(CommonTags.Invincible, 1, TagSource.Effect("Clip.IFrame"));
    }

    struct LiveClip
    {
        public IClipHandler Handler;
        public ClipRuntime Rt;
        public bool ClosePending;
    }

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

        public void Play(TimelineSO so)
        {
            StopInternal(true);
            _so = so ?? throw new ArgumentNullException(nameof(so));
            _time = 0f;
            _playing = true;
            EnsureFlags(_so.Clips?.Length ?? 0, ref _clipOpened);
            EnsureFlags(_so.Payloads?.Length ?? 0, ref _payloadFired);
        }

        public void Stop() => StopInternal(true);

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

        static void EnsureFlags(int n, ref bool[] flags)
        {
            if (flags.Length < n) flags = new bool[n];
            for (int i = 0; i < flags.Length; i++) flags[i] = false;
        }
    }
}
