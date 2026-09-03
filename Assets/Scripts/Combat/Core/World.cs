using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class CombatWorld
    {
        readonly CombatTime _time;
        readonly EntityRegistry _registry;
        readonly IntentQueue _intents;
        readonly EventBus _events;
        readonly EffectPipeline _pipeline;
        readonly IRandom _random;
        readonly SimpleTargetQuery _query;
        readonly HitDetectService _hitDetect;
        readonly ProjectileService _projectilesSvc;
        readonly AoeService _aoe;
        ProjectileCatalog _projectiles = new ProjectileCatalog();
        AoeCatalog _aoes = new AoeCatalog();
        SummonCatalog _summons = new SummonCatalog();
        CueLibrary _cues;
        MotorConfig _motor;
        readonly List<Action> _servicePhase = new List<Action>(8);
        int _buffIds;
        int _hitstopPending;
        int _hitstopLeft;

        public CombatTime Time => _time;
        public IntentQueue Intents => _intents;
        public EventBus Events => _events;
        public IRandom Random => _random;
        public ITargetQuery Query => _query;
        public ProjectileCatalog Projectiles => _projectiles;
        public AoeCatalog Aoes => _aoes;
        public SummonCatalog Summons => _summons;
        public int HitstopLeft => _hitstopLeft;
        public bool InHitstop => _hitstopLeft > 0;
        public CueLibrary Cues => _cues;
        public MotorConfig Motor => _motor;

        public CombatWorld(
            IActorFactory actorFactory,
            IntentQueue intents = null,
            EventBus events = null,
            CombatTime time = null,
            IRandom random = null,
            CueLibrary cues = null,
            MotorConfig? motor = null)
        {
            _time = time ?? new CombatTime();
            _intents = intents ?? new IntentQueue();
            _events = events ?? new EventBus();
            _pipeline = new EffectPipeline();
            _random = random ?? new SeededRandom(1);
            _cues = cues ?? CueLibrary.DefaultCombat();
            _motor = motor ?? MotorConfig.SeasonOneDefaults();
            _query = new SimpleTargetQuery();
            _query.Bind(this);
            _registry = new EntityRegistry(actorFactory ?? throw new ArgumentNullException(nameof(actorFactory)), this);
            _hitDetect = new HitDetectService(this);
            _projectilesSvc = new ProjectileService(this);
            _aoe = new AoeService(this);
        }

        public int NextBuffInstanceId() => ++_buffIds;

        public void ReplaceCatalogs(ProjectileCatalog projectiles, AoeCatalog aoes, SummonCatalog summons)
        {
            if (projectiles != null) _projectiles = projectiles;
            if (aoes != null) _aoes = aoes;
            if (summons != null) _summons = summons;
        }

        public void ReplaceCues(CueLibrary cues)
        {
            if (cues != null) _cues = cues;
        }

        public EntityId SpawnActor(in ActorSpawnSpec spec, bool publishSpawn = true)
        {
            var id = _registry.Spawn(spec);
            if (publishSpawn)
                PublishSpawn(id, spec.BlueprintId);
            return id;
        }

        public void PublishSpawn(EntityId id, string blueprintId)
        {
            if (!TryGetActor(id, out var actor) || actor == null)
                return;
            var owner = EntityId.Invalid;
            if (actor.TryGetComp<ProjectileComp>(out var projectile) && projectile.OwnerId.IsValid)
                owner = projectile.OwnerId;
            else if (actor.TryGetComp<AoeComp>(out var aoe) && aoe.OwnerId.IsValid)
                owner = aoe.OwnerId;
            else if (actor.TryGetComp<SummonComp>(out var summon) && summon.OwnerId.IsValid)
                owner = summon.OwnerId;
            _events.Publish(new EvEntitySpawn(id, blueprintId, owner));
        }
        public bool TryGetActor(EntityId id, out Actor actor) => _registry.TryGet(id, out actor);
        public void RequestDespawn(EntityId id) => _registry.RequestDespawn(id);
        public void RequestHitstop(int frames)
        {
            if (frames > _hitstopPending) _hitstopPending = frames;
        }
        public List<Actor> RegistryActive() => _registry.CopyActiveActors();

        public void AddServicePhase(Action phase)
        {
            if (phase == null) throw new ArgumentNullException(nameof(phase));
            _servicePhase.Add(phase);
        }

        public void Deliver(
            IEffect[] effects,
            Actor source,
            Actor target,
            float snapshotAtk,
            SimVec3? point = null,
            SimVec3? dir = null,
            int buffStacks = 0)
        {
            if (effects == null || effects.Length == 0) return;
            var ctx = new EffectContext
            {
                World = this,
                Source = source,
                Target = target,
                SnapshotAtk = snapshotAtk,
                BuffStacks = buffStacks
            };
            if (point.HasValue) { ctx.Point = point.Value; ctx.HasPoint = true; }
            if (dir.HasValue) { ctx.Dir = dir.Value; ctx.HasDir = true; }
            _pipeline.Run(ref ctx, effects);
        }

        public void CleanupByOwner(EntityId owner)
        {
            if (!owner.IsValid) return;
            var actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (a.TryGetComp<ProjectileComp>(out var p) && p.OwnerId == owner)
                {
                    RequestDespawn(a.Id);
                    a.SetActive(false);
                }
                else if (a.TryGetComp<AoeComp>(out var ao) && ao.OwnerId == owner)
                {
                    // Occupancy OnExit is attributed to the AoE actor itself; the
                    // owner is only the lifetime/cleanup relationship.
                    _aoe.DespawnAoe(a, ao, null);
                }
                else if (a.TryGetComp<SummonComp>(out var summon) && summon.OwnerId == owner)
                {
                    // A summon may already have emitted projectiles or fields. Clean
                    // those descendants before removing the summon itself. Do not
                    // recurse through the summon branch itself.
                    CleanupRuntimeByOwner(a.Id);
                    RequestDespawn(a.Id);
                    a.SetActive(false);
                }
            }
        }

        void CleanupRuntimeByOwner(EntityId owner)
        {
            if (!owner.IsValid) return;
            var actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                var a = actors[i];
                if (a.TryGetComp<ProjectileComp>(out var p) && p.OwnerId == owner)
                {
                    RequestDespawn(a.Id);
                    a.SetActive(false);
                }
                else if (a.TryGetComp<AoeComp>(out var ao) && ao.OwnerId == owner)
                {
                    _aoe.DespawnAoe(a, ao, null);
                }
            }
        }

        public void Tick(float dt)
        {
            _time.Advance(dt);

            if (_hitstopLeft > 0)
            {
                _hitstopLeft--;
                _registry.FlushDespawn();
                return;
            }

            if (_hitstopPending > 0)
            {
                _hitstopLeft = _hitstopPending;
                _hitstopPending = 0;
                _hitstopLeft--;
                _registry.FlushDespawn();
                return;
            }

            var actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
                actors[i].TickAll(_time.Delta);

            for (int i = 0; i < _servicePhase.Count; i++)
                _servicePhase[i]();

            _projectilesSvc.Tick(_time.Delta);
            _hitDetect.Tick();

            _intents.Drain<ApplyEffectsIntent>(intent =>
            {
                TryGetActor(intent.SourceId, out var src);
                if (!TryGetActor(intent.TargetId, out var dst) || dst == null)
                    return;
                SimVec3? pt = intent.HasPoint ? intent.Point : (SimVec3?)null;
                Deliver(intent.Effects, src, dst, intent.SnapshotAtk, pt, null, intent.BuffStacks);
            });

            _aoe.Tick(_time.Delta);

            actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].TryGetComp<BuffComp>(out var buffs))
                    buffs.Tick(_time.Delta);
            }

            actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i].TryGetComp<LocomotionComp>(out var loco))
                    loco.Integrate(_time.Delta);
            }

            _registry.FlushDespawn();
        }
    }
}
