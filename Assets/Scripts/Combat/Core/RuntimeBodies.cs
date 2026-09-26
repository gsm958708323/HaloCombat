using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>子弹运动模式：Linear 匀速、ImmediateHoming 直线追踪、Accelerate 渐入、ReturnToOwner 往返、Bounce 分段抛物线弹跳。</summary>
    public enum ProjectileMotionKind : byte
    {
        Linear,
        ImmediateHoming,
        Accelerate,
        ReturnToOwner,
        Bounce
    }

    /// <summary>AoE 位移模式：Static 原地不动，Forward 沿朝向以 MoveSpeed 前进并叠加被吸收的冲量。</summary>
    public enum AoeMotionKind : byte
    {
        Static,
        Forward
    }

    /// <summary>
    /// 子弹的共享只读定义（按 SpecId 注册在 ProjectileCatalog，同 SpecId 的所有子弹共用同一个实例）。
    /// 运行时可变状态一律放 ProjectileComp，禁止往这里写每颗子弹的数据。
    /// </summary>
    public sealed class ProjectileDefinition
    {
        public int SpecId;
        public float Speed = 14f;
        public float Lifetime = 2f;
        public float HitRadius = 0.3f;
        public int MaxHits = 1;
        public bool SnapshotAtk = true;
        public int HostileMask;
        public int CueId;
        public float SpawnForward = 0.4f;
        public float HomingRate;
        public float HomingMaxTurn;
        public bool HomingRetarget;
        public float HomingAcquireRadius = 12f;
        public ProjectileMotionKind Motion;
        public float MotionParam = 5f;
        public float BounceHeight;
        public float SameTargetDelay;
        public bool RemoveOnObstacle;
        public bool Flying = true;
        // Cumulative touchdown ages. Bounce uses them as segment ends; every crossed
        // touchdown is resolved against the ground layer instead of relying on an
        // exact frame-time match.
        public float[] GroundPhaseAt = Array.Empty<float>();
        public bool TrackOwner;
        public bool HitOwnerOnReturn;
        public string ViewBlueprintId;
        public IEffect[] OnHit = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
        public IEffect[] OnObstacle = Array.Empty<IEffect>();
        public IEffect[] OnOwnerHit = Array.Empty<IEffect>();
    }

    /// <summary>AoE 的共享只读定义（AoeCatalog 按 SpecId 索引，实例被所有同 SpecId 的 AoE 共用）；每片 AoE 的可变状态一律放 AoeComp。</summary>
    public sealed class AoeDefinition
    {
        public int SpecId;
        public float Radius = 1.3f;
        public float Duration = 2f;
        public float PulseInterval = 0.45f;
        public bool PulseOnSpawn = true;
        public bool TrackOccupancy;
        public int HostileMask;
        public int CueId;
        public AoeMotionKind Motion;
        public float MoveSpeed;
        public bool RemoveOnObstacle;
        public bool Flying = true;
        public bool TrackProjectiles;
        public float ProjectileAbsorbForce;
        public float ProjectileRadiusScale = .05f;
        public string ViewBlueprintId;
        public IEffect[] OnPulse = Array.Empty<IEffect>();
        public IEffect[] OnEnter = Array.Empty<IEffect>();
        public IEffect[] OnExit = Array.Empty<IEffect>();
        public IEffect[] OnStay = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
    }

    /// <summary>子弹定义表：SpecId 必须非 0，重复注册抛异常，避免配置静默覆盖。</summary>
    public sealed class ProjectileCatalog
    {
        readonly Dictionary<int, ProjectileDefinition> _map = new Dictionary<int, ProjectileDefinition>(8);
        public int Count => _map.Count;
        public IEnumerable<ProjectileDefinition> All => _map.Values;
        public void Register(ProjectileDefinition def)
        {
            if (def == null || def.SpecId == 0) throw new ArgumentException("ProjectileDefinition");
            if (_map.ContainsKey(def.SpecId)) throw new InvalidOperationException("Duplicate projectile " + def.SpecId);
            _map[def.SpecId] = def;
        }

        public bool TryGet(int specId, out ProjectileDefinition def) => _map.TryGetValue(specId, out def);
    }

    /// <summary>AoE 定义表：SpecId 必须非 0，重复注册抛异常。</summary>
    public sealed class AoeCatalog
    {
        readonly Dictionary<int, AoeDefinition> _map = new Dictionary<int, AoeDefinition>(8);
        public int Count => _map.Count;
        public IEnumerable<AoeDefinition> All => _map.Values;
        public void Register(AoeDefinition def)
        {
            if (def == null || def.SpecId == 0) throw new ArgumentException("AoeDefinition");
            if (_map.ContainsKey(def.SpecId)) throw new InvalidOperationException("Duplicate aoe " + def.SpecId);
            _map[def.SpecId] = def;
        }

        public bool TryGet(int specId, out AoeDefinition def) => _map.TryGetValue(specId, out def);
    }

    /// <summary>
    /// 单颗子弹的运行时状态：缓存定义引用与生成时快照（OwnerId/SnapshotAtk/Def 在生成后不再变），
    /// 命中集合与同目标冷却都是每实体一份。
    /// </summary>
    public sealed class ProjectileComp : Comp
    {
        readonly HashSet<long> _hits = new HashSet<long>();
        public EntityId OwnerId { get; private set; }
        public float SnapshotAtk { get; private set; }
        public ProjectileDefinition Def { get; private set; }
        public float FireYaw { get; private set; }
        public SimVec3 CurrentVelocity { get; private set; }
        public float GroundY { get; private set; }
        public float Age { get; set; }
        public int HitCount { get; set; }
        public bool Exhausted { get; set; }
        public EntityId HomingTarget { get; set; }

        /// <summary>初始化/复用实体时重置全部字段（含命中集合与冷却），防止对象池复用残留上一次的命中记录导致新子弹打不中。</summary>
        public void Setup(ProjectileDefinition def, EntityId owner, float snapshotAtk, EntityId homingTarget = default)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            FireYaw = 0f;
            CurrentVelocity = SimVec3.Zero;
            GroundY = 0f;
            Age = 0f;
            HitCount = 0;
            Exhausted = false;
            HomingTarget = homingTarget;
            _hits.Clear();
            _cooldowns.Clear();
        }

        public void SetGroundY(float groundY) => GroundY = groundY;

        public bool TryRecord(EntityId id)
            => TryRecord(id, 0f);

        /// <summary>记录一次命中：返回 false 表示该目标已记录或子弹已耗尽，调用方不应再结算。cooldown 大于 0 时给该目标记冷却，冷却到期后允许再次命中。</summary>
        public bool TryRecord(EntityId id, float cooldown)
        {
            if (!id.IsValid || Exhausted) return false;
            long packed = HitboxComp.Pack(id);
            if (!_hits.Add(packed)) return false;
            if (cooldown > 0f)
                _cooldowns[packed] = cooldown;
            return true;
        }

        readonly Dictionary<long, float> _cooldowns = new Dictionary<long, float>();

        /// <summary>推进 SameTargetDelay 冷却：到期的目标键同时从冷却表和已命中集合里移除，从而重新可被打中。</summary>
        public void TickHitCooldowns(float dt)
        {
            if (_cooldowns.Count == 0) return;
            var expired = new List<long>();
            var keys = new List<long>(_cooldowns.Keys);
            for (int i = 0; i < keys.Count; i++)
            {
                long key = keys[i];
                float left = _cooldowns[key] - dt;
                if (left <= 0f) expired.Add(key);
                else _cooldowns[key] = left;
            }
            for (int i = 0; i < expired.Count; i++)
            {
                _cooldowns.Remove(expired[i]);
                _hits.Remove(expired[i]);
            }
        }

        /// <summary>记录本帧朝向与速度，供表现层和 AoE 吸收读取；不参与运动解算本身。</summary>
        public void SetMotion(float yaw, in SimVec3 velocity)
        {
            FireYaw = yaw;
            CurrentVelocity = velocity;
        }

        /// <summary>实体销毁：先从拥有者的 ProjectileTrackerComp 摘掉自己（防悬挂引用），再清空命中/冷却并把 Exhausted 置位。</summary>
        protected override void OnDetach()
        {
            if (OwnerId.IsValid && Self != null && Self.World != null &&
                Self.World.TryGetActor(OwnerId, out var owner) && owner != null &&
                owner.TryGetComp<ProjectileTrackerComp>(out var tracker))
                tracker.ClearIf(Self.Id);
            _hits.Clear();
            _cooldowns.Clear();
            Def = null;
            Exhausted = true;
            CurrentVelocity = SimVec3.Zero;
            GroundY = 0f;
        }
    }

    /// <summary>
    /// 单片 AoE 的运行时状态：半径可被吸收放大（Radius 与 BaseRadius 分离），
    /// Inside 只在定义开启 TrackOccupancy 时才分配，否则为 null。
    /// </summary>
    public sealed class AoeComp : Comp
    {
        HashSet<long> _inside;
        public EntityId OwnerId { get; private set; }
        public float SnapshotAtk { get; private set; }
        public AoeDefinition Def { get; private set; }
        public float Radius { get; private set; }
        public float BaseRadius { get; private set; }
        public float Duration { get; private set; }
        public float Age { get; set; }
        public float PulseAcc { get; set; }
        public int BornFrame { get; private set; }
        public HashSet<long> Inside => _inside;
        public int AbsorbedProjectileCount { get; private set; }
        public float VisualScale { get; private set; } = 1f;
        public SimVec3 CurrentVelocity { get; private set; }

        public void SetVisualScale(float scale) => VisualScale = scale > 0f ? scale : 1f;

        /// <summary>吸收一颗子弹：累计冲量、按 radiusScale 放大视觉与碰撞半径。碰撞半径一起放大是源工程行为，不是纯表现。</summary>
        public void RegisterAbsorption(in SimVec3 velocity, float force, float radiusScale)
        {
            AbsorbedProjectileCount++;
            CurrentVelocity = new SimVec3(
                CurrentVelocity.X + velocity.X * force,
                CurrentVelocity.Y + velocity.Y * force,
                CurrentVelocity.Z + velocity.Z * force);
            SetVisualScale(1f + AbsorbedProjectileCount * radiusScale);
            // Source SpaceMonkeyBallHit grows the *collision* radius with every
            // absorbed bullet (aoeState.radius = 0.25f * scaleTo), not just the view.
            SetRadius(BaseRadius * VisualScale);
        }

        /// <summary>只有正半径才生效，防止覆盖或吸收把判据压成 0。</summary>
        public void SetRadius(float radius)
        {
            if (radius > 0f) Radius = radius;
        }

        /// <summary>初始化/重置：覆盖值大于 0 才采用，否则回落到定义值；BaseRadius 记住初始半径，供吸收时按比例放大。</summary>
        public void Setup(AoeDefinition def, EntityId owner, float snapshotAtk, int bornFrame,
            float radiusOverride = 0f, float durationOverride = 0f)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            Age = 0f;
            PulseAcc = 0f;
            BornFrame = bornFrame;
            Radius = radiusOverride > 0f ? radiusOverride : (def != null ? def.Radius : 0f);
            BaseRadius = Radius;
            Duration = durationOverride > 0f ? durationOverride : (def != null ? def.Duration : 0f);
            AbsorbedProjectileCount = 0;
            VisualScale = 1f;
            CurrentVelocity = SimVec3.Zero;
            _inside = (def != null && def.TrackOccupancy) ? new HashSet<long>() : null;
        }

        /// <summary>销毁/复用时清空占用集合与所有缓存，避免对象池复用串味。</summary>
        protected override void OnDetach()
        {
            _inside?.Clear();
            Def = null;
            Radius = 0f;
            BaseRadius = 0f;
            Duration = 0f;
            AbsorbedProjectileCount = 0;
            VisualScale = 1f;
            CurrentVelocity = SimVec3.Zero;
        }
    }

    /// <summary>AoE 判定辅助：PulseNow 供生成当帧立即脉冲（不等第一个 PulseInterval），DeliverBag 统一走 world.Deliver 投递效果包。</summary>
    public static class AoePulse
    {
        public static void PulseNow(CombatWorld world, Actor aoe, AoeComp body)
        {
            if (world == null || aoe == null || body?.Def == null || !aoe.IsActive) return;
            var def = body.Def;
            if (def.OnPulse == null || def.OnPulse.Length == 0) return;
            var tf = aoe.GetComp<TransformComp>();
            world.TryGetActor(body.OwnerId, out var owner);
            var buffer = new List<Actor>(64);
            int n = world.Query.OverlapCircle(tf.Position, body.Radius, owner, def.HostileMask, buffer);
            for (int i = 0; i < n; i++)
            {
                var target = buffer[i];
                if (target == null || !target.IsActive) continue;
                DeliverBag(world, def.OnPulse, owner, target, body.SnapshotAtk, tf.Position);
            }
        }

        public static void DeliverBag(CombatWorld world, IEffect[] bag, Actor owner, Actor target, float snapshotAtk, SimVec3 point)
        {
            if (bag == null || bag.Length == 0) return;
            world.Deliver(bag, owner, target, snapshotAtk, point, null, 0);
        }
    }

    /// <summary>子弹服务：每帧先 DrainSpawns 消费发射意图生成实体，再 MoveAndHit 推进所有存活子弹。</summary>
    public sealed class ProjectileService
    {
        readonly CombatWorld _world;
        readonly List<Actor> _actors = new List<Actor>(64);
        readonly List<Actor> _buffer = new List<Actor>(64);

        public ProjectileService(CombatWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>固定两段：先生成再推进，所以本帧新生成的子弹本帧就会移动一帧。</summary>
        public void Tick(float dt)
        {
            DrainSpawns();
            MoveAndHit(dt);
        }

        /// <summary>消费 SpawnProjectileIntent：按 SpawnForward 沿发射朝向偏移出生点、继承阵营、装配定义；快照 Atk 由定义 SnapshotAtk 决定是否取当前属性。</summary>
        void DrainSpawns()
        {
            _world.Intents.Drain<SpawnProjectileIntent>(intent =>
            {
                if (!_world.Projectiles.TryGet(intent.SpecId, out var def)) return;
                var id = _world.SpawnActor(new ActorSpawnSpec("projectile"), publishSpawn: false);
                if (!_world.TryGetActor(id, out var proj) || proj == null) return;
                var tf = proj.GetComp<TransformComp>();
                var fwd = LocomotionComp.ForwardFromYaw(intent.Yaw);
                tf.Position = new SimVec3(
                    intent.Origin.X + fwd.X * def.SpawnForward,
                    intent.Origin.Y,
                    intent.Origin.Z + fwd.Z * def.SpawnForward);
                tf.YawDegrees = intent.Yaw;
                _world.TryGetActor(intent.Owner, out var owner);
                SpawnAoeEffect.CopyTeam(owner, proj);
                float snap = def.SnapshotAtk ? intent.SnapshotAtk : 0f;
                if (!def.SnapshotAtk && owner != null && owner.TryGetComp<AttributeSet>(out var attr))
                    snap = attr.GetFinal(AttrId.Atk);
                var projectile = proj.GetComp<ProjectileComp>();
                projectile.Setup(def, intent.Owner, snap, intent.Target);
                projectile.SetGroundY(tf.Position.Y);
                projectile.SetMotion(intent.Yaw, SimVec3.Zero);
                if (def.TrackOwner && owner != null && owner.TryGetComp<ProjectileTrackerComp>(out var tracker))
                    tracker.Track(id);
                _world.PublishSpawn(id, "projectile", def.ViewBlueprintId);
            });
        }

        /// <summary>
        /// 推进本帧所有存活子弹，每颗的顺序固定：
        /// 1) 递减命中冷却（SameTargetDelay 到期后重新可命中）；
        /// 2) 转向：SteerHoming 按 HomingRate 与 HomingMaxTurn 逐帧逼近锁定目标；
        /// 3) 速度：MotionVelocity 按运动模式算出本帧速度；
        /// 4) 障碍：Movement.Resolve 解算，RemoveOnObstacle 时直接走 OnObstacle 过期分支；
        /// 5) 寿命：Age 超过 Lifetime 触发 OnExpire；
        /// 6) 回身命中：HitOwnerOnReturn 且已过 MotionParam 时撞到拥有者走 OnOwnerHit；
        /// 7) 圆查询命中：OverlapCircle 后按实体去重（TryRecord），SameTargetDelay 决定能否重复命中；
        /// 8) MaxHits：命中数达到上限立即耗尽并请求销毁。
        /// 无敌目标不消耗命中次数（对齐源工程 CanHit 的 immuneTime 提前返回）。
        /// </summary>
        void MoveAndHit(float dt)
        {
            _world.RegistryActive(_actors);
            var actors = _actors;
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (!a.TryGetComp<ProjectileComp>(out var body) || body.Def == null || body.Exhausted) continue;
                if (!a.TryGetComp<TransformComp>(out var tf)) continue;
                var def = body.Def;
                _world.TryGetActor(body.OwnerId, out var owner);
                body.TickHitCooldowns(dt);
                SteerHoming(a, body, tf, dt, owner);
                float previousAge = body.Age;
                float nextAge = Math.Min(previousAge + dt, def.Lifetime);
                bool touchdown = CrossedGroundPhase(def, previousAge, nextAge);
                var velocity = MotionVelocity(body, tf, def, owner);
                var next = new SimVec3(
                    tf.Position.X + velocity.X * dt,
                    def.Motion == ProjectileMotionKind.Bounce
                        ? body.GroundY + BounceHeightAt(def, nextAge)
                        : tf.Position.Y + velocity.Y * dt,
                    tf.Position.Z + velocity.Z * dt);
                if (touchdown && def.Motion == ProjectileMotionKind.Bounce)
                    next.Y = body.GroundY;
                velocity.Y = (next.Y - tf.Position.Y) / Math.Max(dt, .0001f);
                body.SetMotion(tf.YawDegrees, velocity);
                bool blocked;
                bool flyNow = def.Flying && !touchdown;
                next = _world.Movement.Resolve(tf.Position, next, def.HitRadius, flyNow, false, out blocked);
                if (blocked)
                {
                    if (def.RemoveOnObstacle)
                    {
                        Expire(a, body, true);
                        continue;
                    }
                    velocity = new SimVec3(
                        (next.X - tf.Position.X) / Math.Max(dt, .0001f),
                        velocity.Y,
                        (next.Z - tf.Position.Z) / Math.Max(dt, .0001f));
                    body.SetMotion(tf.YawDegrees, velocity);
                }
                tf.Position = next;
                body.Age = nextAge;
                if (body.Age >= def.Lifetime)
                {
                    Expire(a, body, false);
                    continue;
                }

                if (def.HitOwnerOnReturn && body.Age >= def.MotionParam && owner != null &&
                    owner.TryGetComp<TransformComp>(out var ownerTf))
                {
                    float ownerRadius = owner.TryGetComp<CharacterRadiusComp>(out var ownerBody)
                        ? ownerBody.Radius
                        : .25f;
                    float dx = ownerTf.Position.X - tf.Position.X;
                    float dz = ownerTf.Position.Z - tf.Position.Z;
                    float rr = def.HitRadius + ownerRadius;
                    if (dx * dx + dz * dz <= rr * rr)
                    {
                        body.Exhausted = true;
                        _world.Deliver(def.OnOwnerHit, owner, owner, body.SnapshotAtk, tf.Position, null, 0);
                        _world.RequestDespawn(a.Id);
                        a.SetActive(false);
                        continue;
                    }
                }

                int n = _world.Query.OverlapCircle(tf.Position, def.HitRadius, owner, def.HostileMask, _buffer);
                for (int k = 0; k < n; k++)
                {
                    var victim = _buffer[k];
                    // Pass through instead of consuming a hit: matches the source
                    // BulletState.CanHit early-out for immuneTime > 0.
                    if (victim != null && victim.TryGetComp<HealthComp>(out var victimHealth) &&
                        victimHealth.IsInvulnerable)
                        continue;
                    if (victim == null || !body.TryRecord(victim.Id, def.SameTargetDelay)) continue;
                    float snap = def.SnapshotAtk ? body.SnapshotAtk : (owner != null && owner.TryGetComp<AttributeSet>(out var at) ? at.GetFinal(AttrId.Atk) : body.SnapshotAtk);
                    var vpos = victim.TryGetComp<TransformComp>(out var vtf) ? vtf.Position : tf.Position;
                    _world.Intents.Post(new ApplyEffectsIntent(def.OnHit, body.OwnerId, victim.Id, snap, 0, vpos, true));
                    body.HitCount++;
                    if (def.MaxHits > 0 && body.HitCount >= def.MaxHits)
                    {
                        body.Exhausted = true;
                        _world.RequestDespawn(a.Id);
                        a.SetActive(false);
                        break;
                    }
                }
            }
        }

        /// <summary>按运动模式产出本帧速度：Accelerate 用 2t/(t+pivot) 渐入；ReturnToOwner 复刻源工程飞镖的正弦去程，回程直接朝拥有者飞。</summary>
        static SimVec3 MotionVelocity(ProjectileComp body, TransformComp tf, ProjectileDefinition def, Actor owner)
        {
            float scale = 1f;
            if (def.Motion == ProjectileMotionKind.Accelerate)
            {
                float t = Math.Max(0f, body.Age);
                float pivot = def.MotionParam > 0f ? def.MotionParam : 5f;
                scale = 2f * t / (t + pivot);
            }
            else if (def.Motion == ProjectileMotionKind.ReturnToOwner)
            {
                // Mirrors the source CloakBoomerangTween: the outbound leg is
                // modulated by sin(t / backTime * PI) + 0.1, and once backTime has
                // elapsed the body turns back toward the thrower with the same
                // sine curve capped at half a period.
                float backTime = def.MotionParam > 0f ? def.MotionParam : 1f;
                if (body.Age < backTime)
                {
                    float outRad = body.Age / backTime * (float)Math.PI;
                    scale = (float)Math.Sin(outRad) + .1f;
                }
                else if (owner != null && owner.TryGetComp<TransformComp>(out var ownerTf))
                {
                    float backRad = Math.Min((body.Age - backTime) / backTime * (float)Math.PI, .5f);
                    float magnitude = (float)Math.Sin(backRad) + .1f;
                    float dx = ownerTf.Position.X - tf.Position.X;
                    float dz = ownerTf.Position.Z - tf.Position.Z;
                    float len = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (len > .0001f)
                        return new SimVec3(dx / len * def.Speed * magnitude, 0f, dz / len * def.Speed * magnitude);
                }
            }

            var fwd = LocomotionComp.ForwardFromYaw(tf.YawDegrees);
            return new SimVec3(fwd.X * def.Speed * scale, 0f, fwd.Z * def.Speed * scale);
        }

        /// <summary>锁定转向：锁定目标失效时按 HomingRetarget 决定重选还是保持原航向；单帧转角同时受 HomingRate*dt 与 HomingMaxTurn 限制。</summary>
        void SteerHoming(Actor proj, ProjectileComp body, TransformComp tf, float dt, Actor owner)
        {
            var def = body.Def;
            if (def == null || def.HomingRate <= 0f) return;
            if (!body.HomingTarget.IsValid)
                body.HomingTarget = AcquireNearest(tf.Position, owner, def);
            else if (!IsValid(body.HomingTarget))
            {
                // A projectile keeps its last heading when the locked target dies.
                // Optional retargeting is an explicit definition flag.
                if (!def.HomingRetarget) return;
                body.HomingTarget = AcquireNearest(tf.Position, owner, def);
            }
            if (!IsValid(body.HomingTarget) || !_world.TryGetActor(body.HomingTarget, out var target) ||
                !target.TryGetComp<TransformComp>(out var targetTf)) return;

            float want = LocomotionComp.YawFromStick(new SimVec3(
                targetTf.Position.X - tf.Position.X, 0f, targetTf.Position.Z - tf.Position.Z));
            float delta = NormalizeDeg(want - tf.YawDegrees);
            float step = def.HomingRate * dt;
            if (def.HomingMaxTurn > 0f && step > def.HomingMaxTurn) step = def.HomingMaxTurn;
            if (delta > step) delta = step;
            else if (delta < -step) delta = -step;
            tf.YawDegrees += delta;
        }

        /// <summary>锁定目标是否仍可追击：实体存在且未挂 Dead 标签。</summary>
        bool IsValid(EntityId id)
        {
            if (!_world.TryGetActor(id, out var a) || a == null) return false;
            return !a.TryGetComp<TagComp>(out var tags) || !tags.Has(CommonTags.Dead);
        }

        /// <summary>在 HomingAcquireRadius 内重新锁定最近的可命中目标。</summary>
        EntityId AcquireNearest(SimVec3 origin, Actor owner, ProjectileDefinition def)
        {
            int n = _world.Query.OverlapCircle(origin, def.HomingAcquireRadius, owner, def.HostileMask, _buffer);
            float best = float.MaxValue;
            EntityId pick = EntityId.Invalid;
            for (int i = 0; i < n; i++)
            {
                var v = _buffer[i];
                if (v == null || !v.TryGetComp<TransformComp>(out var tf)) continue;
                float dx = tf.Position.X - origin.X;
                float dz = tf.Position.Z - origin.Z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) { best = d2; pick = v.Id; }
            }
            return pick;
        }

        /// <summary>把角度归一到 ±180 以内，避免累计转向出现大跳变。</summary>
        static float NormalizeDeg(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }

        static float BounceHeightAt(ProjectileDefinition def, float age)
        {
            var phases = def.GroundPhaseAt;
            if (def.Motion != ProjectileMotionKind.Bounce || def.BounceHeight <= 0f ||
                phases == null || phases.Length == 0 || age <= 0f)
                return 0f;

            float segmentStart = 0f;
            float firstDuration = phases[0];
            if (firstDuration <= 0f) return 0f;
            for (int i = 0; i < phases.Length; i++)
            {
                float segmentEnd = phases[i];
                float duration = segmentEnd - segmentStart;
                if (duration <= 0f)
                {
                    segmentStart = segmentEnd;
                    continue;
                }
                if (age <= segmentEnd)
                {
                    float t = Math.Max(0f, Math.Min(1f, (age - segmentStart) / duration));
                    float durationScale = duration / firstDuration;
                    float peak = def.BounceHeight * durationScale * durationScale;
                    return 4f * peak * t * (1f - t);
                }
                segmentStart = segmentEnd;
            }
            return 0f;
        }

        static bool CrossedGroundPhase(ProjectileDefinition def, float previousAge, float nextAge)
        {
            var phases = def.GroundPhaseAt;
            if (phases == null || phases.Length == 0) return false;
            for (int i = 0; i < phases.Length; i++)
            {
                float touchdown = phases[i];
                if (touchdown > previousAge && touchdown <= nextAge) return true;
            }
            return false;
        }

        /// <summary>子弹收尾：先置 Exhausted，按 obstacle 选择 OnObstacle 或 OnExpire 投递，再请求销毁并立即 SetActive(false)，防止本帧后续步骤重复处理。</summary>
        void Expire(Actor proj, ProjectileComp body, bool obstacle)
        {
            body.Exhausted = true;
            _world.TryGetActor(body.OwnerId, out var owner);
            var effects = obstacle ? body.Def.OnObstacle : body.Def.OnExpire;
            if (effects != null && effects.Length > 0)
            {
                var tf = proj.GetComp<TransformComp>();
                _world.Deliver(effects, owner, null, body.SnapshotAtk, tf.Position, null, 0);
            }

            _world.RequestDespawn(proj.Id);
            proj.SetActive(false);
        }
    }

    /// <summary>AoE 服务：推进位移、吸收同队子弹、按占用差集触发进入/离开、按脉冲间隔触发 OnPulse，到期销毁。</summary>
    public sealed class AoeService
    {
        readonly CombatWorld _world;
        readonly List<Actor> _actors = new List<Actor>(64);
        readonly List<Actor> _buffer = new List<Actor>(64);
        readonly List<long> _scratch = new List<long>(16);

        public AoeService(CombatWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        /// <summary>
        /// 推进所有存活 AoE（出生当帧 BornFrame == frame 直接跳过，因为 PulseOnSpawn 已在生成时处理过）：
        /// 1) 位移：Forward 模式沿朝向移动并叠加吸收冲量，RemoveOnObstacle 时撞墙直接销毁；
        /// 2) 吸附：TrackProjectiles 时吸收同队子弹，每吸收一颗半径按 ProjectileRadiusScale 变大；
        /// 3) 占用差集：TrackOccupancy 时先算本帧新进入者并立即投递 OnEnter；
        /// 4) 脉冲：PulseInterval 累积到点，对范围内目标投递 OnPulse（本帧刚进入的不重复吃脉冲）；
        /// 5) 停留：OnStay 每帧对范围内目标投递；
        /// 6) 寿命：Age 超过 Duration 时走 DespawnAoe（先补 OnExit 再 OnExpire）。
        /// </summary>
        public void Tick(float dt)
        {
            _world.RegistryActive(_actors);
            var actors = _actors;
            int frame = _world.Time.LogicFrame;
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (!a.IsActive || !a.TryGetComp<AoeComp>(out var body) || body.Def == null) continue;
                if (body.BornFrame == frame) continue;

                var def = body.Def;
                body.Age += dt;
                _world.TryGetActor(body.OwnerId, out var owner);
                var tf = a.GetComp<TransformComp>();
                if (def.Motion == AoeMotionKind.Forward && def.MoveSpeed != 0f)
                {
                    var fwd = LocomotionComp.ForwardFromYaw(tf.YawDegrees);
                    var desired = new SimVec3(
                        tf.Position.X + fwd.X * def.MoveSpeed * dt + body.CurrentVelocity.X * dt,
                        tf.Position.Y + body.CurrentVelocity.Y * dt,
                        tf.Position.Z + fwd.Z * def.MoveSpeed * dt + body.CurrentVelocity.Z * dt);
                    bool blocked;
                    var resolved = _world.Movement.Resolve(tf.Position, desired, body.Radius, def.Flying, false, out blocked);
                    if (blocked && def.RemoveOnObstacle)
                    {
                        DespawnAoe(a, body, owner);
                        continue;
                    }
                    tf.Position = resolved;
                }

                if (def.TrackProjectiles)
                    AbsorbProjectiles(body, tf.Position, owner, actors);

                int n = _world.Query.OverlapCircle(tf.Position, body.Radius, owner, def.HostileMask, _buffer);

                HashSet<long> entered = null;
                if (def.TrackOccupancy)
                    entered = DiffOccupancy(a, body, owner, n, tf.Position);

                bool doPulse = false;
                if (def.PulseInterval > 0f && def.OnPulse != null && def.OnPulse.Length > 0)
                {
                    body.PulseAcc += dt;
                    if (body.PulseAcc >= def.PulseInterval)
                    {
                        body.PulseAcc -= def.PulseInterval;
                        doPulse = true;
                    }
                }

                if (doPulse)
                {
                    for (int k = 0; k < n; k++)
                    {
                        var target = _buffer[k];
                        if (target == null || !target.IsActive) continue;
                        if (entered != null && entered.Contains(HitboxComp.Pack(target.Id))) continue;
                        AoePulse.DeliverBag(_world, def.OnPulse, owner, target, body.SnapshotAtk, tf.Position);
                    }
                }

                if (def.TrackOccupancy && def.OnStay != null && def.OnStay.Length > 0)
                {
                    for (int k = 0; k < n; k++)
                    {
                        var target = _buffer[k];
                        if (target == null || !target.IsActive) continue;
                        AoePulse.DeliverBag(_world, def.OnStay, a, target, body.SnapshotAtk, tf.Position);
                    }
                }

                if (body.Duration > 0f && body.Age >= body.Duration)
                    DespawnAoe(a, body, owner);
            }
        }

        /// <summary>吸收同队（TeamId 相同）且在半径内的子弹：加冲量、放大半径、置 Exhausted 并销毁；敌人子弹不会被吸走。</summary>
        void AbsorbProjectiles(AoeComp body, SimVec3 center, Actor owner, List<Actor> actors)
        {
            if (owner == null || !owner.TryGetComp<TeamComp>(out var ownerTeam)) return;
            for (int i = 0; i < actors.Count; i++)
            {
                var projectile = actors[i];
                if (projectile == null || !projectile.IsActive || !projectile.TryGetComp<ProjectileComp>(out var p) ||
                    p.Def == null || !projectile.TryGetComp<TransformComp>(out var tf)) continue;
                if (!projectile.TryGetComp<TeamComp>(out var projectileTeam) ||
                    projectileTeam.TeamId != ownerTeam.TeamId) continue;
                float dx = tf.Position.X - center.X;
                float dz = tf.Position.Z - center.Z;
                float radius = body.Radius + p.Def.HitRadius;
                if (dx * dx + dz * dz > radius * radius) continue;

                body.RegisterAbsorption(p.CurrentVelocity, body.Def.ProjectileAbsorbForce,
                    body.Def.ProjectileRadiusScale);
                p.Exhausted = true;
                _world.RequestDespawn(projectile.Id);
                projectile.SetActive(false);
                if (body.Def.CueId != 0)
                    _world.Events.Publish(new EvCue(body.Def.CueId, owner.Id, "ProjectileAbsorb"));
            }
        }

        /// <summary>与上一帧的占用集合做差：本帧新进入者返回给调用方（并立即投递 OnEnter），离开者投递 OnExit 后从集合移除。</summary>
        HashSet<long> DiffOccupancy(Actor aoe, AoeComp body, Actor owner, int n, SimVec3 point)
        {
            var inside = body.Inside;
            if (inside == null) return null;
            var now = new HashSet<long>();
            var entered = new HashSet<long>();
            for (int i = 0; i < n; i++)
            {
                var t = _buffer[i];
                if (t == null) continue;
                long p = HitboxComp.Pack(t.Id);
                now.Add(p);
                if (inside.Add(p))
                {
                    entered.Add(p);
                    AoePulse.DeliverBag(_world, body.Def.OnEnter, aoe, t, body.SnapshotAtk, point);
                }
            }

            _scratch.Clear();
            foreach (var old in inside)
                if (!now.Contains(old)) _scratch.Add(old);
            for (int i = 0; i < _scratch.Count; i++)
            {
                long p = _scratch[i];
                inside.Remove(p);
                var ent = HitboxComp.Unpack(p);
                if (_world.TryGetActor(ent, out var leaver) && leaver != null)
                    AoePulse.DeliverBag(_world, body.Def.OnExit, aoe, leaver, body.SnapshotAtk, point);
            }

            return entered;
        }

        /// <summary>销毁收尾：先给仍在范围内的目标补 OnExit（否则离开事件永久丢失），再投递 OnExpire，最后请求销毁。</summary>
        public void DespawnAoe(Actor aoe, AoeComp body, Actor owner)
        {
            if (body.Def != null && body.Def.TrackOccupancy && body.Inside != null)
            {
                var tf = aoe.GetComp<TransformComp>();
                foreach (var p in body.Inside)
                {
                    var ent = HitboxComp.Unpack(p);
                    if (_world.TryGetActor(ent, out var t) && t != null)
                        AoePulse.DeliverBag(_world, body.Def.OnExit, aoe, t, body.SnapshotAtk, tf.Position);
                }

                body.Inside.Clear();
            }

            if (body.Def != null && body.Def.OnExpire != null && body.Def.OnExpire.Length > 0)
            {
                var tf = aoe.GetComp<TransformComp>();
                _world.Deliver(body.Def.OnExpire, owner, null, body.SnapshotAtk, tf.Position, null, 0);
            }

            _world.RequestDespawn(aoe.Id);
            aoe.SetActive(false);
        }
    }
}
