using System;
using System.Collections.Generic;
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
        readonly Func<string, ArenaSession> _create;
        readonly ISettingsStore _store;
        readonly string[] _playerClasses;
        public string SelectedPlayerClassId { get; private set; }
        float _delay;
        const float Hold = .6f;

        public GameFlow(
            IGameplayInputSource input,
            Func<string, ArenaSession> create,
            ISettingsStore store,
            string[] playerClasses,
            string defaultPlayerClassId)
        {
            _input = input ?? new NullInputSource();
            _create = create ?? throw new ArgumentNullException(nameof(create));
            _store = store ?? new MemorySettingsStore();
            _playerClasses = playerClasses ?? Array.Empty<string>();
            if (_playerClasses.Length == 0)
                throw new ArgumentException("At least one player class is required.", nameof(playerClasses));
            SelectedPlayerClassId = ContainsPlayerClass(defaultPlayerClassId)
                ? defaultPlayerClassId
                : _playerClasses[0];
            Settings = _store.Load() ?? new GameSettings();
            Settings.Apply();
            Router = new InputRouter(_input);
        }

        public IReadOnlyList<string> PlayerClasses => _playerClasses;

        public void SelectPlayerClass(string classId)
        {
            if (!ContainsPlayerClass(classId))
                throw new ArgumentException("Unknown player class " + classId, nameof(classId));
            SelectedPlayerClassId = classId;
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
            Session = _create(SelectedPlayerClassId);
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

        bool ContainsPlayerClass(string classId)
        {
            if (string.IsNullOrEmpty(classId)) return false;
            for (int i = 0; i < _playerClasses.Length; i++)
                if (string.Equals(_playerClasses[i], classId, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
