using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Demos
{
    public class InfraDemo : MonoBehaviour
    {
        public void Awake()
        {
            var time = new CombatTime();
            var intents = new IntentQueue();
            var events = new EventBus();
            var factory = new DemoActorFactory(intents);
            var world = new CombatWorld(factory, intents, events, time);

            events.Subscribe<PingHandledEvent>(e =>
            {
                print($"[F{e.Frame}] handled {e.Message} from {e.Source}");
            });

            // Service 阶段：先处理 Ping，再处理自毁 Intent（顺序有意固定）
            world.AddServicePhase(() =>
            {
                intents.Drain<PingIntent>(ping =>
                {
                    events.Publish(new PingHandledEvent(ping.Source, ping.Message, time.Frame));
                });
            });

            world.AddServicePhase(() =>
            {
                intents.Drain<DespawnSelfIntent>(req =>
                {
                    world.RequestDespawn(req.Target);
                    print($"[F{time.Frame}] despawn requested {req.Target}");
                });
            });

            var id = world.SpawnActor(new ActorSpawnSpec("pinger"));
            print($"spawned {id}, active={world.Registry.ActiveCount}");

            // 模拟约 2 秒，固定 0.1s 步长（确定性）
            for (int i = 0; i < 20; i++)
                world.Tick(0.1f);

            bool stillExists = world.TryGetActor(id, out _);
            print($"after ticks: exists={stillExists}, active={world.Registry.ActiveCount}");

            // generation 校验：旧 id 应失效
            var id2 = world.SpawnActor(new ActorSpawnSpec("pinger"));
            print($"respawn {id2}, oldValid={world.TryGetActor(id, out _)}, newValid={world.TryGetActor(id2, out _)}");
        }
    }

    readonly struct PingIntent
    {
        public readonly EntityId Source;
        public readonly string Message;
        public PingIntent(EntityId source, string message)
        {
            Source = source;
            Message = message;
        }
    }

    readonly struct DespawnSelfIntent
    {
        public readonly EntityId Target;
        public DespawnSelfIntent(EntityId target) => Target = target;
    }

    readonly struct PingHandledEvent
    {
        public readonly EntityId Source;
        public readonly string Message;
        public readonly int Frame;
        public PingHandledEvent(EntityId source, string message, int frame)
        {
            Source = source;
            Message = message;
            Frame = frame;
        }
    }

    sealed class PingEmitterComp : ITickComp
    {
        readonly IIntentQueue _intents;
        readonly float _interval;
        float _acc;
        IActor _self;
        int _shots;

        public PingEmitterComp(IIntentQueue intents, float interval)
        {
            _intents = intents ?? throw new ArgumentNullException(nameof(intents));
            _interval = interval;
        }

        public void OnAttach(IActor self) { _self = self; _acc = 0f; _shots = 0; }
        public void OnDetach() { _self = null; }

        public void Tick(float dt)
        {
            _acc += dt;
            if (_acc < _interval) return;
            _acc -= _interval;
            _shots++;

            _intents.Post(new PingIntent(_self.Id, $"ping-{_shots}"));
            if (_shots >= 3)
                _intents.Post(new DespawnSelfIntent(_self.Id));
        }
    }

    sealed class DemoActorFactory : IActorFactory
    {
        readonly IIntentQueue _intents;
        public DemoActorFactory(IIntentQueue intents) => _intents = intents;

        public IActor Create(in ActorSpawnSpec spec)
        {
            var actor = new Actor();
            actor.SetActive(true);
            if (spec.BlueprintId == "pinger")
                actor.AddComp(new PingEmitterComp(_intents, 0.5f));
            return actor;
        }

        public void Release(IActor actor)
        {
            if (actor is Actor a) a.ResetForPool();
        }
    }
}