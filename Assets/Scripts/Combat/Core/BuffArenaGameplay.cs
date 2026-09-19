using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public static class BuffArenaIds
    {
        // Blueprint ids are shared identity, not tuning: the content table, the actor
        // factory and the session all have to agree on them.
        public const string PlayerBlueprint = "buff_player";
        public const string EnemyBlueprint = "buff_enemy";
        public const string BarrelBlueprint = "buff_barrel";

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
        // Warp counterpart of the teleport bullet, resolved while its projectile is alive.
        public const int SkillTeleportWarpValue = 2011;

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
        public static readonly SkillNodeId TeleportWarp = new SkillNodeId(SkillTeleportWarpValue);

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
        // Teleport bullet: while a tracked projectile exists the skill resolves to
        // WarpSkillId instead of casting itself again.
        public bool RequiresTrackedProjectile;
        public SkillNodeId WarpSkillId = SkillNodeId.None;
    }

    /// <summary>
    /// How the runtime treats a missing or incomplete baked content database. The Unity
    /// runtime is supposed to run on its ScriptableObject assets, so SoStrict is the target
    /// state; SoWithWarnings exists only so a build stays runnable while assets are authored.
    /// </summary>
    public enum ContentSourcePolicy : byte
    {
        SoWithWarnings = 0,
        SoStrict = 1
    }

    public sealed class BuffArenaSourceConfig
    {
        public string[] SkillIds = Array.Empty<string>();
        public string[] ProjectileIds = Array.Empty<string>();
        public string[] AoeIds = Array.Empty<string>();
        public int PlayerMaxHp = 500;
        public int PlayerAmmoCapacity = 60;
        public int MaxEnemies = 10;
        public float SpawnPeriod = 10f;
        public float EnemyCleanupDelay = 5f;
        public float BarrelSelfDamagePeriod = 5f;
        public MotorConfig Motor = MotorConfig.SeasonOneDefaults();
        public int Seed = 1;
        public BuffArenaActorDef[] Actors = Array.Empty<BuffArenaActorDef>();
        public ContentSourcePolicy Policy = ContentSourcePolicy.SoWithWarnings;
        // Pre-baked database produced from ScriptableObject assets. Null in the pure C#
        // path, where the code-defined table below is registered instead.
        public BuffArenaData Content;
        /// Explains why Content is null. Set by BuffArenaDatabaseAsset.BakeContent().
        public string ContentError;
    }

    public sealed class BuffArenaData
    {
        public TimelineLibrary Timelines = new TimelineLibrary();
        public ProjectileCatalog Projectiles = new ProjectileCatalog();
        public AoeCatalog Aoes = new AoeCatalog();
        public CueLibrary Cues = new CueLibrary();
        public MotorConfig Motor = MotorConfig.SeasonOneDefaults();
        public readonly List<BuffArenaSkill> Skills = new List<BuffArenaSkill>(9);
        public int PlayerMaxHp = 500;
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

            // Source skill timelines gate on canUseSkill via SetCasterControlState. Only
            // drop buffered input while the running timeline still forbids casting.
            if (_director.IsPlaying && !_director.AllowsSkill)
            {
                _input.Clear();
                return;
            }
            if (_director.IsPlaying) return;

            if (!_input.TryPeek(out var token)) return;
            _input.Consume();

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
                Self.World.Events.Publish(new EvGameplayMessage(Self.Id, "Teleport blocked"));
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

    public sealed class BarrelComp : Comp
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
            _damageTimer += _selfDamagePeriod;
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
            _damageTimer = _selfDamagePeriod;
            _exploded = false;
        }
    }

    public sealed class PlayBuffArenaCueEffect : IEffect
    {
        // Field names follow the "_<AssetFieldName>" convention so BuffArenaDatabaseBuilder
        // can copy this configuration into PlayBuffArenaCueAsset by reflection.
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
            BuffArenaContentTables.Populate(data);
            return data;
        }

        public static BuffArenaData Build(BuffArenaSourceConfig source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            ValidateSource(source);
            if (source.Content == null)
            {
                string reason = string.IsNullOrEmpty(source.ContentError)
                    ? "the generated database is incomplete"
                    : source.ContentError;
                if (source.Policy == ContentSourcePolicy.SoStrict)
                    throw new InvalidOperationException(
                        "Buff Arena is configured to run on ScriptableObject content, but " + reason
                        + ". Rebuild or finish the assets under Assets/Combat/Config/Generated, "
                        + "or set the content policy back to SoWithWarnings.");
                CombatLog.Error(
                    "Buff Arena fell back to the code-defined table: " + reason
                    + ". The Unity runtime is supposed to run on ScriptableObject content.");
            }
            // Asset-driven path: the ScriptableObject database already carries every
            // definition, so nothing is registered from code here.
            var data = source.Content ?? Build();
            data.PlayerMaxHp = source.PlayerMaxHp;
            data.PlayerAmmoCapacity = source.PlayerAmmoCapacity;
            data.MaxEnemies = source.MaxEnemies;
            data.SpawnPeriod = source.SpawnPeriod;
            data.EnemyCleanupDelay = source.EnemyCleanupDelay;
            data.BarrelSelfDamagePeriod = source.BarrelSelfDamagePeriod;
            data.Motor = source.Motor;
            data.Seed = source.Seed;
            data.Actors.Clear();
            if (source.Actors != null)
                for (int i = 0; i < source.Actors.Length; i++)
                    if (source.Actors[i] != null) data.Actors.Add(source.Actors[i]);
            return data;
        }

        static void ValidateSource(BuffArenaSourceConfig source)
        {
            RequireSet("skills", source.SkillIds, "fire", "roll", "spaceMonkeyBall", "homingMissle",
                "cloakBoomerang", "teleportBullet", "grenade", "explosiveBarrel", "reload");
            RequireSet("projectiles", source.ProjectileIds, "normal0", "normal1", "cloakBoomerang",
                "teleportBullet", "boomball");
            RequireSet("aoes", source.AoeIds, "BulletShield", "SpaceMonkeyBall", "BlackHole",
                "BoomExplosive", "StayingBoom");
            if (source.PlayerMaxHp <= 0 || source.PlayerAmmoCapacity <= 0 || source.MaxEnemies <= 0 ||
                source.SpawnPeriod <= 0f || source.EnemyCleanupDelay <= 0f || source.BarrelSelfDamagePeriod <= 0f)
                throw new InvalidOperationException("Buff Arena source database contains invalid runtime settings.");
        }

        static void RequireSet(string name, string[] actual, params string[] expected)
        {
            if (actual == null || actual.Length != expected.Length)
                throw new InvalidOperationException("Buff Arena source database has invalid " + name + " entries.");
            var values = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < expected.Length; i++)
                if (!values.Contains(expected[i]))
                    throw new InvalidOperationException("Buff Arena source database is missing " + name + " entry " + expected[i] + ".");
        }

        static void RegisterContent(BuffArenaData data)
        {
            // The value table lives in BuffArenaContentTables so this file only owns
            // the runtime plumbing. The generated ScriptableObject assets are the
            // day-to-day tuning surface; this table seeds them and backs the pure C#
            // regression suite.
            BuffArenaContentTables.Populate(data);
        }
    }
}
