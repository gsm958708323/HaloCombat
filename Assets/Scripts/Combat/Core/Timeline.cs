using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum ClipKind : byte
    {
        CancelTag = 1,
        Move = 2,
        Hitbox = 3
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
        public static readonly HitProfileBake G1Melee = new HitProfileBake();

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
    }

    public sealed class TimelineLibrary
    {
        readonly Dictionary<int, TimelineSO> _map = new Dictionary<int, TimelineSO>(32);

        public void Register(TimelineSO so)
        {
            if (so == null || !so.Id.IsValid)
                throw new ArgumentException("Invalid TimelineSO");
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

    struct LiveClip
    {
        public IClipHandler Handler;
        public ClipRuntime Rt;
    }

    public sealed class TimelinePlayer
    {
        readonly List<LiveClip> _live = new List<LiveClip>(8);
        bool[] _clipOpened = new bool[8];
        bool[] _payloadFired = new bool[8];
        TimelineSO _so;
        float _time;
        bool _playing;

        public bool IsPlaying => _playing;
        public float Time => _time;
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
            float prev = _time;
            _time += dt;
            OpenDue(self);
            TickLive(dt);
            CloseDue();
            FirePayloads(self, prev);
            if (_time >= _so.Duration)
                StopInternal(false);
        }

        void OpenDue(Actor self)
        {
            var clips = _so.Clips;
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
            {
                if (_clipOpened[i] || clips[i].Start > _time) continue;
                _clipOpened[i] = true;
                var rt = new ClipRuntime
                {
                    Self = self,
                    Clip = clips[i],
                    Start = clips[i].Start,
                    End = clips[i].End
                };
                var handler = ClipHandlerFactory.Create(clips[i].Kind);
                handler.Open(rt);
                _live.Add(new LiveClip { Handler = handler, Rt = rt });
            }
        }

        void TickLive(float dt)
        {
            for (int i = 0; i < _live.Count; i++)
                _live[i].Handler.Tick(_live[i].Rt, dt);
        }

        void CloseDue()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var live = _live[i];
                if (_time < live.Rt.End) continue;
                live.Handler.Close(live.Rt, false);
                _live.RemoveAt(i);
            }
        }

        void FirePayloads(Actor self, float prev)
        {
            var payloads = _so.Payloads;
            if (payloads == null || self.World == null) return;
            float atk = 0f;
            if (self.TryGetComp<AttributeSet>(out var attr))
                atk = attr.GetFinal(AttrId.Atk);
            for (int i = 0; i < payloads.Length; i++)
            {
                if (_payloadFired[i]) continue;
                float t = payloads[i].Time;
                bool due = (t > prev && t <= _time) || (prev <= 0f && t <= _time && t >= 0f && !_payloadFired[i]);
                if (!due) continue;
                _payloadFired[i] = true;
                self.World.Deliver(payloads[i].Effects, self, null, atk);
            }
        }

        void StopInternal(bool interrupted)
        {
            for (int i = 0; i < _live.Count; i++)
                _live[i].Handler.Close(_live[i].Rt, interrupted);
            _live.Clear();
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
