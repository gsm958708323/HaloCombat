using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>
    /// Buff Arena 的固定 id 与归属边界。归代码的只有三类：蓝图名（player/enemy/barrel）、敌人 AI 的技能与时间轴、
    /// 以及木桶爆炸发布的两个 cue；它们必须和 BuffArenaActorFactory / WanderShooter 逐字对上。
    /// 玩家的技能、时间轴、弹道、AoE、cue 全部由 SO 授权（见下方注释的 Generated 目录），不在这里重复声明。
    /// </summary>
    public static class BuffArenaIds
    {
        // Blueprint ids are shared identity, not tuning: the asset table, the actor
        // factory and the session all have to agree on them.
        public const string PlayerBlueprint = "buff_player";
        public const string EnemyBlueprint = "buff_enemy";
        public const string BarrelBlueprint = "buff_barrel";

        // Player skills, timelines, projectiles, AoEs and cues are SO-authored; their ids
        // live in Assets/Combat/Config/Generated. Only the enemy AI tree is code-owned, so
        // its two ids stay here, plus the two cue ids the barrel explosion publishes.
        public const int SkillEnemyValue = 2010;
        public const int TimelineEnemyValue = 3010;
        public const int CueHitValue = 4204;
        public const int CueExplosionValue = 4206;

        public static readonly SkillNodeId SkillEnemy = new SkillNodeId(SkillEnemyValue);
        public static readonly TimelineId TimelineEnemy = new TimelineId(TimelineEnemyValue);

        // The player's key map is code-owned (see BuffArenaSession.ApplyInput): it has to agree
        // with the Gameplay action map, which only exists in code anyway. These tokens are the
        // other half of that contract - each must equal the InputToken authored on its skill asset.
        public static readonly InputToken Fire1 = new InputToken("Fire1");
        public static readonly InputToken Fire2 = new InputToken("Fire2");
        public static readonly InputToken Fire3 = new InputToken("Fire3");
        public static readonly InputToken Fire4 = new InputToken("Fire4");
        public static readonly InputToken Fire5 = new InputToken("Fire5");
        public static readonly InputToken RollInput = new InputToken("Roll");
        public static readonly InputToken HomingInput = new InputToken("Homing");
        public static readonly InputToken MonkeyInput = new InputToken("Monkey");
    }

    /// <summary>
    /// 弹药：Current 只能经 TryConsume/Refill/Set 变化，Capacity 是上限。
    /// TryConsume 失败时不改变任何状态，调用方可以靠返回值决定是否回退（见 BuffArenaPlayerComp.Tick 的 FallbackSkill）。
    /// </summary>
    public sealed class AmmoComp : Comp
    {
        public int Current { get; private set; }
        public int Capacity { get; private set; }

        public AmmoComp(int capacity)
        {
            Capacity = Math.Max(0, capacity);
            Current = Capacity;
        }

        public bool TryConsume(int amount)
        {
            if (amount <= 0) return true;
            if (Current < amount) return false;
            Current -= amount;
            return true;
        }

        public void Refill(int amount)
        {
            if (amount <= 0) return;
            Current = Math.Min(Capacity, Current + amount);
        }

        public void Set(int current, int capacity)
        {
            Capacity = Math.Max(0, capacity);
            Current = Math.Max(0, Math.Min(Capacity, current));
        }

        protected override void OnDetach()
        {
            Current = 0;
            Capacity = 0;
        }
    }

    /// <summary>
    /// 追踪本角色最近发射的弹道实体（传送弹靠它判断“弹还在飞”）。
    /// 弹被销毁/回收时必须 ClearIf，否则这里会一直认为有弹——EntityId.Generation 能挡住指向新实体的误用，但状态会卡住。
    /// </summary>
    public sealed class ProjectileTrackerComp : Comp
    {
        public EntityId TrackedProjectile { get; private set; } = EntityId.Invalid;
        public bool HasProjectile => TrackedProjectile.IsValid;

        public void Track(EntityId id) => TrackedProjectile = id;

        public void ClearIf(EntityId id)
        {
            if (TrackedProjectile == id)
                TrackedProjectile = EntityId.Invalid;
        }

        protected override void OnDetach() => TrackedProjectile = EntityId.Invalid;
    }

    /// <summary>
    /// 一条技能定义（SO 授权的只读共享数据，同一份会被多个角色读）。技能的运行态（冷却、位移、动画）
    /// 存在角色/实体上，这里只放描述性字段。两条回退链：付不出 AmmoCost 时改用 FallbackSkill 及其时间轴；
    /// RequiresTrackedProjectile 且已有弹在飞时改用 WarpSkillId。
    /// </summary>
    public sealed class BuffArenaSkill
    {
        public SkillNodeId Id;
        public TimelineId Timeline;
        public InputToken Input;
        public int AmmoCost;
        public string AnimatorState;
        // Teleport bullet: while a tracked projectile exists the skill resolves to
        // WarpSkillId instead of casting itself again.
        public bool RequiresTrackedProjectile;
        public SkillNodeId WarpSkillId = SkillNodeId.None;
        // Played instead when this skill cannot pay its AmmoCost. SO-authored, and the
        // fallback's own timeline is used, so no second id pair is hard-coded here.
        public SkillNodeId FallbackSkill = SkillNodeId.None;
    }

    /// <summary>
    /// 一场 Buff Arena 的共享配置与目录（时间轴/弹道/AoE/cue/角色定义/技能表）。
    /// 实例由会话创建后只读共享：可变状态属于角色与实体，不要写回这里，否则重开局或对象复用会串场。
    /// </summary>
    public sealed class BuffArenaData
    {
        public TimelineLibrary Timelines = new TimelineLibrary();
        public ProjectileCatalog Projectiles = new ProjectileCatalog();
        public AoeCatalog Aoes = new AoeCatalog();
        public CueLibrary Cues = new CueLibrary();
        public MotorConfig Motor = MotorConfig.SeasonOneDefaults();
        public readonly List<BuffArenaSkill> Skills = new List<BuffArenaSkill>(9);
        public int PlayerAmmoCapacity = 60;
        public int MaxEnemies = 10;
        public float SpawnPeriod = 10f;
        public float EnemyCleanupDelay = 5f;
        public float BarrelSelfDamagePeriod = 5f;
        public int Seed = 1;
        public readonly List<BuffArenaActorDef> Actors = new List<BuffArenaActorDef>(4);

        public BuffArenaActorDef RequireActor(string blueprintId)
        {
            for (int i = 0; i < Actors.Count; i++)
                if (string.Equals(Actors[i].BlueprintId, blueprintId, StringComparison.Ordinal))
                    return Actors[i];
            throw new InvalidOperationException("Missing Buff Arena actor definition " + blueprintId);
        }

        public bool TryGetSkill(SkillNodeId id, out BuffArenaSkill skill)
        {
            for (int i = 0; i < Skills.Count; i++)
            {
                if (Skills[i].Id != id) continue;
                skill = Skills[i];
                return true;
            }
            skill = null;
            return false;
        }
    }

    /// <summary>
    /// 玩家“输入 → 技能”的执行器：读 InputBuffer，经 FindByInput 把 token 映射到技能，再交给 SkillDirector 播放。
    /// 契约：token 必须与技能资产上的 InputToken 一致，否则该次输入会被静默丢弃（不报错）。
    /// </summary>
    public sealed class BuffArenaPlayerComp : Comp
    {
        readonly BuffArenaData _data;
        InputBufferComp _input;
        SkillDirectorComp _director;
        AmmoComp _ammo;
        ProjectileTrackerComp _tracker;
        LocomotionComp _loco;

        public override bool WantsTick => true;

        public BuffArenaPlayerComp(BuffArenaData data) => _data = data ?? throw new ArgumentNullException(nameof(data));

        protected override void OnAttach()
        {
            _input = Self.GetComp<InputBufferComp>();
            _director = Self.GetComp<SkillDirectorComp>();
            Self.TryGetComp(out _ammo);
            Self.TryGetComp(out _tracker);
            Self.TryGetComp(out _loco);
        }

        protected override void OnDetach()
        {
            _input = null;
            _director = null;
            _ammo = null;
            _tracker = null;
            _loco = null;
        }

        /// <summary>
        /// 每帧判定顺序（顺序即语义，不要重排）：
        /// 1) 缺组件或导演不允许施法 → 保留队列并返回；
        /// 2) 时间轴正在播 → 保留缓冲，不引入取消当前技能的机制；
        /// 3) 消费一个排队 token；队列为空才使用 Held 普攻，本步最多尝试一次；
        /// 4) FindByInput 匹配不到技能 → 本次输入作废；
        /// 5) 传送弹特例：需要追踪弹且弹还在飞时改为传送到弹的位置，成功即结束；
        /// 6) 弹药不足：付不出 AmmoCost 就播 FallbackSkill（用回退自己的时间轴）后结束；
        /// 7) Play 失败且本来要耗弹时把弹药退回去，避免“技能没播成却扣了弹”。
        /// </summary>
        public override void Tick(float dt)
        {
            if (_input == null || _director == null || _loco == null ||
                !_director.CanStartSkill || _director.IsPlaying) return;
            InputToken token;
            if (_input.TryPeek(out token)) _input.Consume();
            else if (_input.PrimaryHeld) token = BuffArenaIds.Fire1;
            else return;

            BuffArenaSkill skill = FindByInput(token);
            if (skill == null) return;

            // Data-driven teleport bullet: the skill warps instead of firing while one of
            // its tracked projectiles is still airborne.
            if (skill.RequiresTrackedProjectile && _tracker != null && _tracker.HasProjectile &&
                skill.WarpSkillId.IsValid)
            {
                if (TryTeleport()) return;
            }

            if (skill.AmmoCost > 0 && (_ammo == null || !_ammo.TryConsume(skill.AmmoCost)))
            {
                // The fallback is SO data (SK_2001 -> Reload). Play it on its own timeline,
                // so no second skill/timeline id pair has to be hard-coded here.
                if (skill.FallbackSkill.IsValid && _data.TryGetSkill(skill.FallbackSkill, out var fallback))
                    _director.Play(fallback.Id, fallback.Timeline);
                return;
            }

            if (!_director.Play(skill.Id, skill.Timeline) && skill.AmmoCost > 0)
                _ammo.Refill(skill.AmmoCost);
        }

        /// <summary>
        /// 传送到追踪弹的位置。弹实体已不存在时清掉追踪并返回 false，让调用方继续走普通施放；
        /// 落点用地面规则 CanPlace 校验（flying=false），放不下就发一条 Teleport blocked 消息并返回 true——
        /// 即“这次施放算消耗掉了，不要再放技能”，避免在墙里反复尝试。成功后弹道立即 despawn 并停用，防止重复传送。
        /// </summary>
        bool TryTeleport()
        {
            if (_tracker == null || !_tracker.HasProjectile || Self.World == null)
                return false;
            if (!Self.World.TryGetActor(_tracker.TrackedProjectile, out var projectile) || projectile == null ||
                !projectile.TryGetComp<TransformComp>(out var projectileTf))
            {
                _tracker.ClearIf(_tracker.TrackedProjectile);
                return false;
            }

            float radius = Self.TryGetComp<CharacterRadiusComp>(out var body) ? body.Radius : .25f;
            if (!Self.World.Movement.CanPlace(projectileTf.Position, radius, false))
            {
                Self.World.Events.Publish(new EvGameplayMessage(Self.Id, "Teleport blocked"));
                return true;
            }

            _loco.RequestTeleport(projectileTf.Position);
            Self.World.RequestDespawn(projectile.Id);
            projectile.SetActive(false);
            _tracker.ClearIf(projectile.Id);
            return true;
        }

        /// <summary>
        /// 按 token 线性扫描技能表；技能表是个位数规模，不值得建索引。找不到返回 null，由调用方决定怎么处理已消耗的输入。
        /// </summary>
        BuffArenaSkill FindByInput(InputToken token)
        {
            for (int i = 0; i < _data.Skills.Count; i++)
                if (_data.Skills[i].Input == token) return _data.Skills[i];
            return null;
        }
    }

    /// <summary>
    /// 敌人 AI 叶子：随机游走 + 定时施放。它直接调 SkillDirector.Play，不经过 InputBuffer、不查 InputToken、也不扣弹药，
    /// 所以给 AI 用的技能不需要（也不应该）配 InputToken/AmmoCost。随机数一律走 World.Random，保证逻辑层可重放。
    /// </summary>
    public sealed class WanderShooter : BtNode
    {
        readonly SkillNodeId _skill;
        readonly TimelineId _timeline;
        float _wanderTimer = 2f;
        float _fireTimer = 3f;
        float _wanderYaw;
        bool _initialized;

        public WanderShooter(SkillNodeId skill, TimelineId timeline)
        {
            _skill = skill;
            _timeline = timeline;
        }

        public override BtStatus Tick(in BtTick ctx)
        {
            if (ctx.World == null || !ctx.Self.TryGetComp<LocomotionComp>(out var loco) ||
                !ctx.Self.TryGetComp<SkillDirectorComp>(out var director))
                return BtStatus.Failure;
            if (ctx.Self.TryGetComp<TagComp>(out var tags) &&
                (tags.Has(CommonTags.Dead) || tags.Has(CommonTags.Stunned) || tags.Has(CommonTags.Downed)))
            {
                loco.RequestMoveIntent(0f, 0f);
                return BtStatus.Running;
            }

            if (!_initialized && ctx.Self.TryGetComp<TransformComp>(out var initialTf))
            {
                _wanderYaw = initialTf.YawDegrees;
                _initialized = true;
            }

            _wanderTimer -= ctx.Dt;
            if (_wanderTimer <= 0f)
            {
                // The yaw convention is mirrored relative to the old +X axis, so the
                // signed wander offset flips with it and the same random draw keeps
                // producing the same world heading.
                _wanderYaw -= Range(ctx.World, -90f, 90f);
                _wanderTimer = Range(ctx.World, 1.6f, 3.2f);
            }

            var move = LocomotionComp.ForwardFromYaw(_wanderYaw);
            loco.RequestMoveIntent(move.X, move.Z);

            if (CondHasTarget.IsTargetValid(ctx) && ctx.World.TryGetActor(ctx.Board.Target, out var target) && target != null &&
                target.TryGetComp<TransformComp>(out var targetTf))
            {
                var tf = ctx.Self.GetComp<TransformComp>();
                loco.RequestAimYaw(YawTo(tf.Position, targetTf.Position));
            }

            _fireTimer -= ctx.Dt;
            if (_fireTimer <= 0f && !director.IsPlaying)
            {
                director.Play(_skill, _timeline);
                _fireTimer = Range(ctx.World, 2f, 5f);
            }
            return BtStatus.Running;
        }

        public override BtNode Clone() => new WanderShooter(_skill, _timeline);

        static float Range(CombatWorld world, float min, float max)
            => min + (max - min) * world.Random.Next01();

        static float YawTo(SimVec3 from, SimVec3 to)
            => LocomotionComp.YawFromStick(new SimVec3(to.X - from.X, 0f, to.Z - from.Z));
    }

    /// <summary>
    /// Arena 炸药桶：每 BarrelSelfDamagePeriod 秒自伤 1 点，死亡时按所有者的攻击力爆炸。
    /// 它同时实现 IIncomingDamage，让「普通伤害打桶最多 1 点、桶互爆放行」这条规则留在桶自己身上，
    /// 而不是写进通用伤害公式。
    /// </summary>
    public sealed class BarrelComp : Comp, IIncomingDamage
    {
        EntityId _owner = EntityId.Invalid;
        readonly float _selfDamagePeriod;
        float _damageTimer = 5f;
        bool _exploded;
        public override bool WantsTick => true;

        public BarrelComp(float selfDamagePeriod = 5f)
        {
            _selfDamagePeriod = selfDamagePeriod > 0f ? selfDamagePeriod : 5f;
            _damageTimer = _selfDamagePeriod;
        }

        public void SetOwner(EntityId owner) => _owner = owner;

        /// <summary>
        /// 受击过滤：来源本身也是木桶时原样通过（爆炸效果用 Flat=9999 连锁引爆就靠这条），
        /// 其余来源一律封顶 1 点——所以普通射击打不爆木桶，只能靠周期自伤或连锁爆炸。
        /// </summary>
        public float Filter(Actor source, float amount, in EffectContext ctx)
        {
            if (source != null && source.TryGetComp<BarrelComp>(out _))
                return amount;
            return Math.Min(1f, amount);
        }

        /// <summary>
        /// 周期自伤：计时到点后用 += period 而不是 = period，保留溢出的小数部分，掉帧时不会把周期越拉越长。
        /// 已爆、已死或无世界时不再推进（死亡到 despawn 之间仍可能被 Tick）。
        /// </summary>
        public override void Tick(float dt)
        {
            if (_exploded || Self.World == null) return;
            if (Self.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead)) return;
            _damageTimer -= dt;
            if (_damageTimer > 0f) return;
            _damageTimer += _selfDamagePeriod;
            Self.World.Deliver(new IEffect[]
            {
                new DamageEffect { Flat = 1f, CanCrit = false, FireOnHurted = false, DirectDamage = true }
            }, Self, Self, 0f);
        }

        /// <summary>
        /// 死亡即爆炸：伤害系数取拥有者当前的 Atk（没有拥有者则为 0），爆炸后立刻 despawn 并停用。
        /// _exploded 保证只爆一次——自伤致死与敌人击杀同帧到达时也不会双爆。
        /// </summary>
        public override void OnDeath(Actor killer)
        {
            if (_exploded || Self.World == null) return;
            _exploded = true;
            Self.World.TryGetActor(_owner, out var owner);
            Self.World.Deliver(new IEffect[] { new BarrelExplosionEffect() }, owner ?? Self, Self,
                owner != null && owner.TryGetComp<AttributeSet>(out var attr) ? attr.GetFinal(AttrId.Atk) : 0f,
                Self.GetComp<TransformComp>().Position);
            Self.World.RequestDespawn(Self.Id);
            Self.SetActive(false);
        }

        protected override void OnDetach()
        {
            _owner = EntityId.Invalid;
            _damageTimer = _selfDamagePeriod;
            _exploded = false;
        }
    }

    /// <summary>
    /// 专用 Effect：把技能/资产里的 cue 参数翻译成 EvCue 事件交给表现层，本身不产生任何战斗效果。
    /// atCuePoint 只有在上下文真的带点时才成立（_atCuePoint &amp;&amp; ctx.HasPoint）；targetIsVictim 决定 victim 字段取目标还是留空。
    /// </summary>
    public sealed class PlayBuffArenaCueEffect : IEffect
    {
        // Every field mirrors the matching public field on PlayBuffArenaCueAsset, which
        // BakeNew() copies explicitly.
        readonly int _cueId;
        readonly string _anchorKey;
        readonly string _instanceKey;
        readonly bool _targetIsVictim;
        readonly bool _atCuePoint;
        readonly bool _loop;
        readonly bool _stop;

        public PlayBuffArenaCueEffect(int cueId, string anchor = "", string key = "", bool target = false,
            bool point = false, bool loop = false, bool stop = false)
        {
            _cueId = cueId;
            _anchorKey = anchor ?? string.Empty;
            _instanceKey = key ?? string.Empty;
            _targetIsVictim = target;
            _atCuePoint = point;
            _loop = loop;
            _stop = stop;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null) return;
            var source = ctx.Source != null ? ctx.Source.Id : EntityId.Invalid;
            var target = _targetIsVictim && ctx.Target != null ? ctx.Target.Id : EntityId.Invalid;
            ctx.World.Events.Publish(new EvCue(_cueId, source, "BuffArena", target, ctx.Point,
                _atCuePoint && ctx.HasPoint, _anchorKey, _instanceKey, _loop, _stop));
        }
    }

    /// <summary>
    /// 受伤反馈：只在结算后目标仍活着（GetBase(Hp) &gt; 0）时才发 EvHurt，
    /// 因此致命一击不会额外播一次“受伤”表现，死亡表现由死亡流程负责。
    /// </summary>
    public sealed class HurtFeedbackEffect : IEffect
    {
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Target == null) return;
            if (ctx.Target.TryGetComp<AttributeSet>(out var attr) && attr.GetBase(AttrId.Hp) > 0f)
                ctx.World.Events.Publish(new EvHurt(ctx.Target.Id));
        }
    }

    /// <summary>
    /// 补弹：没指定 target 时补给自己（Source），方便“施放者就是受益者”的技能只写一个字段。
    /// </summary>
    public sealed class RefillAmmoEffect : IEffect
    {
        readonly int _amount;
        public RefillAmmoEffect(int amount) => _amount = amount;
        public void Apply(ref EffectContext ctx)
        {
            var target = ctx.Target ?? ctx.Source;
            if (target != null && target.TryGetComp<AmmoComp>(out var ammo))
                ammo.Refill(_amount);
        }
    }

    /// <summary>
    /// 木桶爆炸：对半径内的目标分两类处理——桶用 Flat=9999 连锁引爆（正是 BarrelComp.Filter 对“木桶来源放行”的那条规则），
    /// 指定阵营的活体结算一次伤害并附带命中 cue 与受伤反馈。被炸的桶自身（ctx.Target）会被排除，避免自炸；
    /// 爆炸点优先取 ctx.Point，没有点才退到目标位置。
    /// </summary>
    public sealed class BarrelExplosionEffect : IEffect
    {
        public float Radius = 2.2f;
        public float DamageCoeff = .15f;
        // The source content stores team ids as raw numbers; keep it configurable so the
        // effect does not hard-code "the enemy team".
        public int EnemyTeamId = 2;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null) return;
            SimVec3 point = ctx.HasPoint ? ctx.Point :
                (ctx.Target != null ? ctx.Target.GetComp<TransformComp>().Position : SimVec3.Zero);
            var source = ctx.Source;
            ctx.World.Events.Publish(new EvCue(BuffArenaIds.CueExplosionValue,
                source != null ? source.Id : EntityId.Invalid, "BuffArena", EntityId.Invalid, point, true,
                string.Empty, string.Empty, false, false));

            var actors = ctx.World.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var target = actors[i];
                if (target == null || target == ctx.Target || !target.TryGetComp<TransformComp>(out var tf) ||
                    !target.TryGetComp<HealthComp>(out _)) continue;
                float dx = tf.Position.X - point.X;
                float dz = tf.Position.Z - point.Z;
                if (dx * dx + dz * dz > Radius * Radius) continue;

                if (target.TryGetComp<BarrelComp>(out _))
                {
                    ctx.World.Deliver(new IEffect[]
                    {
                        new DamageEffect { Flat = 9999f, CanCrit = false, FireOnHurted = false, DirectDamage = true }
                    }, ctx.Target, target, 0f, point);
                }
                else if (target.TryGetComp<TeamComp>(out var team) && team.TeamId == EnemyTeamId)
                {
                    ctx.World.Deliver(new IEffect[]
                    {
                        new DamageEffect { Coeff = DamageCoeff, TargetHitstopFrames = 3, CanCrit = true, CritMul = 1.8f,
                            CritChance = .05f, FireOnHurted = false, DirectDamage = true },
                        new HurtFeedbackEffect(),
                        new PlayBuffArenaCueEffect(BuffArenaIds.CueHitValue, "Body", target: true)
                    }, source, target, ctx.SnapshotAtk, point);
                }
            }
        }
    }

    /// <summary>
    /// 把目标沿地面拉向 ctx.Point：力度随距离饱和（近距离更轻、远距离趋近 Strength），并乘本帧 Delta 以与帧率无关；
    /// 位移走 RequestHitDelta，不会被普通移动输入覆盖，因此是“强制位移”而不是“改朝向”。
    /// </summary>
    public sealed class PullToPointEffect : IEffect
    {
        public float Strength = 1f;

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.Target == null || !ctx.HasPoint || !ctx.Target.TryGetComp<TransformComp>(out var tf) ||
                !ctx.Target.TryGetComp<LocomotionComp>(out var loco)) return;
            float dx = ctx.Point.X - tf.Position.X;
            float dz = ctx.Point.Z - tf.Position.Z;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < .0001f) return;
            float amount = Math.Min(1f, len / (len + 1f)) * Strength *
                (ctx.World != null ? ctx.World.Time.Delta : .02f);
            loco.RequestHitDelta(dx / len * amount, 0f, dz / len * amount);
        }
    }

    /// <summary>
    /// 生成木桶：放在施放者正前方、以施放者为拥有者，并把属性初始化为 0 攻 0 速（血上限由 MaxHp 覆写）。
    /// 蓝图与视图 id 可被烘焙资产覆写；spawn 事件刻意延后到位置/属性都设好再 Publish，避免表现层先看到默认值。
    /// </summary>
    public sealed class SpawnBuffArenaBarrelEffect : IEffect
    {
        public float ForwardOffset = .55f;
        public float MaxHp = 5f;
        // Identity is configuration here too: the effect spawns a content blueprint and
        // publishes its view, both of which the baked asset can override.
        public string BlueprintId = BuffArenaIds.BarrelBlueprint;
        public string ViewBlueprintId = "buff_barrel_view";

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Source == null || !ctx.Source.TryGetComp<TransformComp>(out var sourceTf)) return;
            var id = ctx.World.SpawnActor(new ActorSpawnSpec(BlueprintId), publishSpawn: false);
            if (!ctx.World.TryGetActor(id, out var barrel) || barrel == null) return;
            var fwd = LocomotionComp.ForwardFromYaw(sourceTf.YawDegrees);
            barrel.GetComp<TransformComp>().Position = new SimVec3(
                sourceTf.Position.X + fwd.X * ForwardOffset, sourceTf.Position.Y,
                sourceTf.Position.Z + fwd.Z * ForwardOffset);
            barrel.GetComp<TransformComp>().YawDegrees = sourceTf.YawDegrees;
            var attr = barrel.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, MaxHp);
            attr.SetBase(AttrId.Hp, MaxHp);
            attr.SetBase(AttrId.Atk, 0f);
            attr.SetBase(AttrId.MoveSpeed, 0f);
            barrel.GetComp<BarrelComp>().SetOwner(ctx.Source.Id);
            ctx.World.PublishSpawn(id, BlueprintId, ViewBlueprintId);
        }
    }

    /// <summary>
    /// 按蓝图名装配 Actor：projectile/aoe 只挂位置与阵营，是纯运行时实体；player/enemy/barrel 走角色装配。
    /// 敌人额外挂 WanderShooter 行为树（技能/时间轴 id 见 BuffArenaIds）。工厂会被复用，Release 只做重置。
    /// </summary>
    public sealed class BuffArenaActorFactory : IActorFactory
    {
        readonly BuffArenaData _data;

        public BuffArenaActorFactory(BuffArenaData data) => _data = data ?? throw new ArgumentNullException(nameof(data));

        public Actor Create(in ActorSpawnSpec spec)
        {
            switch (spec.BlueprintId)
            {
                case "projectile":
                    return RuntimeActor(new ProjectileComp(), 0);
                case "aoe":
                    return RuntimeActor(new AoeComp(), 0);
                case BuffArenaIds.PlayerBlueprint:
                    return Combatant(_data.RequireActor(BuffArenaIds.PlayerBlueprint), true);
                case BuffArenaIds.EnemyBlueprint:
                    return Combatant(_data.RequireActor(BuffArenaIds.EnemyBlueprint), false);
                case BuffArenaIds.BarrelBlueprint:
                    var barrel = Combatant(_data.RequireActor(BuffArenaIds.BarrelBlueprint), false);
                    barrel.AddComp(new BarrelComp(_data.BarrelSelfDamagePeriod));
                    return barrel;
                default:
                    return RuntimeActor(null, 0);
            }
        }

        Actor RuntimeActor(Comp body, int team)
        {
            var actor = new Actor();
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(new TeamComp(team));
            if (body != null) actor.AddComp(body);
            return actor;
        }

        Actor Combatant(BuffArenaActorDef def, bool player)
        {
            var actor = new Actor();
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(new AttributeSet());
            actor.AddComp(new BuffComp());
            actor.AddComp(new TeamComp(def.TeamId));
            actor.AddComp(new HealthComp());
            actor.AddComp(new StateMachineComp());
            actor.AddComp(new CharacterRadiusComp(def.BodyRadius));
            actor.AddComp(new LocomotionComp());
            actor.AddComp(new HitboxComp());
            actor.AddComp(new SkillDirectorComp(_data.Timelines));
            if (def.AmmoCapacity > 0) actor.AddComp(new AmmoComp(def.AmmoCapacity));

            if (player)
            {
                actor.AddComp(new InputBufferComp());
                actor.AddComp(new ProjectileTrackerComp());
                actor.AddComp(new BuffArenaPlayerComp(_data));
            }
            else if (def.BlueprintId == BuffArenaIds.EnemyBlueprint)
            {
                actor.AddComp(new BehaviorTreeComp(
                    new WanderShooter(BuffArenaIds.SkillEnemy, BuffArenaIds.TimelineEnemy),
                    board => board.AcquireRadius = 20f));
            }

            // 默认属性在所有组件装配完之后统一写入：组件挂载期不应依赖属性已有初值。
            actor.GetComp<AttributeSet>().InitFighterDefaults();
            return actor;
        }

        public void Release(Actor actor) => actor?.ResetForPool();
    }
}
