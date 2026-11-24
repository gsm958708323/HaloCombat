using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum ClipKind : byte
    {
        CancelTag = 1,
        Move = 2,
        Hitbox = 3,
        IFrame = 4,
        Melee = 5,
        Dash = 6
    }

    [Serializable]
    public struct TimelineClip
    {
        public int DiagnosticSite; // Opaque to Core; the optional observer's source catalog owns it.
        public float Start;
        public float End;
        public ClipKind Kind;
        public float MoveX, MoveY, MoveZ;
        public float Steer;
        public float HitRadius;
        public float HitOffsetX, HitOffsetY, HitOffsetZ;
        public IEffect[] OnHit;
        public bool OnPayload;
        public int PayloadIndex;
        public HitShape Shape;
        public float Length, Width, Angle;
        public int MaxTargets;
        public string CancelChannel;
    }

    [Serializable]
    public struct TimelinePayload
    {
        public float Time;
        public float RepeatInterval;
        public IEffect[] Effects;
        public bool OnPayload;
        public int PayloadIndex;
    }

    [Serializable]
    public sealed class TimelineDefinition
    {
        public static readonly HitProfileBake G1Melee = new HitProfileBake
        {
            // The baseline G1 bag owns the short melee hitstop. Knockdown is opt-in and
            // deliberately remains outside the default Season One profile.
            Damage = new DamageEffect { Coeff = 1f, CanCrit = true, UseSnapshotAtk = true, HitstopFrames = 3 }
        };

        public TimelineId Id;
        public float Duration = 0.55f;
        public string Animation;
        public TagId[] ControlTags=Array.Empty<TagId>();
        public bool ScaleWithActionSpeed;
        public TimelineClip[] Clips = Array.Empty<TimelineClip>();
        public TimelinePayload[] Payloads = Array.Empty<TimelinePayload>();

        public static TimelineDefinition G1()
        {
            return new TimelineDefinition
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

        public static TimelineDefinition G2()
        {
            return new TimelineDefinition
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

        public static TimelineDefinition Dodge()
        {
            return new TimelineDefinition
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

        public static TimelineDefinition Homing()
        {
            return new TimelineDefinition
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
        readonly Dictionary<int, TimelineDefinition> _map = new Dictionary<int, TimelineDefinition>(32);

        public void Register(TimelineDefinition so)
        {
            if (so == null || !so.Id.IsValid)
                throw new ArgumentException("Invalid TimelineDefinition");
            _map[so.Id.Value] = so;
        }

        public bool TryGet(TimelineId id, out TimelineDefinition so)
            => _map.TryGetValue(id.Value, out so);
    }

    public struct ClipRuntime
    {
        public Actor Self;
        public TimelineClip Clip;
        public float Start;
        public float End;
        public SkillAim Aim;
        public float Power;
        public int Sequence;
        public SkillOrigin Origin;
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
                case ClipKind.Melee: return new MeleeClipHandler();
                case ClipKind.Dash: return new DashClipHandler();
                default: throw new NotSupportedException(kind.ToString());
            }
        }
    }

    public sealed class CancelTagClipHandler : IClipHandler
    {
        public void Open(in ClipRuntime rt)
        {
            if (string.IsNullOrEmpty(rt.Clip.CancelChannel))
                rt.Self.GetComp<TagComp>().Add(CommonTags.Cancel, 1, TagSource.Effect("Clip.Cancel"));
            else
                rt.Self.GetComp<SkillDirectorComp>()?.AddCancelChannel(rt.Clip.CancelChannel);
        }

        public void Tick(in ClipRuntime rt, float dt) { }

        public void Close(in ClipRuntime rt, bool interrupted)
        {
            if (string.IsNullOrEmpty(rt.Clip.CancelChannel))
                rt.Self.GetComp<TagComp>().Remove(CommonTags.Cancel, 1, TagSource.Effect("Clip.Cancel"));
            else
                rt.Self.GetComp<SkillDirectorComp>()?.RemoveCancelChannel(rt.Clip.CancelChannel);
        }
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
                new SimVec3(rt.Clip.HitOffsetX, rt.Clip.HitOffsetY, rt.Clip.HitOffsetZ),rt.Origin);
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
    }

}
