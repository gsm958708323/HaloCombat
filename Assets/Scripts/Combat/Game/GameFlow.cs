using System;
using Combat.Presentation;

namespace Combat.Game
{
    public enum FlowState : byte
    {
        Boot,
        Title,
        Loading,
        Arena,
        Result,
    }

    public sealed class GameFlow
    {
        public FlowState State { get; private set; } = FlowState.Title;
        public ArenaSession Session { get; private set; }
        public InputRouter Router { get; private set; }
        public GameSettings Settings { get; private set; }
        readonly IGameplayInputSource _input;
        readonly Func<ArenaSession> _create;
        readonly ISettingsStore _store;
        float _delay;
        const float Hold = .6f;

        public GameFlow(
            IGameplayInputSource i,
            Func<ArenaSession> c = null,
            ISettingsStore s = null
        )
        {
            _input = i ?? new NullInputSource();
            _create = c ?? ArenaSession.StartHeadless;
            _store = s ?? new MemorySettingsStore();
            Settings = _store.Load() ?? new GameSettings();
            Settings.Apply();
            Router = new InputRouter(_input);
        }

        public void SaveSettings()
        {
            Settings.Apply();
            _store.Save(Settings);
        }

        public void GoTitle()
        {
            DisposeSession();
            State = FlowState.Title;
            _delay = 0;
        }

        public void StartRun()
        {
            Settings.Apply();
            DisposeSession();
            State = FlowState.Loading;
            Session = _create();
            Router = new InputRouter(_input);
            State = FlowState.Arena;
        }

        public void TickUpdate(float dt)
        {
            if (State == FlowState.Arena && Session != null)
            {
                Router.Apply(Session);
                Session.PumpLogic(dt);
                if (Session.Finished)
                {
                    State = FlowState.Result;
                    _delay = Hold;
                }
            }
            else if (State == FlowState.Result)
                _delay -= dt;
        }

        public void TickLate(float dt) => Session?.PumpPresent(dt);

        public bool ResultReady => State == FlowState.Result && _delay <= 0;

        public void ConfirmResultRetry()
        {
            if (State == FlowState.Result)
                StartRun();
        }

        public void ConfirmResultTitle()
        {
            if (State == FlowState.Result)
                GoTitle();
        }

        void DisposeSession()
        {
            Session?.Dispose();
            Session = null;
        }
    }
}
