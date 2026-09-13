using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum ProjectileMotionKind : byte
    {
        Linear,
        ImmediateHoming,
        Accelerate,
        ReturnToOwner
    }

    public enum AoeMotionKind : byte
    {
        Static,
        Forward
    }

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
        public float SameTargetDelay;
        public bool RemoveOnObstacle;
        public bool Flying = true;
        // Ages (seconds since launch) at which the body falls back to the ground
        // layer for one frame. Source BoomBallRolling flips MoveType to ground while
        // the bouncing grenade is touching down, so it explodes on water then.
        public float[] GroundPhaseAt = Array.Empty<float>();
        public bool TrackOwner;
        public bool HitOwnerOnReturn;
        public string ViewBlueprintId;
        public IEffect[] OnHit = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
        public IEffect[] OnObstacle = Array.Empty<IEffect>();
        public IEffect[] OnOwnerHit = Array.Empty<IEffect>();
    }

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

    public sealed class ProjectileComp : Comp
    {
        readonly HashSet<long> _hits = new HashSet<long>();
        public EntityId OwnerId { get; private set; }
        public float SnapshotAtk { get; private set; }
        public ProjectileDefinition Def { get; private set; }
        public float FireYaw { get; private set; }
        public SimVec3 CurrentVelocity { get; private set; }
        public float Age { get; set; }
        public int HitCount { get; set; }
        public bool Exhausted { get; set; }
        public EntityId HomingTarget { get; set; }

        public void Setup(ProjectileDefinition def, EntityId owner, float snapshotAtk, EntityId homingTarget = default)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            FireYaw = 0f;
            CurrentVelocity = SimVec3.Zero;
            Age = 0f;
            HitCount = 0;
            Exhausted = false;
            HomingTarget = homingTarget;
            _hits.Clear();
            _cooldowns.Clear();
        }

        public bool TryRecord(EntityId id)
            => TryRecord(id, 0f);

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

        public void SetMotion(float yaw, in SimVec3 velocity)
        {
            FireYaw = yaw;
            CurrentVelocity = velocity;
        }

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
        }
    }

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

        public void SetRadius(float radius)
        {
            if (radius > 0f) Radius = radius;
        }

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

    public sealed class ProjectileService
    {
        readonly CombatWorld _world;
        readonly List<Actor> _actors = new List<Actor>(64);
        readonly List<Actor> _buffer = new List<Actor>(64);

        public ProjectileService(CombatWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public void Tick(float dt)
        {
            DrainSpawns();
            MoveAndHit(dt);
        }

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
                projectile.SetMotion(intent.Yaw, SimVec3.Zero);
                if (def.TrackOwner && owner != null && owner.TryGetComp<ProjectileTrackerComp>(out var tracker))
                    tracker.Track(id);
                _world.PublishSpawn(id, "projectile", def.ViewBlueprintId);
            });
        }

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
                var velocity = MotionVelocity(body, tf, def, owner);
                body.SetMotion(tf.YawDegrees, velocity);
                var next = new SimVec3(
                    tf.Position.X + velocity.X * dt,
                    tf.Position.Y + velocity.Y * dt,
                    tf.Position.Z + velocity.Z * dt);
                bool blocked;
                bool flyNow = def.Flying && !InGroundPhase(def, body.Age);
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
                body.Age += dt;
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

        bool IsValid(EntityId id)
        {
            if (!_world.TryGetActor(id, out var a) || a == null) return false;
            return !a.TryGetComp<TagComp>(out var tags) || !tags.Has(CommonTags.Dead);
        }

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

        static float NormalizeDeg(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
        }

        // True while the body is inside one of its touchdown windows (one logic frame
        // of tolerance, matching the 0.02s fixed step used by the source tweens).
        static bool InGroundPhase(ProjectileDefinition def, float age)
        {
            var phases = def.GroundPhaseAt;
            if (phases == null || phases.Length == 0) return false;
            for (int i = 0; i < phases.Length; i++)
            {
                if (Math.Abs(age - phases[i]) <= .02f) return true;
            }
            return false;
        }

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
