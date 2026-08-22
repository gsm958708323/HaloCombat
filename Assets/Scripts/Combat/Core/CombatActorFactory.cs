using System;

namespace Combat.Core
{
    public struct PulseZoneSpawnContext
    {
        public bool IsValid;
        public EntityId Owner;
        public int OwnerTeam;
        public SimVec3 Position;
        public float Radius;
        public float Interval;
        public float Lifetime;
        public int AttackSpecValue;
        public int SourceSkillValue;
    }
    public sealed class CombatActorFactory : IActorFactory
    {
        readonly CombatTime _time;
        readonly ComboTableSO _combos;
        readonly TimelineLibrary _timelines;
        readonly EffectFactory _effects;
        readonly IntentQueue _intents;
        readonly ProjectileSpecLibrary _projSpecs;
        PulseZoneSpawnContext _pendingPulse;
        public void SetPendingPulseZone(in PulseZoneSpawnContext ctx) => _pendingPulse = ctx;

        // Spawn 投射物时由 Service 写入的临时上下文（避免万能 World 进 Comp）
        ProjectileSpawnContext _pendingProj;

        public CombatActorFactory(
            CombatTime time,
            ComboTableSO combos,
            TimelineLibrary timelines,
            EffectFactory effects,
            IntentQueue intents,
            ProjectileSpecLibrary projSpecs)
        {
            _time = time;
            _combos = combos;
            _timelines = timelines;
            _effects = effects;
            _intents = intents;
            _projSpecs = projSpecs;
        }

        public void SetPendingProjectile(in ProjectileSpawnContext ctx)
            => _pendingProj = ctx;

        public Actor Create(in ActorSpawnSpec spec)
        {
            var actor = new Actor();
            actor.SetActive(true);

            if (spec.BlueprintId == "projectile")
            {
                BuildProjectile(actor);
                return actor;
            }

            if (spec.BlueprintId == "dummy")
            {
                BuildDummy(actor);
                return actor;
            }
            if (spec.BlueprintId == "pulse_zone")
            {
                var ctx = _pendingPulse;
                _pendingPulse = default;
                if (!ctx.IsValid)
                    throw new InvalidOperationException("pulse_zone without context");
                actor.AddComp(new TransformComp());
                actor.AddComp(new PulseZoneComp(_intents));
                // 场地通常不可被弹道打；不挂 Hurtbox
                return actor;
            }

            // 默认 fighter
            BuildFighter(actor);
            return actor;
        }

        void BuildFighter(Actor actor)
        {
            var team = new TeamComp();
            team.SetTeam(0);
            actor.AddComp(team);
            var attr = new AttrComp();
            attr.Setup(15f, 2f, 100f);
            actor.AddComp(attr);
            actor.AddComp(new HealthComp());
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(new BuffComp());
            actor.AddComp(new InputBufferComp(_time));
            actor.AddComp(new StateMachineComp());
            actor.AddComp(new LocomotionComp(_time));
            actor.AddComp(new SkillDirectorComp(_timelines, _effects));
            actor.AddComp(new ComboComp(_combos));
            actor.AddComp(new PlayerCombatDriverComp());
            var hurt = new HurtboxComp();
            hurt.SetRadius(0.5f);
            hurt.CanBeHit = true;
            actor.AddComp(hurt);
        }
        void BuildDummy(Actor actor)
        {
            var team = new TeamComp();
            team.SetTeam(1);
            actor.AddComp(team);
            var attr = new AttrComp();
            attr.Setup(atk: 0f, def: 1f, maxHp: 30f);
            actor.AddComp(attr);
            actor.AddComp(new HealthComp());
            actor.AddComp(new TransformComp());
            actor.AddComp(new TagComp());
            actor.AddComp(new StateMachineComp());
            var hurt = new HurtboxComp();
            hurt.SetRadius(0.6f);
            hurt.CanBeHit = true;
            actor.AddComp(hurt);
        }

        void BuildProjectile(Actor actor)
        {
            var ctx = _pendingProj;
            _pendingProj = default;

            if (!ctx.IsValid)
                throw new InvalidOperationException("Projectile spawn without context");

            actor.AddComp(new TransformComp());
            actor.AddComp(new ProjectileMoveComp());
            actor.AddComp(new ProjectileLifetimeComp(_intents));
            actor.AddComp(new ProjectileHitRecordComp());
            actor.AddComp(new ProjectileContactComp());
            // Attach 后由 Service 调用 ProjectileSetup
            // 这里只挂组件；位姿/速度在 Spawn 后立刻 Setup
        }

        public void Release(Actor actor) => actor?.ResetForPool();
    }
}

