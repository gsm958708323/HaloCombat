using System;
using Combat.Core;
using Combat.Presentation;

namespace Combat.Game
{
    public sealed class ArenaSession : IDisposable
    {
        public CombatWorld World { get; private set; }
        public PresentHub Hub { get; private set; }
        public EntityId LocalPlayerId { get; private set; }
        public bool Paused => _pause.Paused;
        public bool PlayFrozen { get; private set; }
        public bool AllowsGameplayInput => !Paused && !PlayFrozen && World != null;
        public bool Finished { get; private set; }
        public bool PlayerWin { get; private set; }
        public int Frame => World == null ? 0 : World.Time.Frame;
        public LogicTicker Ticker => _ticker;
        readonly PauseStack _pause = new PauseStack();
        readonly LogicTicker _ticker = new LogicTicker();
        readonly WinWatcher _win = new WinWatcher();
        readonly SpawnTable _spawns;
        Action<EvEntityDead> _dead;
        Action<EvEntityCleanup> _cleanup;
        Action<EvHitstop> _hitstop;
        bool _disposed;

        public ArenaSession(CombatWorld world, PresentHub hub, SpawnTable spawns)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            Hub = hub ?? throw new ArgumentNullException(nameof(hub));
            _spawns = spawns ?? new SpawnTable();
        }

        public static ArenaSession StartHeadless()
        {
            var baked = new CodeCombatContent().Bake();
            var world = new CombatWorld(
                new FighterActorFactory(baked),
                new IntentQueue(),
                new EventBus(),
                new CombatTime(),
                new FixedRandom(0f),
                baked.Cues,
                baked.Motor
            );
            baked.Install(world);
            var s = new ArenaSession(world, new PresentHub(), DefaultArenaSpawns());
            s.Start();
            return s;
        }

        public static SpawnTable DefaultArenaSpawns() =>
            new SpawnTable
            {
                Entries = new[]
                {
                    new SpawnEntry { BlueprintId = "fighter", IsLocalPlayer = true },
                    new SpawnEntry
                    {
                        BlueprintId = "melee_guard",
                        Position = new SimVec3(2.2f, 0, 0),
                        YawDegrees = 180,
                        CountsForWin = true,
                        LeashOverride = 8,
                    },
                    new SpawnEntry { BlueprintId = "stake", Position = new SimVec3(.55f, 0, 0) },
                    new SpawnEntry { BlueprintId = "stake", Position = new SimVec3(3, 0, 2.2f) },
                },
            };

        public void Start()
        {
            Hub.SetWorld(World);
            Hub.BindBus(World.Events);
            _dead = OnDead;
            _cleanup = Hub.OnCleanup;
            _hitstop = OnHitstop;
            World.Events.Subscribe(_dead);
            World.Events.Subscribe(_cleanup);
            World.Events.Subscribe(_hitstop);
            var es = _spawns.Entries ?? Array.Empty<SpawnEntry>();
            for (int i = 0; i < es.Length; i++)
                SpawnOne(es[i]);
        }

        void SpawnOne(SpawnEntry e)
        {
            var id = World.SpawnActor(
                new ActorSpawnSpec(string.IsNullOrEmpty(e.BlueprintId) ? "stake" : e.BlueprintId)
            );
            if (!World.TryGetActor(id, out var a) || a == null)
                return;
            if (a.TryGetComp<TransformComp>(out var tf))
            {
                tf.Position = e.Position;
                tf.YawDegrees = e.YawDegrees;
            }
            if (a.TryGetComp<BehaviorTreeComp>(out var bt))
            {
                bt.Board.Home = e.Position;
                if (e.LeashOverride > 0)
                    bt.Board.LeashRange = e.LeashOverride;
                if (e.PatrolOverride > 0)
                    bt.Board.PatrolRadius = e.PatrolOverride;
            }
            Hub.BindSpawn(id, e.BlueprintId);
            if (e.IsLocalPlayer)
            {
                LocalPlayerId = id;
                Hub.SetLocalPlayer(id);
                _win.SetPlayer(id);
            }
            if (e.CountsForWin)
                _win.RegisterGuard(id);
        }

        public void TogglePause()
        {
            if (PlayFrozen)
                return;
            if (Paused)
                _pause.Pop();
            else
                _pause.Push();
            Hub.SetPaused(Paused);
        }

        public void PumpLogic(float dt)
        {
            if (_disposed || PlayFrozen || Paused)
                return;
            _ticker.Accumulate(dt, TickLogic);
        }

        public void PumpPresent(float dt)
        {
            if (_disposed || World == null)
                return;
            Hub.LateUpdate(World, dt, _ticker.RenderLogicTime(World.Time.Time));
            Hub.PumpUnscaled(dt < 0 ? 0 : dt);
        }

        void TickLogic(float step)
        {
            World.Tick(step);
            Hub.AfterLogicTick(World);
            if (!Finished && _win.Settled)
            {
                Finished = true;
                PlayerWin = _win.PlayerWin;
                PlayFrozen = true;
            }
        }

        void OnDead(EvEntityDead e)
        {
            _win.OnDead(e);
            Hub.OnDead(e);
        }

        void OnHitstop(EvHitstop e) => Hub.NotifyLocalHitstop();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            if (World != null)
            {
                if (_dead != null)
                    World.Events.Unsubscribe(_dead);
                if (_cleanup != null)
                    World.Events.Unsubscribe(_cleanup);
                if (_hitstop != null)
                    World.Events.Unsubscribe(_hitstop);
            }
            Hub?.UnbindBus();
            Hub?.ReleaseAll();
            World?.Shutdown();
            World = null;
            Hub = null;
            _dead = null;
            _cleanup = null;
            _hitstop = null;
        }
    }
}
