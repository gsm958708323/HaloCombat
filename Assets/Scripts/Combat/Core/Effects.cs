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
            ctx.Target.GetComp<BuffComp>().Apply(_spec, ctx.Source, _stacks,ctx.SnapshotAtk,ctx.Power,ctx.Origin);
        }
    }

    public sealed class DispelEffect : IEffect
    {
        readonly DispelMode _mode;
        readonly int _key;
        readonly TagId _tag;
        readonly int _maxCount;
        readonly EntityId _source;
        public DispelEffect(DispelMode mode, int key = 0, TagId tag = default, int maxCount = 0,EntityId source=default)
        {
            _mode = mode;
            _key = key;
            _tag = tag;
            _maxCount = maxCount;
            _source=source;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            var source=_source.IsValid?_source:ctx.Source?.Id??EntityId.Invalid;
            ctx.Target.GetComp<BuffComp>().Dispel(_mode, _key, _tag, _maxCount,source);
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
        public readonly EntityId Target;
        public readonly float Power;
        public readonly ProjectileSeed Seed;
        public SpawnProjectileIntent(EntityId owner, int specId, SimVec3 origin, float yaw, float snapshotAtk, EntityId target = default, float power = 1,ProjectileSeed seed=null)
        {
            Owner = owner;
            SpecId = specId;
            Origin = origin;
            Yaw = yaw;
            SnapshotAtk = snapshotAtk;
            Target = target;
            Power = power;
            Seed=seed;
        }
    }

    public sealed class SpawnProjectileEffect : IEffect
    {
        readonly int _specId;
        readonly int _count;
        readonly float _spread;
        public SpawnProjectileEffect(int specId,int count=1,float spread=0) {_specId=specId;_count=count;_spread=spread;}

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Source == null) return;
            var tf = ctx.Source.GetComp<TransformComp>();
            float atk = ctx.SnapshotAtk;
            if (atk == 0f && ctx.Source.TryGetComp<AttributeSet>(out var attr))
                atk = attr.GetFinal(AttrId.Atk);
            var origin=ctx.Origin;
            if(origin.CastId==0&&ctx.Source.TryGetComp<SkillDirectorComp>(out var director)&&director.IsPlaying)origin=director.CurrentOrigin;
            var family=ctx.ProjectileHit?.Family??new ProjectileFamily(origin);
            float yaw=ctx.HasDir?LocomotionComp.YawFromStick(ctx.Dir):tf.YawDegrees;
            for(int i=0;i<_count;i++)
                ProjectileEmission.Post(ctx.World,ctx.Source.Id,_specId,tf.Position,yaw+ProjectileEmission.Offset(i,_count,_spread),atk,
                    ctx.Target!=null?ctx.Target.Id:EntityId.Invalid,ctx.Power>0?ctx.Power:1,
                    new ProjectileSeed(family,ctx.ProjectileHit!=null?ctx.ProjectileHit.Generation+1:0,0,false,Array.Empty<EntityId>()));
        }
    }

    public sealed class SpawnAoeEffect : IEffect
    {
        readonly int _specId;
        readonly EffectCenter _center;
        readonly float _forwardOffset;
        public SpawnAoeEffect(int specId, EffectCenter center = EffectCenter.Auto,float forwardOffset=0) { _specId = specId; _center = center;_forwardOffset=forwardOffset; }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null) return;
            if (!ctx.World.Aoes.TryGet(_specId, out var def))
                throw new InvalidOperationException("Unknown AoE " + _specId);

            if (!EffectPosition.TryResolve(ctx, _center, out var origin)) return;

            float snap = ctx.SnapshotAtk;
            if (snap == 0f && ctx.Source != null && ctx.Source.TryGetComp<AttributeSet>(out var attr))
                snap = attr.GetFinal(AttrId.Atk);

            var id = ctx.World.SpawnActor(new ActorSpawnSpec("aoe"), publishSpawn: false);
            if (!ctx.World.TryGetActor(id, out var aoe) || aoe == null) return;

            float yaw=ctx.HasDir?LocomotionComp.YawFromStick(ctx.Dir):ctx.Source!=null&&ctx.Source.TryGetComp<TransformComp>(out var sourceTransform)?sourceTransform.YawDegrees:0;
            var direction=LocomotionComp.ForwardFromYaw(yaw);
            aoe.GetComp<TransformComp>().Position = new SimVec3(origin.X+direction.X*_forwardOffset,origin.Y,origin.Z+direction.Z*_forwardOffset);
            aoe.GetComp<TransformComp>().YawDegrees=yaw;
            CopyTeam(ctx.Source, aoe);

            var body = aoe.GetComp<AoeComp>();
            body.Setup(def, ctx.Source != null ? ctx.Source.Id : EntityId.Invalid, snap, ctx.World.Time.Frame, ctx.Power > 0 ? ctx.Power : 1,ctx.Origin);

            if (def.CueId != 0)
                ctx.World.Events.Publish(new EvCue(def.CueId, body.OwnerId, "AoeSpawn"));

            ctx.World.PublishSpawn(id, "aoe");
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
            if (ctx.Target == null||!ctx.Target.IsActive||ctx.Target.GetComp<TagComp>().Has(CommonTags.Dead)) return;
            var attr = ctx.Target.GetComp<AttributeSet>();
            float old=attr.GetBase(AttrId.Hp);if(old<=0)return;
            float hp = old + Math.Max(0,Amount);
            attr.SetBase(AttrId.Hp, hp);
            ctx.World?.Events.Publish(new EvHeal(ctx.Target.Id, attr.GetBase(AttrId.Hp)-old));
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

    public sealed class HitStunEffect : IEffect
    {
        public float Duration = 0.35f;
        public float IFrameDuration;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            if (ctx.Target.TryGetComp<TagComp>(out var tags) &&
                (tags.Has(CommonTags.SuperArmor) || tags.Has(CommonTags.Dead) || tags.Has(CommonTags.Downed)))
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

    public sealed class KnockdownEffect : IEffect
    {
        public float Duration = 0.80f;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null) return;
            if (ctx.Target.TryGetComp<TagComp>(out var tags) &&
                (tags.Has(CommonTags.Dead) || tags.Has(CommonTags.SuperArmor)))
                return;
            ctx.Target.GetComp<StateMachineComp>().TryEnter(
                ActivityId.Knockdown,
                new ActivityEnterArgs
                {
                    Reason = "Knockdown",
                    HitDuration = Duration > 0f ? Duration : 0.80f
                });
        }
    }
}
