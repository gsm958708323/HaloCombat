using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Combat.Config;

namespace Combat.Unity
{
    // Disk I/O can wait for an in-progress host commit. Only completed reads are
    // examined on Unity's thread, where resource validation and world swaps belong.
    internal sealed class SkillLabPublicationMonitor : IDisposable
    {
        const float PollInterval = .25f;
        readonly string _directory;
        readonly string _projectPath;
        IReadOnlyDictionary<string, string> _observedSlots;
        Task<ReadResult<IReadOnlyDictionary<string, string>>> _slotRead;
        Task<ReadResult<RuntimeProjectLoader>> _defaultRead;
        int _startedReads;
        int _defaultRequest, _defaultReadRequest;
        float _nextReadTime;
        bool _refreshRequested, _retryRequested, _retryOnRead;
        bool _needsDefaults;
        string _readError, _slotError, _defaultError;

        public PublishedSkillSession Live { get; } = new PublishedSkillSession { ValidateViews = UnityActorResources.Validate };
        public int CompletedReads { get; private set; }
        public int Version { get; private set; }
        public bool UsingDefaultSkills { get; private set; }
        public bool WaitingForDefaults => _needsDefaults && _defaultRead != null;
        public string LastError
        {
            get
            {
                var publicationError = _readError ?? _slotError;
                if (publicationError == null) return _defaultError;
                return _defaultError == null ? publicationError : publicationError + "\n" + _defaultError;
            }
        }

        public SkillLabPublicationMonitor(string directory)
        {
            _directory = directory;
            _projectPath = Path.Combine(directory, "project.json");
        }

        // A refresh requested during an in-flight read waits for the next read,
        // which observes commits completed before the request.
        public int RequestRefresh(bool retryInvalid = false)
        {
            _refreshRequested = true;
            _retryRequested |= retryInvalid;
            return _startedReads + 1;
        }

        public void Poll(float now)
        {
            if (_slotRead != null && _slotRead.IsCompleted)
            {
                var result = _slotRead.GetAwaiter().GetResult();
                _slotRead = null;
                CompletedReads = _startedReads;
                ApplyRead(result);
            }
            if (_defaultRead != null && _defaultRead.IsCompleted)
            {
                var result = _defaultRead.GetAwaiter().GetResult();
                _defaultRead = null;
                if (_needsDefaults && _defaultReadRequest == _defaultRequest) ApplyDefaults(result);
            }
            if (_needsDefaults && _defaultRead == null && _defaultError == null)
            {
                var path = _projectPath;
                _defaultReadRequest = _defaultRequest;
                _defaultRead = ReadAsync(() => new RuntimeProjectLoader(path));
            }
            if (_slotRead == null && (_refreshRequested || now >= _nextReadTime))
            {
                _nextReadTime = now + PollInterval;
                _retryOnRead = _retryRequested;
                _refreshRequested = _retryRequested = false;
                ++_startedReads;
                var directory = _directory;
                _slotRead = ReadAsync(() => PublishedSkillSlots.Read(directory));
            }
        }

        void ApplyRead(ReadResult<IReadOnlyDictionary<string, string>> result)
        {
            _readError = result.Error == null ? null : _directory + ": " + result.Error;
            if (result.Error != null)
            {
                // An unreadable group cannot invalidate an already validated publication.
                NeedDefaults(Live.Simulation == null);
                if (_retryOnRead) _defaultError = null;
                return;
            }

            var slots = result.Value;
            bool changed = !SameSlots(_observedSlots, slots);
            bool retry = _retryOnRead && (_slotError != null || _defaultError != null);
            if (changed || retry) _defaultError = null;
            if (slots.Count == 0)
            {
                _slotError = null;
                NeedDefaults(!UsingDefaultSkills);
            }
            else
            {
                if (changed || retry) ApplyPublications(slots);
                NeedDefaults(Live.Simulation == null);
            }
            // Observed and active are separate. Bad bytes are remembered to avoid
            // recompiling them every poll, but never become the active version.
            _observedSlots = slots;
        }

        void ApplyPublications(IReadOnlyDictionary<string, string> slots)
        {
            try
            {
                Live.ReplaceForSkillLab(slots);
                UsingDefaultSkills = false;
                ++Version;
                _slotError = null;
            }
            catch (ContentValidationException error)
            {
                _slotError = _directory + ": " + error.Message;
            }
        }

        void ApplyDefaults(ReadResult<RuntimeProjectLoader> result)
        {
            _defaultError = result.Error;
            if (_defaultError == null)
            {
                try
                {
                    Live.ReplaceWithSkillLabDefaults(result.Value);
                    UsingDefaultSkills = true;
                    ++Version;
                }
                catch (ContentValidationException error) { _defaultError = error.Message; }
            }
            if (_defaultError != null) _defaultError = _projectPath + ": " + _defaultError;
            NeedDefaults(false);
        }

        void NeedDefaults(bool needed)
        {
            if (_needsDefaults == needed) return;
            _needsDefaults = needed;
            // A cleared group may request a new snapshot after an earlier fallback
            // was superseded by a valid publication. Discard that earlier result.
            if (needed) ++_defaultRequest;
        }

        static bool SameSlots(IReadOnlyDictionary<string, string> left, IReadOnlyDictionary<string, string> right)
        {
            if (left == null || left.Count != right.Count) return false;
            foreach (var slot in right)
                if (!left.TryGetValue(slot.Key, out var text) || !string.Equals(text, slot.Value, StringComparison.Ordinal))
                    return false;
            return true;
        }

        static Task<ReadResult<T>> ReadAsync<T>(Func<T> read) where T : class => Task.Run(() =>
        {
            try { return new ReadResult<T>(read(), null); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ContentValidationException)
            { return new ReadResult<T>(null, error.Message); }
        });

        readonly struct ReadResult<T> where T : class
        {
            public readonly T Value;
            public readonly string Error;

            public ReadResult(T value, string error)
            {
                Value = value;
                Error = error;
            }
        }

        public void Dispose() => Live.Dispose();
    }
}
