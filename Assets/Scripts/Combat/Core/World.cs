using System;
using System.Collections.Generic;

namespace Combat.Core
{
    /// <summary>
    /// 构造 CombatWorld 需要的一次性装配参数。目录、Cue、Motor、移动约束在构造函数里一次给全，
    /// 取代「构造后再 Replace」：漏装会在调用点暴露，而不是对局里表现为「技能放了但没有子弹」这种静默失败。
    /// 每个字段都可以留空，留空即用默认值。
    /// </summary>
    public struct WorldInstall
    {
        public IntentQueue Intents;
        public EventBus Events;
        public CombatTime Time;
        public IRandom Random;
        public CueLibrary Cues;
        public MotorConfig? Motor;
        public IMovementConstraint Movement;
        public ProjectileCatalog Projectiles;
        public AoeCatalog Aoes;
        public SummonCatalog Summons;
    }

    /// <summary>
    /// 一场对局的唯一世界：持有时间、实体表、意图队列、事件总线、效果管线，以及弹体 / AoE / 命中服务。
    /// 定义（目录、Cue、Motor）在构造时一次装配，之后不再替换；一帧的推进顺序见 <see cref="Tick"/>。
    /// </summary>
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
        readonly IMovementConstraint _movement;
        ProjectileCatalog _projectiles = new ProjectileCatalog();
        AoeCatalog _aoes = new AoeCatalog();
        SummonCatalog _summons = new SummonCatalog();
        CueLibrary _cues;
        MotorConfig _motor;
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
        public bool InHitstop { get; private set; }
        public bool IsActorStopped(Actor actor) => InHitstop || (actor != null && actor.Time.IsStopped);
        public bool AllowsHitstopSkip => InHitstop;
        public CueLibrary Cues => _cues;
        public MotorConfig Motor => _motor;
        public IMovementConstraint Movement => _movement;

        /// <summary>按装配参数构造世界。目录 / Cue / Motor / 移动约束在这里一次到位，之后不再替换。</summary>
        public CombatWorld(IActorFactory actorFactory, in WorldInstall install)
        {
            if (actorFactory == null) throw new ArgumentNullException(nameof(actorFactory));
            _time = install.Time ?? new CombatTime();
            _intents = install.Intents ?? new IntentQueue();
            _events = install.Events ?? new EventBus();
            _pipeline = new EffectPipeline();
            _random = install.Random ?? new SeededRandom(1);
            _cues = install.Cues ?? CueLibrary.DefaultCombat();
            _motor = install.Motor ?? MotorConfig.SeasonOneDefaults();
            _movement = install.Movement ?? new FreeMovementConstraint();
            if (install.Projectiles != null) _projectiles = install.Projectiles;
            if (install.Aoes != null) _aoes = install.Aoes;
            if (install.Summons != null) _summons = install.Summons;
            _query = new SimpleTargetQuery();
            _query.Bind(this);
            _registry = new EntityRegistry(actorFactory, this);
            _hitDetect = new HitDetectService(this);
            _projectilesSvc = new ProjectileService(this);
            _aoe = new AoeService(this);
        }

        public int NextBuffInstanceId() => ++_buffIds;

        /// <summary>清空实体与待处理意图。目录是只读的，不需要重置。</summary>
        public void Shutdown()
        {
            _registry.ClearAll();
            _intents.ClearAll();
        }

        public EntityId SpawnActor(in ActorSpawnSpec spec, bool publishSpawn = true)
        {
            var id = _registry.Spawn(spec);
            if (publishSpawn)
                PublishSpawn(id, spec.BlueprintId);
            return id;
        }

        public void PublishSpawn(EntityId id, string blueprintId, string viewBlueprintId = null)
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
            _events.Publish(new EvEntitySpawn(id, blueprintId, owner, viewBlueprintId));
        }
        public bool TryGetActor(EntityId id, out Actor actor) => _registry.TryGet(id, out actor);
        public void RequestDespawn(EntityId id) => _registry.RequestDespawn(id);
        /// <summary>申请顿帧：只取本次更大的值，顿帧期间逻辑帧暂停、wall time 继续。</summary>
        public void RequestHitstop(int frames)
        {
            if (frames > _hitstopPending) _hitstopPending = frames;
        }
        /// <summary>当前活跃实体的一份独立副本。演示与课程代码用；50Hz 热路径请用带 destination 的重载。</summary>
        public List<Actor> RegistryActive()
        {
            var result = new List<Actor>(64);
            _registry.CopyActiveActors(result);
            return result;
        }

        public void RegistryActive(List<Actor> destination) => _registry.CopyActiveActors(destination);

        /// <summary>唯一的结算入口：按数组顺序跑效果管线（无优先级、无短路）。</summary>
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

        /// <summary>所有者消失时清掉它生成的弹体 / AoE / 召唤物（召唤物会先清掉自己的后代）。</summary>
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

        /// <summary>
        /// 一个逻辑帧的固定顺序。每一段之间重新取一次活跃快照：上一段出生或失活的实体会在下一段生效，
        /// 而本段循环中途的结构变化不会改掉正在走的下标。
        ///   1. 组件 Tick（含时间轴推进；技能在这一段投出弹体生成意图）
        ///   2. 命中前位移积分（让判定用的位置与本帧已积分的位置一致）
        ///   3. 弹体移动与命中，然后近战 / 命中盒检测
        ///   4. 排空 ApplyEffectsIntent（唯一的结算路径）
        ///   5. 时间轴收尾 FlushTimeline
        ///   6. AoE 脉冲与占用差分（本帧进入火地的目标能在同一逻辑帧吃到结算）
        ///   7. Buff 周期
        ///   8. 命中后位移积分（击退、技能位移在这里生效）
        ///   9. 回收待销毁实体
        /// Hitstop 期间只推进 wall time、暂停逻辑帧，但仍然回收。
        /// </summary>
        public void Tick(float dt)
        {
            _time.AdvanceWall(dt);

            _hitstopLeft = Math.Max(_hitstopLeft, _hitstopPending);
            _hitstopPending = 0;
            InHitstop = _hitstopLeft > 0;
            if (InHitstop)
            {
                _time.PauseLogic();
                _hitstopLeft--;
                _registry.FlushDespawn();
                return;
            }

            _time.AdvanceLogic(dt);

            var actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++) actors[i].Time.BeginStep(_time.Delta);
            for (int i = 0; i < actors.Count; i++) actors[i].TickAll(actors[i].Time.Delta);

            // Apply regular and skill movement before hit detection so the displayed
            // hitbox and the position used for the actual query describe the same frame.
            actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].Time.IsStopped && actors[i].TryGetComp<LocomotionComp>(out var loco))
                    loco.IntegrateBeforeHitDetection(_time.Delta);
            }

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

            actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].Time.IsStopped && actors[i].TryGetComp<SkillDirectorComp>(out var director))
                    director.FlushTimeline();
            }

            _aoe.Tick(_time.Delta);

            actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].Time.IsStopped && actors[i].TryGetComp<BuffComp>(out var buffs))
                    buffs.Tick(actors[i].Time.Delta);
            }

            actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (!actors[i].Time.IsStopped && actors[i].TryGetComp<LocomotionComp>(out var loco))
                    loco.IntegrateAfterHitDetection(_time.Delta);
            }

            _registry.FlushDespawn();
        }
    }
}
