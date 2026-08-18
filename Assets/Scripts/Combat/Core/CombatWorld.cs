using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public interface ICombatWorld
    {
        ICombatTime Time { get; }
        IEntityRegistry Registry { get; }
        IIntentQueue Intents { get; }
        IEventBus Events { get; }

        EntityId SpawnActor(in ActorSpawnSpec spec);
        bool TryGetActor(EntityId id, out Actor actor);
        void RequestDespawn(EntityId id);
        void Tick(float dt);
    }

    /// <summary>
    /// 帧序门面。本步仅跑：时间 → 本地 Tick →（预留服务空阶段）→ Despawn → 事件由调用方 Publish。
    /// 后续子系统往固定钩子填，不改 Tick 顺序语义。
    /// </summary>
    public sealed class CombatWorld : ICombatWorld
    {
        readonly CombatTime _time;
        readonly EntityRegistry _registry;
        readonly IntentQueue _intents;
        readonly EventBus _events;
        readonly List<Action> _servicePhase = new List<Action>(8);
        public ICombatTime Time => _time;
        public IEntityRegistry Registry => _registry;
        public IIntentQueue Intents => _intents;
        public IEventBus Events => _events;
        public CombatWorld(
            IActorFactory actorFactory,
            IntentQueue intents = null,
            EventBus events = null,
            CombatTime time = null)
        {
            _time = time ?? new CombatTime();
            _intents = intents ?? new IntentQueue();
            _events = events ?? new EventBus();
            _registry = new EntityRegistry(
                actorFactory ?? throw new ArgumentNullException(nameof(actorFactory)));
        }

        public EntityId SpawnActor(in ActorSpawnSpec spec) => _registry.Spawn(spec);

        public bool TryGetActor(EntityId id, out Actor actor) => _registry.TryGet(id, out actor);

        public void RequestDespawn(EntityId id) => _registry.RequestDespawn(id);

        /// <summary>
        /// 注册跨实体服务阶段（按添加顺序执行）。战斗 Service 后续在此挂上。
        /// </summary>
        public void AddServicePhase(Action phase)
        {
            if (phase == null)
                throw new ArgumentNullException(nameof(phase));
            _servicePhase.Add(phase);
        }

        public void Tick(float dt)
        {
            _time.Advance(dt);

            // 1) 本地：状态 / 连招 / 轴 / 只投递 Intent（后续）
            var actors = _registry.CopyActiveActors();
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] is Actor a)
                    a.TickAll(_time.Delta);
            }

            // 2) 跨实体服务固定阶段（本步可为空，Demo 会挂测试消费者）
            for (int i = 0; i < _servicePhase.Count; i++)
                _servicePhase[i]();

            // 3) 帧末销毁
            _registry.FlushDespawn();
        }
    }
}
