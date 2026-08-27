using System;

namespace Combat.Core
{
    public sealed class AddTagEffect : IEffect
    {
        readonly TagId _tag;
        readonly int _stacks;
        public AddTagEffect(TagId tag, int stacks)
        {
            _tag = tag;
            _stacks = stacks < 1 ? 1 : stacks;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            ctx.Target.GetComp<TagComp>().Add(_tag, _stacks, TagSource.Effect("AddTag"));
        }
    }

    public sealed class RemoveTagEffect : IEffect
    {
        readonly TagId _tag;
        readonly int _stacks;
        public RemoveTagEffect(TagId tag, int stacks)
        {
            _tag = tag;
            _stacks = stacks < 1 ? 1 : stacks;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            ctx.Target.GetComp<TagComp>().Remove(_tag, _stacks, TagSource.Effect("RemoveTag"));
        }
    }

    public sealed class ApplyDurationEffect : IEffect
    {
        readonly DurationSpec _spec;
        readonly int _stacks;
        public ApplyDurationEffect(DurationSpec spec, int stacks = 1)
        {
            _spec = spec;
            _stacks = stacks;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            ctx.Target.GetComp<BuffComp>().Apply(_spec, ctx.Source, _stacks);
        }
    }

    public sealed class DispelEffect : IEffect
    {
        readonly DispelMode _mode;
        readonly int _key;
        readonly TagId _tag;
        readonly int _maxCount;
        public DispelEffect(DispelMode mode, int key = 0, TagId tag = default, int maxCount = 0)
        {
            _mode = mode;
            _key = key;
            _tag = tag;
            _maxCount = maxCount;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            ctx.Target.GetComp<BuffComp>().Dispel(_mode, _key, _tag, _maxCount);
        }
    }

