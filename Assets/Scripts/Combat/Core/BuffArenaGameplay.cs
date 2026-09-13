using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public static class BuffArenaIds
    {
        public const int SkillFireValue = 2001;
        public const int SkillRollValue = 2002;
        public const int SkillMonkeyValue = 2003;
        public const int SkillHomingValue = 2004;
        public const int SkillBoomerangValue = 2005;
        public const int SkillTeleportValue = 2006;
        public const int SkillGrenadeValue = 2007;
        public const int SkillBarrelValue = 2008;
        public const int SkillReloadValue = 2009;
        public const int SkillEnemyValue = 2010;

        public const int TimelineFireValue = 3001;
        public const int TimelineRollValue = 3002;
        public const int TimelineMonkeyValue = 3003;
        public const int TimelineHomingValue = 3004;
        public const int TimelineBoomerangValue = 3005;
        public const int TimelineTeleportValue = 3006;
        public const int TimelineGrenadeValue = 3007;
        public const int TimelineBarrelValue = 3008;
        public const int TimelineReloadValue = 3009;
        public const int TimelineEnemyValue = 3010;

        public const int ProjectileNormalValue = 4001;
        public const int ProjectileEnemyValue = 4002;
        public const int ProjectileBoomerangValue = 4003;
        public const int ProjectileTeleportValue = 4004;
        public const int ProjectileBombValue = 4005;
        public const int ProjectileHomingValue = 4006;

        public const int AoeShieldValue = 4101;
        public const int AoeMonkeyValue = 4102;
        public const int AoeBlackHoleValue = 4103;
        public const int AoeExplosionValue = 4104;
        public const int AoeStayingBombValue = 4105;

        public const int CueMuzzleValue = 4201;
        public const int CueHeartValue = 4202;
        public const int CueRollFireValue = 4203;
        public const int CueHitValue = 4204;
        public const int CueShieldValue = 4205;
        public const int CueExplosionValue = 4206;
        public const int CueStarValue = 4207;
        public const int CueShockwaveValue = 4208;

        public static readonly SkillNodeId Fire = new SkillNodeId(SkillFireValue);
        public static readonly SkillNodeId Roll = new SkillNodeId(SkillRollValue);
        public static readonly SkillNodeId Monkey = new SkillNodeId(SkillMonkeyValue);
        public static readonly SkillNodeId Homing = new SkillNodeId(SkillHomingValue);
        public static readonly SkillNodeId Boomerang = new SkillNodeId(SkillBoomerangValue);
        public static readonly SkillNodeId Teleport = new SkillNodeId(SkillTeleportValue);
        public static readonly SkillNodeId Grenade = new SkillNodeId(SkillGrenadeValue);
        public static readonly SkillNodeId Barrel = new SkillNodeId(SkillBarrelValue);
        public static readonly SkillNodeId Reload = new SkillNodeId(SkillReloadValue);
        public static readonly SkillNodeId SkillEnemy = new SkillNodeId(SkillEnemyValue);

        public static readonly TimelineId FireTimeline = new TimelineId(TimelineFireValue);
        public static readonly TimelineId RollTimeline = new TimelineId(TimelineRollValue);
        public static readonly TimelineId MonkeyTimeline = new TimelineId(TimelineMonkeyValue);
        public static readonly TimelineId HomingTimeline = new TimelineId(TimelineHomingValue);
        public static readonly TimelineId BoomerangTimeline = new TimelineId(TimelineBoomerangValue);
        public static readonly TimelineId TeleportTimeline = new TimelineId(TimelineTeleportValue);
        public static readonly TimelineId GrenadeTimeline = new TimelineId(TimelineGrenadeValue);
        public static readonly TimelineId BarrelTimeline = new TimelineId(TimelineBarrelValue);
        public static readonly TimelineId ReloadTimeline = new TimelineId(TimelineReloadValue);
        public static readonly TimelineId TimelineEnemy = new TimelineId(TimelineEnemyValue);

        public static readonly InputToken Fire1 = new InputToken("Fire1");
        public static readonly InputToken Fire2 = new InputToken("Fire2");
        public static readonly InputToken Fire3 = new InputToken("Fire3");
        public static readonly InputToken Fire4 = new InputToken("Fire4");
        public static readonly InputToken Fire5 = new InputToken("Fire5");
        public static readonly InputToken RollInput = new InputToken("Roll");
        public static readonly InputToken HomingInput = new InputToken("Homing");
        public static readonly InputToken MonkeyInput = new InputToken("Monkey");
    }

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

    public sealed class BuffArenaSkill
    {
        public SkillNodeId Id;
        public TimelineId Timeline;
        public InputToken Input;
        public int AmmoCost;
        public string AnimatorState;
    }

    public sealed class BuffArenaData
    {
        public TimelineLibrary Timelines = new TimelineLibrary();
        public ProjectileCatalog Projectiles = new ProjectileCatalog();
        public AoeCatalog Aoes = new AoeCatalog();
        public CueLibrary Cues = new CueLibrary();
        public MotorConfig Motor = MotorConfig.SeasonOneDefaults();
        public readonly List<BuffArenaSkill> Skills = new List<BuffArenaSkill>(9);

        public BuffArenaSkill RequireSkill(SkillNodeId id)
        {
            for (int i = 0; i < Skills.Count; i++)
                if (Skills[i].Id == id) return Skills[i];
            throw new InvalidOperationException("Missing Buff Arena skill " + id.Value);
        }
    }

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

        public override void Tick(float dt)
        {
            if (_input == null || _director == null || _loco == null ||
                Self.TryGetComp<TagComp>(out var tags) &&
                (tags.Has(CommonTags.Dead) || tags.Has(CommonTags.Stunned) || tags.Has(CommonTags.Downed)))
                return;

            if (_director.IsPlaying)
            {
                _input.Clear();
                return;
            }

            if (!_input.TryPeek(out var token)) return;
            _input.Consume();

            if (token == BuffArenaIds.Fire4 && TryTeleport())
                return;

            BuffArenaSkill skill = FindByInput(token);
            if (skill == null) return;

            if (skill.AmmoCost > 0 && (_ammo == null || !_ammo.TryConsume(skill.AmmoCost)))
            {
                if (skill.Id == BuffArenaIds.Fire)
                    _director.Play(BuffArenaIds.Reload, BuffArenaIds.ReloadTimeline);
                return;
            }

            if (!_director.Play(skill.Id, skill.Timeline) && skill.AmmoCost > 0)
                _ammo.Refill(skill.AmmoCost);
        }

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
                Self.World.Events.Publish(new EvGameplayMessage(Self.Id, "无法传送"));
                return true;
            }

            _loco.RequestTeleport(projectileTf.Position);
            Self.World.RequestDespawn(projectile.Id);
            projectile.SetActive(false);
            _tracker.ClearIf(projectile.Id);
            return true;
        }

        BuffArenaSkill FindByInput(InputToken token)
        {
            for (int i = 0; i < _data.Skills.Count; i++)
                if (_data.Skills[i].Input == token) return _data.Skills[i];
            return null;
        }
    }

    public sealed class BuffArenaShooterComp : Comp
    {
        readonly BuffArenaData _data;
        SkillDirectorComp _director;
        LocomotionComp _loco;
        EntityId _target = EntityId.Invalid;
        float _wanderTimer = 2f;
        float _fireTimer = 3f;
        float _wanderYaw;

        public override bool WantsTick => true;

        public BuffArenaShooterComp(BuffArenaData data) => _data = data ?? throw new ArgumentNullException(nameof(data));

        protected override void OnAttach()
        {
            _director = Self.GetComp<SkillDirectorComp>();
            _loco = Self.GetComp<LocomotionComp>();
            _wanderYaw = Self.GetComp<TransformComp>().YawDegrees;
        }

        protected override void OnDetach()
        {
            _director = null;
            _loco = null;
            _target = EntityId.Invalid;
        }

        public void SetTarget(EntityId target) => _target = target;

        public override void Tick(float dt)
        {
            if (_director == null || _loco == null || Self.World == null) return;
            if (Self.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead)) return;

            _wanderTimer -= dt;
            if (_wanderTimer <= 0f)
            {
                _wanderYaw += Range(-90f, 90f);
                _wanderTimer = Range(1.6f, 3.2f);
            }

            var move = LocomotionComp.ForwardFromYaw(_wanderYaw);
            _loco.RequestMoveIntent(move.X, move.Z);

            if (_target.IsValid && Self.World.TryGetActor(_target, out var target) && target != null &&
                target.TryGetComp<TransformComp>(out var targetTf))
            {
                var tf = Self.GetComp<TransformComp>();
                _loco.RequestAimYaw(YawTo(tf.Position, targetTf.Position));
            }

            _fireTimer -= dt;
            if (_fireTimer <= 0f && !_director.IsPlaying)
            {
                _director.Play(BuffArenaIds.SkillEnemy, BuffArenaIds.TimelineEnemy);
                _fireTimer = Range(2f, 5f);
            }
        }

        float Range(float min, float max) => min + (max - min) * Self.World.Random.Next01();

        static float YawTo(SimVec3 from, SimVec3 to)
        {
            return (float)(Math.Atan2(to.Z - from.Z, to.X - from.X) * 180.0 / Math.PI);
        }
    }

    public sealed class BarrelComp : Comp
    {
        EntityId _owner = EntityId.Invalid;
        float _damageTimer = 1f;
        bool _exploded;
        public override bool WantsTick => true;

        public void SetOwner(EntityId owner) => _owner = owner;

        public float FilterDamage(Actor source, float amount)
        {
            if (source != null && source.TryGetComp<BarrelComp>(out _))
                return amount;
            return Math.Min(1f, amount);
        }

        public override void Tick(float dt)
        {
            if (_exploded || Self.World == null) return;
            if (Self.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Dead)) return;
            _damageTimer -= dt;
            if (_damageTimer > 0f) return;
            _damageTimer += 1f;
            Self.World.Deliver(new IEffect[]
            {
                new DamageEffect { Flat = 1f, CanCrit = false, FireOnHurted = false, DirectDamage = true }
            }, Self, Self, 0f);
        }

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
            _damageTimer = 1f;
            _exploded = false;
        }
    }

    public sealed class PlayBuffArenaCueEffect : IEffect
    {
        readonly int _cueId;
        readonly string _anchor;
        readonly string _key;
        readonly bool _target;
        readonly bool _point;
        readonly bool _loop;
        readonly bool _stop;

        public PlayBuffArenaCueEffect(int cueId, string anchor = "", string key = "", bool target = false,
            bool point = false, bool loop = false, bool stop = false)
        {
            _cueId = cueId;
            _anchor = anchor ?? string.Empty;
            _key = key ?? string.Empty;
            _target = target;
            _point = point;
            _loop = loop;
            _stop = stop;
        }

        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null) return;
            var source = ctx.Source != null ? ctx.Source.Id : EntityId.Invalid;
            var target = _target && ctx.Target != null ? ctx.Target.Id : EntityId.Invalid;
            ctx.World.Events.Publish(new EvCue(_cueId, source, "BuffArena", target, ctx.Point,
                _point && ctx.HasPoint, _anchor, _key, _loop, _stop));
        }
    }

    public sealed class HurtFeedbackEffect : IEffect
    {
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Target == null) return;
            if (ctx.Target.TryGetComp<AttributeSet>(out var attr) && attr.GetBase(AttrId.Hp) > 0f)
                ctx.World.Events.Publish(new EvHurt(ctx.Target.Id));
        }
    }

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

    public sealed class BarrelExplosionEffect : IEffect
    {
        public float Radius = 2.2f;
        public float DamageCoeff = .15f;

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
                else if (target.TryGetComp<TeamComp>(out var team) && team.TeamId == 2)
                {
                    ctx.World.Deliver(new IEffect[]
                    {
                        new DamageEffect { Coeff = DamageCoeff, CanCrit = true, CritMul = 1.8f,
                            CritChance = .05f, FireOnHurted = false, DirectDamage = true },
                        new HurtFeedbackEffect(),
                        new PlayBuffArenaCueEffect(BuffArenaIds.CueHitValue, "Body", target: true)
                    }, source, target, ctx.SnapshotAtk, point);
                }
            }
        }
    }

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

    public sealed class SpawnBuffArenaBarrelEffect : IEffect
    {
        public void Apply(ref EffectContext ctx)
        {
            if (ctx.World == null || ctx.Source == null || !ctx.Source.TryGetComp<TransformComp>(out var sourceTf)) return;
            var id = ctx.World.SpawnActor(new ActorSpawnSpec("buff_barrel"), publishSpawn: false);
            if (!ctx.World.TryGetActor(id, out var barrel) || barrel == null) return;
            var fwd = LocomotionComp.ForwardFromYaw(sourceTf.YawDegrees);
            barrel.GetComp<TransformComp>().Position = new SimVec3(
                sourceTf.Position.X + fwd.X * .55f, sourceTf.Position.Y,
                sourceTf.Position.Z + fwd.Z * .55f);
            barrel.GetComp<TransformComp>().YawDegrees = sourceTf.YawDegrees;
            var attr = barrel.GetComp<AttributeSet>();
            attr.SetBase(AttrId.MaxHp, 5f);
            attr.SetBase(AttrId.Hp, 5f);
            attr.SetBase(AttrId.Atk, 0f);
            attr.SetBase(AttrId.MoveSpeed, 0f);
            barrel.GetComp<BarrelComp>().SetOwner(ctx.Source.Id);
            ctx.World.PublishSpawn(id, "buff_barrel", "buff_barrel_view");
        }
    }

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
                case "buff_player":
                    return Combatant("buff_player", 1, .25f, 60, true);
                case "buff_enemy":
                    return Combatant("buff_enemy", 2, .25f, 0, false);
                case "buff_barrel":
                    var barrel = Combatant("buff_barrel", 0, .25f, 0, false);
                    barrel.AddComp(new BarrelComp());
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

        Actor Combatant(string blueprint, int team, float radius, int ammoCapacity, bool player)
        {
            var actor = new Actor();
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(new AttributeSet());
            actor.AddComp(new BuffComp());
            actor.AddComp(new TeamComp(team));
            actor.AddComp(new HealthComp());
            actor.AddComp(new StateMachineComp());
            actor.AddComp(new CharacterRadiusComp(radius));
            actor.AddComp(new LocomotionComp());
            actor.AddComp(new HitboxComp());
            actor.AddComp(new SkillDirectorComp(_data.Timelines));
            if (ammoCapacity > 0) actor.AddComp(new AmmoComp(ammoCapacity));

            if (player)
            {
                actor.AddComp(new InputBufferComp());
                actor.AddComp(new ProjectileTrackerComp());
                actor.AddComp(new BuffArenaPlayerComp(_data));
            }
            else if (blueprint == "buff_enemy")
            {
                actor.AddComp(new BuffArenaShooterComp(_data));
            }

            actor.GetComp<AttributeSet>().InitFighterDefaults();
            return actor;
        }

        public void Release(Actor actor) => actor?.ResetForPool();
    }

    public static class BuffArenaContent
    {
        public static BuffArenaData Build()
        {
            var data = new BuffArenaData();
            RegisterCues(data);
            RegisterProjectiles(data);
            RegisterAoes(data);
            RegisterTimelines(data);
            RegisterSkills(data);
            return data;
        }

        static void RegisterCues(BuffArenaData data)
        {
            RegisterCue(data, BuffArenaIds.CueMuzzleValue, "fx_muzzle", "MuzzleFlash");
            RegisterCue(data, BuffArenaIds.CueHeartValue, "fx_heart", "Heart");
            RegisterCue(data, BuffArenaIds.CueRollFireValue, "fx_roll_fire", "Fire_B");
            RegisterCue(data, BuffArenaIds.CueHitValue, "fx_hit", "HitEffect_A");
            RegisterCue(data, BuffArenaIds.CueShieldValue, "fx_shield", "HitEffect_B");
            RegisterCue(data, BuffArenaIds.CueExplosionValue, "fx_explosion", "Explosion_A");
            RegisterCue(data, BuffArenaIds.CueStarValue, "fx_star", "Star_B");
            RegisterCue(data, BuffArenaIds.CueShockwaveValue, "fx_shockwave", "ShockWave");
        }

        static void RegisterCue(BuffArenaData data, int id, string prefabKey, string name)
        {
            data.Cues.Register(new CueDef { CueId = id, PrefabKey = prefabKey, SfxKey = name, LifeTime = .8f });
        }

        static void RegisterProjectiles(BuffArenaData data)
        {
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileNormalValue,
                Speed = 6f,
                Lifetime = 10f,
                HitRadius = .1f,
                MaxHits = 1,
                SameTargetDelay = .1f,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_normal_view",
                OnHit = DamageBag(1f, true),
                OnExpire = CueBag(BuffArenaIds.CueHitValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileEnemyValue,
                Speed = 6f,
                Lifetime = 10f,
                HitRadius = .1f,
                MaxHits = 1,
                SameTargetDelay = .1f,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_enemy_view",
                OnHit = DamageBag(1f, true),
                OnExpire = CueBag(BuffArenaIds.CueHitValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileHomingValue,
                Speed = 3f,
                Lifetime = 100f,
                HitRadius = .1f,
                MaxHits = 1,
                SameTargetDelay = .1f,
                HomingRate = 36000f,
                HomingAcquireRadius = 14f,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_normal_view",
                OnHit = DamageBag(1f, true),
                OnExpire = CueBag(BuffArenaIds.CueHitValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileBoomerangValue,
                Speed = 5f,
                Lifetime = 10f,
                HitRadius = .5f,
                MaxHits = 99999,
                SameTargetDelay = .5f,
                Motion = ProjectileMotionKind.ReturnToOwner,
                MotionParam = 1f,
                HitOwnerOnReturn = true,
                RemoveOnObstacle = false,
                ViewBlueprintId = "buff_projectile_boomerang_view",
                OnHit = DamageBag(1f, true),
                OnOwnerHit = CueBag(BuffArenaIds.CueHeartValue, "Body")
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileTeleportValue,
                Speed = 6f,
                Lifetime = 3f,
                HitRadius = .1f,
                MaxHits = 1,
                Motion = ProjectileMotionKind.Accelerate,
                MotionParam = 5f,
                TrackOwner = true,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_teleport_view",
                OnHit = DamageBag(.6f, false),
                OnExpire = CueBag(BuffArenaIds.CueStarValue)
            });
            data.Projectiles.Register(new ProjectileDefinition
            {
                SpecId = BuffArenaIds.ProjectileBombValue,
                Speed = 3f,
                Lifetime = 2f,
                HitRadius = .1f,
                MaxHits = 1,
                RemoveOnObstacle = true,
                ViewBlueprintId = "buff_projectile_bomb_view",
                OnHit = new IEffect[] { new SpawnAoeEffect(BuffArenaIds.AoeExplosionValue, false, 1.5f, .02f) },
                OnExpire = new IEffect[] { new SpawnAoeEffect(BuffArenaIds.AoeStayingBombValue, false, .1f, 3f) },
                OnObstacle = new IEffect[] { new SpawnAoeEffect(BuffArenaIds.AoeExplosionValue, false, 1.5f, .02f) }
            });
        }

        static void RegisterAoes(BuffArenaData data)
        {
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeShieldValue,
                Radius = 1.5f,
                Duration = 0f,
                TrackProjectiles = true,
                ProjectileAbsorbForce = 0f,
                ViewBlueprintId = "buff_aoe_shield_view"
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeMonkeyValue,
                Radius = .25f,
                Duration = 100f,
                Motion = AoeMotionKind.Forward,
                MoveSpeed = .1f,
                RemoveOnObstacle = true,
                TrackOccupancy = true,
                TrackProjectiles = true,
                ProjectileAbsorbForce = .05f,
                ProjectileRadiusScale = .05f,
                ViewBlueprintId = "buff_aoe_monkey_view",
                OnEnter = DamageBag(.2f, true)
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeBlackHoleValue,
                Radius = 2f,
                Duration = 1f,
                PulseInterval = .02f,
                TrackOccupancy = true,
                ViewBlueprintId = "buff_aoe_blackhole_view",
                OnStay = new IEffect[] { new PullToPointEffect() }
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeExplosionValue,
                Radius = 1.5f,
                Duration = .02f,
                PulseOnSpawn = true,
                OnPulse = new IEffect[]
                {
                    new DamageEffect { Coeff = .1f, CanCrit = true, CritMul = 1.8f,
                        CritChance = .05f, FireOnHurted = false, DirectDamage = true },
                    new HurtFeedbackEffect(),
                    new PlayBuffArenaCueEffect(BuffArenaIds.CueHitValue, "Body", target: true)
                },
                OnExpire = CueBag(BuffArenaIds.CueExplosionValue)
            });
            data.Aoes.Register(new AoeDefinition
            {
                SpecId = BuffArenaIds.AoeStayingBombValue,
                Radius = .1f,
                Duration = 3f,
                ViewBlueprintId = "buff_aoe_stayingbomb_view",
                OnExpire = new IEffect[]
                {
                    new SpawnAoeEffect(BuffArenaIds.AoeExplosionValue, false, 1.5f, .02f)
                }
            });
        }

        static void RegisterTimelines(BuffArenaData data)
        {
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineFireValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileNormalValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineReloadValue, 1.15f, "Reload", true, true,
                Payload(1.1f, new RefillAmmoEffect(60))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineBoomerangValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueHeartValue, "Head"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileBoomerangValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineTeleportValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileTeleportValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineHomingValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnProjectileEffect(BuffArenaIds.ProjectileHomingValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineGrenadeValue, .5f, "Fire", true, true,
                Payload(.1f, new SpawnProjectileEffect(BuffArenaIds.ProjectileBombValue))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineBarrelValue, .5f, "Fire", true, true,
                Payload(.1f, new SpawnBuffArenaBarrelEffect())));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineMonkeyValue, .5f, "Fire", true, true,
                Payload(.1f, new PlayBuffArenaCueEffect(BuffArenaIds.CueMuzzleValue, "Muzzle"),
                    new SpawnAoeEffect(BuffArenaIds.AoeMonkeyValue, false, .25f, 100f, .5f))));
            data.Timelines.Register(Timeline(BuffArenaIds.TimelineEnemyValue, .5f, "Fire", true, true,
                Payload(.1f, new SpawnProjectileEffect(BuffArenaIds.ProjectileEnemyValue))));

            var roll = Timeline(BuffArenaIds.TimelineRollValue, .9f, "Roll", false, false,
                Payload(0f, new PlayBuffArenaCueEffect(BuffArenaIds.CueRollFireValue, "Body", "roll_fire", false, false, true)),
                Payload(.8f, new PlayBuffArenaCueEffect(BuffArenaIds.CueRollFireValue, "Body", "roll_fire", false, false, false, true),
                    new PlayBuffArenaCueEffect(BuffArenaIds.CueShockwaveValue, "Body")));
            roll.Clips = new[]
            {
                new TimelineClip { Start = .1f, End = .8f, Kind = ClipKind.IFrame },
                new TimelineClip { Start = .2f, End = .7f, Kind = ClipKind.Move, MoveX = .4f }
            };
            data.Timelines.Register(roll);
        }

        static void RegisterSkills(BuffArenaData data)
        {
            AddSkill(data, BuffArenaIds.Fire, BuffArenaIds.FireTimeline, BuffArenaIds.Fire1, 1, "Fire");
            AddSkill(data, BuffArenaIds.Roll, BuffArenaIds.RollTimeline, BuffArenaIds.RollInput, 0, "Roll");
            AddSkill(data, BuffArenaIds.Monkey, BuffArenaIds.MonkeyTimeline, BuffArenaIds.MonkeyInput, 3, "Fire");
            AddSkill(data, BuffArenaIds.Homing, BuffArenaIds.HomingTimeline, BuffArenaIds.HomingInput, 2, "Fire");
            AddSkill(data, BuffArenaIds.Boomerang, BuffArenaIds.BoomerangTimeline, BuffArenaIds.Fire2, 0, "Fire");
            AddSkill(data, BuffArenaIds.Teleport, BuffArenaIds.TeleportTimeline, BuffArenaIds.Fire4, 0, "Fire");
            AddSkill(data, BuffArenaIds.Grenade, BuffArenaIds.GrenadeTimeline, BuffArenaIds.Fire3, 0, "Fire");
            AddSkill(data, BuffArenaIds.Barrel, BuffArenaIds.BarrelTimeline, BuffArenaIds.Fire5, 0, "Fire");
            AddSkill(data, BuffArenaIds.Reload, BuffArenaIds.ReloadTimeline, new InputToken(""), 0, "Reload");
            AddSkill(data, BuffArenaIds.SkillEnemy, BuffArenaIds.TimelineEnemy, new InputToken(""), 0, "Fire");
        }

        static void AddSkill(BuffArenaData data, SkillNodeId id, TimelineId timeline, InputToken input,
            int ammoCost, string animation)
        {
            data.Skills.Add(new BuffArenaSkill
            {
                Id = id,
                Timeline = timeline,
                Input = input,
                AmmoCost = ammoCost,
                AnimatorState = animation
            });
        }

        static TimelineSO Timeline(int id, float duration, string animation, bool allowMove, bool allowRotate,
            params TimelinePayload[] payloads)
        {
            return new TimelineSO
            {
                Id = new TimelineId(id),
                Duration = duration,
                AnimatorState = animation,
                AllowMove = allowMove,
                AllowRotate = allowRotate,
                Payloads = payloads ?? Array.Empty<TimelinePayload>(),
                Clips = Array.Empty<TimelineClip>()
            };
        }

        static TimelinePayload Payload(float time, params IEffect[] effects)
        {
            return new TimelinePayload { Time = time, Effects = effects ?? Array.Empty<IEffect>() };
        }

        static IEffect[] DamageBag(float coefficient, bool canCrit)
        {
            return new IEffect[]
            {
                new DamageEffect { Coeff = coefficient, CanCrit = canCrit, CritMul = 1.8f,
                    CritChance = canCrit ? .05f : 0f, FireOnHurted = false, DirectDamage = true },
                new HurtFeedbackEffect(),
                new PlayBuffArenaCueEffect(BuffArenaIds.CueHitValue, "Body", target: true)
            };
        }

        static IEffect[] CueBag(int cueId, string anchor = "")
        {
            return new IEffect[] { new PlayBuffArenaCueEffect(cueId, anchor, point: anchor.Length == 0) };
        }
    }

}
