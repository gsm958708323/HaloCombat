using System;
using System.Collections.Generic;

namespace Combat.Core
{
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
        public IEffect[] OnHit = Array.Empty<IEffect>();
        public IEffect[] OnExpire = Array.Empty<IEffect>();
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
        public IEffect[] OnPulse = Array.Empty<IEffect>();
        public IEffect[] OnEnter = Array.Empty<IEffect>();
        public IEffect[] OnExit = Array.Empty<IEffect>();
        public IEffect[] OnStay = Array.Empty<IEffect>();
    }

    public sealed class ProjectileCatalog
    {
        readonly Dictionary<int, ProjectileDefinition> _map = new Dictionary<int, ProjectileDefinition>(8);
        public void Register(ProjectileDefinition def)
        {
            if (def == null || def.SpecId == 0) throw new ArgumentException("ProjectileDefinition");
            _map[def.SpecId] = def;
        }

        public bool TryGet(int specId, out ProjectileDefinition def) => _map.TryGetValue(specId, out def);
    }

    public sealed class AoeCatalog
    {
        readonly Dictionary<int, AoeDefinition> _map = new Dictionary<int, AoeDefinition>(8);
        public void Register(AoeDefinition def)
        {
            if (def == null || def.SpecId == 0) throw new ArgumentException("AoeDefinition");
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
        public float Age { get; set; }
        public int HitCount { get; set; }
        public bool Exhausted { get; set; }
        public EntityId HomingTarget { get; set; }

        public void Setup(ProjectileDefinition def, EntityId owner, float snapshotAtk, EntityId homingTarget = default)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            Age = 0f;
            HitCount = 0;
            Exhausted = false;
            HomingTarget = homingTarget;
            _hits.Clear();
        }

        public bool TryRecord(EntityId id)
        {
            if (!id.IsValid || Exhausted) return false;
            return _hits.Add(HitboxComp.Pack(id));
        }

        protected override void OnDetach()
        {
            _hits.Clear();
            Def = null;
            Exhausted = true;
        }
    }

    public sealed class AoeComp : Comp
    {
        HashSet<long> _inside;
        public EntityId OwnerId { get; private set; }
        public float SnapshotAtk { get; private set; }
        public AoeDefinition Def { get; private set; }
        public float Age { get; set; }
        public float PulseAcc { get; set; }
        public int BornFrame { get; private set; }
        public HashSet<long> Inside => _inside;

        public void Setup(AoeDefinition def, EntityId owner, float snapshotAtk, int bornFrame)
        {
            Def = def;
            OwnerId = owner;
            SnapshotAtk = snapshotAtk;
            Age = 0f;
            PulseAcc = 0f;
            BornFrame = bornFrame;
            _inside = (def != null && def.TrackOccupancy) ? new HashSet<long>() : null;
        }

        protected override void OnDetach()
        {
            _inside?.Clear();
            Def = null;
        }
    }

    public static class AoePulse
    {
        static readonly Actor[] Buffer = new Actor[32];

        public static void PulseNow(CombatWorld world, Actor aoe, AoeComp body)
        {
            if (world == null || aoe == null || body?.Def == null || !aoe.IsActive) return;
            var def = body.Def;
            if (def.OnPulse == null || def.OnPulse.Length == 0) return;
            var tf = aoe.GetComp<TransformComp>();
            world.TryGetActor(body.OwnerId, out var owner);
            int n = world.Query.OverlapCircle(tf.Position, def.Radius, owner, def.HostileMask, Buffer);
            for (int i = 0; i < n; i++)
            {
                var target = Buffer[i];
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
        readonly Actor[] _buffer = new Actor[32];

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
                proj.GetComp<ProjectileComp>().Setup(def, intent.Owner, snap, intent.Target);
                _world.PublishSpawn(id, "projectile");
            });
        }

        void MoveAndHit(float dt)
        {
            var actors = _world.RegistryActive();
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (!a.TryGetComp<ProjectileComp>(out var body) || body.Def == null || body.Exhausted) continue;
                if (!a.TryGetComp<TransformComp>(out var tf)) continue;
                var def = body.Def;
                _world.TryGetActor(body.OwnerId, out var owner);
                SteerHoming(a, body, tf, dt, owner);
                var fwd = LocomotionComp.ForwardFromYaw(tf.YawDegrees);
                tf.Position = new SimVec3(
                    tf.Position.X + fwd.X * def.Speed * dt,
                    tf.Position.Y,
                    tf.Position.Z + fwd.Z * def.Speed * dt);
                body.Age += dt;
                if (body.Age >= def.Lifetime)
                {
                    Expire(a, body);
                    continue;
                }

                int n = _world.Query.OverlapCircle(tf.Position, def.HitRadius, owner, def.HostileMask, _buffer);
                for (int k = 0; k < n; k++)
                {
                    var victim = _buffer[k];
                    if (victim == null || !body.TryRecord(victim.Id)) continue;
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

        void Expire(Actor proj, ProjectileComp body)
        {
            body.Exhausted = true;
            _world.TryGetActor(body.OwnerId, out var owner);
            if (body.Def.OnExpire != null && body.Def.OnExpire.Length > 0)
            {
                var tf = proj.GetComp<TransformComp>();
                _world.Deliver(body.Def.OnExpire, owner, null, body.SnapshotAtk, tf.Position, null, 0);
            }

            _world.RequestDespawn(proj.Id);
            proj.SetActive(false);
        }
    }

    public sealed class AoeService
    {
        readonly CombatWorld _world;
        readonly Actor[] _buffer = new Actor[32];
        readonly List<long> _scratch = new List<long>(16);

        public AoeService(CombatWorld world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public void Tick(float dt)
        {
            var actors = _world.RegistryActive();
            int frame = _world.Time.Frame;
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (!a.IsActive || !a.TryGetComp<AoeComp>(out var body) || body.Def == null) continue;
                if (body.BornFrame == frame) continue;

                var def = body.Def;
                body.Age += dt;
                _world.TryGetActor(body.OwnerId, out var owner);
                var tf = a.GetComp<TransformComp>();
                int n = _world.Query.OverlapCircle(tf.Position, def.Radius, owner, def.HostileMask, _buffer);

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

                if (def.Duration > 0f && body.Age >= def.Duration)
                    DespawnAoe(a, body, owner);
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

            _world.RequestDespawn(aoe.Id);
            aoe.SetActive(false);
        }
    }
}