    public sealed class PlayCueEffect : IEffect
    {
        readonly int _cueId;
        readonly string _name;
        public PlayCueEffect(int cueId, string name = "")
        {
            _cueId = cueId;
            _name = name ?? string.Empty;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World?.Events == null) return;
            var src = ctx.Source != null ? ctx.Source.Id : EntityId.Invalid;
            ctx.World.Events.Publish(new EvCue(_cueId, src, _name));
        }
    }

    public readonly struct SpawnProjectileIntent
    {
        public readonly EntityId Owner;
        public readonly int SpecId;
        public readonly SimVec3 Origin;
        public readonly float Yaw;
        public readonly float SnapshotAtk;
        public SpawnProjectileIntent(EntityId owner, int specId, SimVec3 origin, float yaw, float snapshotAtk)
        {
            Owner = owner;
            SpecId = specId;
            Origin = origin;
            Yaw = yaw;
            SnapshotAtk = snapshotAtk;
        }
    }

    public sealed class SpawnProjectileEffect : IEffect
    {
        readonly int _specId;
        public SpawnProjectileEffect(int specId) => _specId = specId;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Source == null) return;
            var tf = ctx.Source.GetComp<TransformComp>();
            float atk = ctx.SnapshotAtk;
            if (atk == 0f && ctx.Source.TryGetComp<AttributeSet>(out var attr))
                atk = attr.GetFinal(AttrId.Atk);
            ctx.World.Intents.Post(new SpawnProjectileIntent(
                ctx.Source.Id, _specId, tf.Position, tf.YawDegrees, atk));
        }
    }

    public sealed class SpawnAoeEffect : IEffect
    {
        readonly int _specId;
        public SpawnAoeEffect(int specId) => _specId = specId;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null) return;
            if (!ctx.World.Aoes.TryGet(_specId, out var def))
                throw new InvalidOperationException("Unknown AoE " + _specId);

            SimVec3 origin;
            if (ctx.HasPoint) origin = ctx.Point;
            else if (ctx.Source != null && ctx.Source.TryGetComp<TransformComp>(out var stf))
                origin = stf.Position;
            else origin = SimVec3.Zero;

            float snap = ctx.SnapshotAtk;
            if (snap == 0f && ctx.Source != null && ctx.Source.TryGetComp<AttributeSet>(out var attr))
                snap = attr.GetFinal(AttrId.Atk);

            var id = ctx.World.SpawnActor(new ActorSpawnSpec("aoe"));
            if (!ctx.World.TryGetActor(id, out var aoe) || aoe == null) return;

            aoe.GetComp<TransformComp>().Position = origin;
            if (ctx.Source != null && ctx.Source.TryGetComp<TransformComp>(out var yawSrc))
                aoe.GetComp<TransformComp>().YawDegrees = yawSrc.YawDegrees;
            CopyTeam(ctx.Source, aoe);

            var body = aoe.GetComp<AoeComp>();
            body.Setup(def, ctx.Source != null ? ctx.Source.Id : EntityId.Invalid, snap, ctx.World.Time.Frame);

            if (def.CueId != 0)
                ctx.World.Events.Publish(new EvCue(def.CueId, body.OwnerId, "AoeSpawn"));

            if (def.PulseOnSpawn)
                AoePulse.PulseNow(ctx.World, aoe, body);
        }

        public static void CopyTeam(Actor owner, Actor spawned)
        {
            if (owner == null || !spawned.TryGetComp<TeamComp>(out var dt)) return;
            if (owner.TryGetComp<TeamComp>(out var st))
                dt.SetTeam(st.TeamId);
        }
    }

    public sealed class HealEffect : IEffect
    {
        public float Amount = 10f;
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            var attr = ctx.Target.GetComp<AttributeSet>();
            float hp = attr.GetBase(AttrId.Hp) + Amount;
            attr.SetBase(AttrId.Hp, hp);
            ctx.World?.Events.Publish(new EvHeal(ctx.Target.Id, Amount));
        }
    }

    public sealed class TeleportEffect : IEffect
    {
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null || !ctx.HasPoint) return;
            if (ctx.Target.TryGetComp<LocomotionComp>(out var loco))
                loco.RequestTeleport(ctx.Point);
        }
    }

    public sealed class DamageEffect : IEffect
    {
        public float Coeff = 1f;
        public float Flat;
        public bool IgnoreDef;
        public bool CanCrit;
        public bool UseSnapshotAtk = true;
        public bool ScaleByBuffStacks;
        public float CritMul = 2f;
        public bool FireOnHurted = true;

        public void Apply(ref EffectContext ctx)
        {
            var target = ctx.Target;
            var source = ctx.Source;
            if (target == null || ctx.World == null) return;

            var tags = target.GetComp<TagComp>();
            if (tags.Has(CommonTags.Invincible) ||
                (target.TryGetComp<HealthComp>(out var hc) && hc.InIFrame))
            {
                ctx.World.Events.Publish(new EvImmune(target.Id, source != null ? source.Id : EntityId.Invalid));
                return;
            }

            var dstAttr = target.GetComp<AttributeSet>();
            float atk;
            if (UseSnapshotAtk) atk = ctx.SnapshotAtk;
            else if (source != null && source.TryGetComp<AttributeSet>(out var srcLive))
                atk = srcLive.GetFinal(AttrId.Atk);
            else atk = ctx.SnapshotAtk;

            float def = IgnoreDef ? 0f : dstAttr.GetFinal(AttrId.Def);
            float raw = atk * Coeff + Flat - def;
            if (raw < 0f) raw = 0f;
            if (ScaleByBuffStacks)
                raw *= Math.Max(1, ctx.BuffStacks);

            bool crit = false;
            if (CanCrit && source != null && source.TryGetComp<AttributeSet>(out var srcAttr))
            {
                float rate = srcAttr.GetFinal(AttrId.CritRate);
                if (ctx.World.Random.Next01() < rate)
                {
                    crit = true;
                    raw *= CritMul > 0f ? CritMul : 2f;
                }
            }

            float dealMul = 1f;
            float takenMul = dstAttr.GetFinal(AttrId.DmgTakenMul);
            if (source != null && source.TryGetComp<AttributeSet>(out var srcM))
                dealMul = srcM.GetFinal(AttrId.DmgDealMul);
            raw *= dealMul * takenMul;
            if (raw < 0f) raw = 0f;

            float shield = dstAttr.GetBase(AttrId.Shield);
            float absorb = 0f;
            if (shield > 0f)
            {
                absorb = raw < shield ? raw : shield;
                dstAttr.SetBase(AttrId.Shield, shield - absorb);
                raw -= absorb;
            }

            float hp = dstAttr.GetBase(AttrId.Hp);
            hp -= raw;
            if (hp < 0f) hp = 0f;
            dstAttr.SetBase(AttrId.Hp, hp);

            bool kill = hp <= 0f;
            ctx.World.Events.Publish(new EvDamage(
                source != null ? source.Id : EntityId.Invalid,
                target.Id, raw + absorb, crit, absorb, kill));

            if (kill && target.TryGetComp<StateMachineComp>(out var fsm))
            {
                fsm.TryEnter(ActivityId.Dead, new ActivityEnterArgs
                {
                    Reason = "Kill",
                    Killer = source != null ? source.Id : EntityId.Invalid
                });
            }

            if (FireOnHurted && target.TryGetComp<BuffComp>(out var buffs))
                buffs.DispatchOnHurted(source);
        }
    }

    public sealed class DamageAttackerEffect : IEffect
    {
        public DamageEffect Inner = new DamageEffect { FireOnHurted = false, CanCrit = false, UseSnapshotAtk = true };

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Source == null) return;
            var inner = ctx;
            inner.Target = ctx.Source;
            Inner.Apply(ref inner);
        }
    }

    public sealed class HitStunEffect : IEffect
    {
        public float Duration = 0.35f;
        public float IFrameDuration;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            if (ctx.Target.TryGetComp<TagComp>(out var tags) &&
                (tags.Has(CommonTags.SuperArmor) || tags.Has(CommonTags.Dead)))
                return;
            ctx.Target.GetComp<StateMachineComp>().TryEnter(ActivityId.Hit, new ActivityEnterArgs
            {
                Reason = "HitStun",
                HitDuration = Duration > 0f ? Duration : 0.35f,
                IFrameDuration = IFrameDuration
            });
        }
    }

    public sealed class KnockbackEffect : IEffect
    {
        public float Distance = 0.4f;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null || Distance == 0f) return;
            if (!ctx.Target.TryGetComp<LocomotionComp>(out var loco)) return;
            if (ctx.Target.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead)) return;

            SimVec3 dir;
            if (ctx.HasDir) dir = ctx.Dir;
            else if (ctx.Source != null && ctx.Source.TryGetComp<TransformComp>(out var stf)
                     && ctx.Target.TryGetComp<TransformComp>(out var ttf))
                dir = new SimVec3(ttf.Position.X - stf.Position.X, 0f, ttf.Position.Z - stf.Position.Z);
            else dir = new SimVec3(1f, 0f, 0f);

            float mag = LocomotionComp.StickMag(dir);
            if (mag < 1e-5f) dir = new SimVec3(1f, 0f, 0f);
            else
            {
                float inv = 1f / mag;
                dir = new SimVec3(dir.X * inv, 0f, dir.Z * inv);
            }

            loco.RequestHitDelta(dir.X * Distance, 0f, dir.Z * Distance);
        }
    }

    public sealed class IFrameEffect : IEffect
    {
        public float Duration = 0.1f;
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            if (ctx.Target.TryGetComp<HealthComp>(out var hp))
                hp.BeginIFrame(Duration);
        }
    }
}
