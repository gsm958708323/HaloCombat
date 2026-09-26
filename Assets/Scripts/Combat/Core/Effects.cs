using System;

namespace Combat.Core
{
    /// <summary>给目标叠加标签层数。契约：stacks 小于 1 时按 1 处理，调用方传 0 不代表「移除」。</summary>
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

    /// <summary>移除目标标签层数，与 AddTagEffect 对称；stacks 小于 1 时同样按 1 处理。</summary>
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

    /// <summary>给目标挂一条带时长的 Buff；层数与刷新规则由 BuffComp.Apply 决定，本效果只负责把来源一起传下去。</summary>
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

    /// <summary>按模式驱散目标 Buff。DispelMode.BySource 且未显式给 key 时，把 Source 实体打包成 key，所以来源为空的驱散只能走其他模式。</summary>
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
            int key = _key;
            if (_mode == DispelMode.BySource && key == 0)
                key = BuffComp.Pack(ctx.Source);
            ctx.Target.GetComp<BuffComp>().Dispel(_mode, key, _tag, _maxCount);
        }
    }

    /// <summary>只广播一条表现事件 EvCue，不修改任何战斗数值；World 或 Events 缺失时静默跳过（无表现环境可复用）。</summary>
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

    /// <summary>发射意图（值类型）：效果层只投递它，实体由 ProjectileService 稍后统一生成，因此这里携带的必须是生成后不再回查的只读快照。</summary>
    public readonly struct SpawnProjectileIntent
    {
        public readonly EntityId Owner;
        public readonly int SpecId;
        public readonly SimVec3 Origin;
        public readonly float Yaw;
        public readonly float SnapshotAtk;
        public readonly EntityId Target;
        public SpawnProjectileIntent(EntityId owner, int specId, SimVec3 origin, float yaw, float snapshotAtk, EntityId target = default)
        {
            Owner = owner;
            SpecId = specId;
            Origin = origin;
            Yaw = yaw;
            SnapshotAtk = snapshotAtk;
            Target = target;
        }
    }

    /// <summary>
    /// 投意图而不是当场生成：只把 SpawnProjectileIntent 投进 Intents，由 ProjectileService.DrainSpawns 稍后生成子弹实体。
    /// 正因如此，快照 Atk 必须在投递当帧定死；ctx.SnapshotAtk 为 0 时才回落到施法者当前 Atk。
    /// Target 只是初始追踪目标，可为 Invalid。
    /// </summary>
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
                ctx.Source.Id, _specId, tf.Position, tf.YawDegrees, atk,
                ctx.Target != null ? ctx.Target.Id : EntityId.Invalid));
        }
    }

    /// <summary>
    /// 与子弹相反：AoE 是当场生成实体（Apply 内立即 SpawnActor + Setup），所以脉冲与寿命从这一帧开始计时。
    /// 原点选择顺序：useTargetPoint 取目标位置 → ctx.Point → 施法者位置 → 原点；forwardOffset 再沿施法者朝向平移。
    /// </summary>
    public sealed class SpawnAoeEffect : IEffect
    {
        readonly int _specId;
        readonly bool _useTargetPoint;
        readonly float _radiusOverride;
        readonly float _durationOverride;
        readonly float _forwardOffset;

        public SpawnAoeEffect(int specId, bool useTargetPoint = false, float radiusOverride = 0f,
            float durationOverride = 0f, float forwardOffset = 0f)
        {
            _specId = specId;
            _useTargetPoint = useTargetPoint;
            _radiusOverride = radiusOverride;
            _durationOverride = durationOverride;
            _forwardOffset = forwardOffset;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null) return;
            if (!ctx.World.Aoes.TryGet(_specId, out var def))
                throw new InvalidOperationException("Unknown AoE " + _specId);

            SimVec3 origin;
            if (_useTargetPoint && ctx.Target != null && ctx.Target.TryGetComp<TransformComp>(out var targetTf))
                origin = targetTf.Position;
            else if (ctx.HasPoint) origin = ctx.Point;
            else if (ctx.Source != null && ctx.Source.TryGetComp<TransformComp>(out var stf))
                origin = stf.Position;
            else origin = SimVec3.Zero;

            if (_forwardOffset != 0f && ctx.Source != null &&
                ctx.Source.TryGetComp<TransformComp>(out var sourceTf))
            {
                var fwd = LocomotionComp.ForwardFromYaw(sourceTf.YawDegrees);
                origin = new SimVec3(origin.X + fwd.X * _forwardOffset, origin.Y,
                    origin.Z + fwd.Z * _forwardOffset);
            }

            float snap = ctx.SnapshotAtk;
            if (snap == 0f && ctx.Source != null && ctx.Source.TryGetComp<AttributeSet>(out var attr))
                snap = attr.GetFinal(AttrId.Atk);

            var id = ctx.World.SpawnActor(new ActorSpawnSpec("aoe"), publishSpawn: false);
            if (!ctx.World.TryGetActor(id, out var aoe) || aoe == null) return;

            aoe.GetComp<TransformComp>().Position = origin;
            if (ctx.Source != null && ctx.Source.TryGetComp<TransformComp>(out var yawSrc))
                aoe.GetComp<TransformComp>().YawDegrees = yawSrc.YawDegrees;
            CopyTeam(ctx.Source, aoe);

            var body = aoe.GetComp<AoeComp>();
            body.Setup(def, ctx.Source != null ? ctx.Source.Id : EntityId.Invalid, snap,
                ctx.World.Time.LogicFrame, _radiusOverride, _durationOverride);

            if (def.CueId != 0)
                ctx.World.Events.Publish(new EvCue(def.CueId, body.OwnerId, "AoeSpawn"));

            if (def.PulseOnSpawn)
                AoePulse.PulseNow(ctx.World, aoe, body);

            if (!string.IsNullOrEmpty(def.ViewBlueprintId))
                ctx.World.PublishSpawn(id, "aoe", def.ViewBlueprintId);
        }

        /// <summary>让运行时体继承施法者阵营，仅用于伤害归属与敌我过滤；运行时体本身不参战。</summary>
        public static void CopyTeam(Actor owner, Actor spawned)
        {
            if (owner == null || !spawned.TryGetComp<TeamComp>(out var dt)) return;
            if (owner.TryGetComp<TeamComp>(out var st))
                dt.SetTeam(st.TeamId);
        }
    }

    /// <summary>直接改 Hp 基础值并广播 EvHeal，不经过伤害管线（防御、护盾、受击过滤都不参与）。</summary>
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

    /// <summary>把目标瞬移到效果点；没有点（HasPoint 为假）时什么都不做，实际位移交由 LocomotionComp 排队，避免与移动求解抢位置。</summary>
    public sealed class TeleportEffect : IEffect
    {
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null || !ctx.HasPoint) return;
            if (ctx.Target.TryGetComp<LocomotionComp>(out var loco))
                loco.RequestTeleport(ctx.Point);
        }
    }

    /// <summary>通用伤害结算效果。字段都是配置：产品特有的减伤/限伤规则不要塞进这里，而应由目标实现 IIncomingDamage 自己决定。</summary>
    public sealed class DamageEffect : IEffect
    {
        public float Coeff = 1f;
        public float Flat;
        public bool IgnoreDef;
        public bool CanCrit;
        public bool UseSnapshotAtk = true;
        public bool ScaleByBuffStacks;
        public float CritMul = 2f;
        public float CritChance = -1f;
        public bool DirectDamage = true;
        public bool FireOnHurted = true;
        public int SourceHitstopFrames;
        public int TargetHitstopFrames;

        /// <summary>
        /// 伤害结算顺序（固定，不可重排）：
        /// 1) 无敌判定：Invincible 标签或 HealthComp.InIFrame 命中即广播 EvImmune 并返回；
        /// 2) 取攻击值：UseSnapshotAtk 用快照，否则取来源当帧最终 Atk，来源缺失时退回快照；
        /// 3) 减防御：IgnoreDef 时防御记 0，raw = atk*Coeff + Flat - def，并夹到不小于 0；
        /// 4) Buff 层数：ScaleByBuffStacks 时乘 max(1, BuffStacks)；
        /// 5) 暴击：CanCrit 且概率有效时用 World.Random 掷骰（CritChance 小于 0 回落到来源 CritRate）；
        /// 6) 攻防乘区：乘来源 DmgDealMul 与目标 DmgTakenMul，再夹到不小于 0；
        /// 7) 受击过滤 IIncomingDamage：DirectDamage 为真时让目标组件在写入血量前改写数值；
        /// 8) 护盾吸收：先扣 Shield 基础值，剩余部分才进血量；
        /// 9) 写血量：SetBase(Hp)（不走 final 属性重算），并夹到不小于 0；
        /// 10) 后置：广播 EvDamage、命中停帧、死亡状态机切换、受击 Buff 回调。
        /// 受击过滤必须放在扣血之前：它改的就是「最终进血量的数字」，一旦先扣血再过滤，
        /// 护盾/血量已被不可逆写坏，上报的 EvDamage 也会与真实扣血不一致。
        /// </summary>
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
            float critChance = CritChance;
            if (critChance < 0f && source != null && source.TryGetComp<AttributeSet>(out var srcAttr))
                critChance = srcAttr.GetFinal(AttrId.CritRate);
            if (CanCrit && critChance >= 0f)
            {
                if (ctx.World.Random.Next01() < critChance)
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

            // 受击过滤交给目标组件（例如 Arena 木桶的「最多 1 点」）：通用伤害公式不认识具体产品组件。
            if (DirectDamage && target.TryGetComp<IIncomingDamage>(out var filter))
                raw = filter.Filter(source, raw, in ctx);

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

            if (raw + absorb > 0f && (SourceHitstopFrames > 0 || TargetHitstopFrames > 0))
            {
                source?.Time.RequestHitstop(SourceHitstopFrames);
                target.Time.RequestHitstop(TargetHitstopFrames);
                ctx.World.Events.Publish(new EvHitstop(
                    source != null ? source.Id : EntityId.Invalid, target.Id,
                    SourceHitstopFrames, TargetHitstopFrames));
            }

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

    /// <summary>把伤害回敬给施法者自身。Inner 默认关闭暴击与 OnHurted，避免自伤再次触发反击形成循环。</summary>
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

    /// <summary>进入受击硬直。超甲/死亡/倒地直接吞掉；Duration 非正时回落到 0.35s，IFrameDuration 交给状态机在进入时开无敌。</summary>
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

    /// <summary>沿效果方向、否则沿施法者到目标的水平方向推一段位移；方向退化为零长度时回落到 +X，避免正常化除零。</summary>
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

    /// <summary>给目标一个向上的冲量；死亡与超甲免疫击飞，垂直速度非正时不做任何事。</summary>
    public sealed class LaunchEffect : IEffect
    {
        public float VerticalSpeed = 5f;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null || VerticalSpeed <= 0f) return;
            if (ctx.Target.TryGetComp<TagComp>(out var tags) &&
                (tags.Has(CommonTags.Dead) || tags.Has(CommonTags.SuperArmor)))
                return;
            if (ctx.Target.TryGetComp<LocomotionComp>(out var loco))
                loco.ImpulseLaunch(VerticalSpeed);
        }
    }

    /// <summary>开启一段无敌帧。与 HitStun 自带的 IFrameDuration 共用 HealthComp 同一份计时（取较长者，不叠加）。</summary>
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

    /// <summary>进入倒地状态；死亡与超甲免疫，Duration 非正时回落到 0.80s。</summary>
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
