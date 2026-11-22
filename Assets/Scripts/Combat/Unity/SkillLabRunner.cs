using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Combat.Config;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    [DisallowMultipleComponent]
    public sealed class SkillLabRunner : MonoBehaviour
    {
        static readonly IReadOnlyDictionary<string, string> EmptySlots = new Dictionary<string, string>();
        readonly SkillLabHud _hud = new SkillLabHud();
        readonly SimulationClock _clock = new SimulationClock(SkillSimulation.Step, 12);
        readonly CombatControls.Sampler _input = new CombatControls.Sampler();

        [SerializeField, Tooltip("Optional isolated content directory containing project.json and the four publication files.")]
        string contentDirectoryOverride;

        SkillLabPublicationMonitor _publications;
        PublishedSkillSession _replay;
        int _displayedVersion, _replayBaseVersion, _refreshAfterRead;
        int _heldSlot = -1;
        Vector2 _aim;
        bool _resumeAutoAdvance;
        string _replayError;
        string _status = "正在读取试验技能…";

        PublishedSkillSession Session => _replay ?? _publications?.Live;
        SkillSimulation LiveSimulation => ReplayMode ? null : _publications?.Live.Simulation;
        public bool AutoAdvance { get; set; } = true;
        public bool Paused { get; set; }
        public string CurrentSlot { get; private set; }
        public string Revision => Session?.Revision;
        public string SkillId => CurrentSlot != null && SlotSkills.TryGetValue(CurrentSlot, out var skill) ? skill : null;
        public string LastLoadError => _replayError ?? _publications?.LastError;
        public int Generation { get; private set; }
        public SkillSimulationSnapshot Snapshot { get; private set; }
        public string ContentDirectory => string.IsNullOrWhiteSpace(contentDirectoryOverride)
            ? Path.Combine(Application.streamingAssetsPath, "Combat")
            : Path.GetFullPath(contentDirectoryOverride);
        public IReadOnlyDictionary<string, RuntimeViewDefinition> Views => Session?.Simulation?.Views;
        public IReadOnlyDictionary<string, string> SlotSkills => Session?.Slots ?? EmptySlots;
        public IReadOnlyDictionary<string, string> SlotRevisions => Session?.SlotRevisions ?? EmptySlots;
        public SkillAimPreview AimPreview { get; private set; }
        public bool PointerInWorld { get; private set; }
        public bool ShowRanges => _hud.Ranges;
        public bool ShowTrajectories => _hud.Trajectories;
        public bool ShowDamageNumbers => _hud.DamageNumbers;
        public bool HasDifferentSlotLayouts => !ReplayMode && Session?.HasDifferentSlotLayouts == true;
        public bool UsingDefaultSkills => !ReplayMode && _publications?.UsingDefaultSkills == true;
        public bool PendingPublication => ReplayMode && _publications?.Version > _replayBaseVersion;
        public bool ReplayMode => _replay != null;
        public bool ReplayExitPending => ReplayMode && _refreshAfterRead != 0;
        public SkillReplayComparison ReplayComparison { get; private set; }

        public void ConfigureContentDirectory(string directory)
        {
            if (_publications != null || ReplayMode)
                throw new InvalidOperationException("Configure the directory before SkillLab Start.");
            contentDirectoryOverride = Path.GetFullPath(directory);
        }

        void Start() => CheckPublications();

        // F5 requests a fresh read and resets a healthy training session. Automatic
        // loading follows the same path, so pressing F5 is optional.
        public bool ReloadPublished()
        {
            if (!Application.isPlaying) return false;
            return RequestRefresh(retryInvalid: true);
        }

        public bool ExitReplay() => ReplayMode && RequestRefresh(retryInvalid: true);

        bool RequestRefresh(bool retryInvalid)
        {
            if (_refreshAfterRead == 0)
                _refreshAfterRead = EnsureMonitor().RequestRefresh(retryInvalid);
            _replayError = null;
            return true;
        }

        SkillLabPublicationMonitor EnsureMonitor() =>
            _publications ?? (_publications = new SkillLabPublicationMonitor(ContentDirectory));

        public void CheckPublications()
        {
            if (!Application.isPlaying) return;
            var monitor = EnsureMonitor();
            monitor.Poll(Time.unscaledTime);
            bool refresh = _refreshAfterRead != 0 && monitor.CompletedReads >= _refreshAfterRead && !monitor.WaitingForDefaults;
            if (refresh) _refreshAfterRead = 0;
            if (ReplayMode)
            {
                if (refresh && monitor.Live.Simulation != null)
                {
                    if (monitor.Version == _replayBaseVersion) monitor.Live.RestartSkillLabSimulation();
                    _replay.Dispose();
                    _replay = null;
                    ReplayComparison = null;
                    AutoAdvance = _resumeAutoAdvance;
                    ActivateTraining();
                }
                return;
            }
            if (monitor.Live.Simulation == null) return;
            if (_displayedVersion != monitor.Version)
            {
                ActivateTraining();
            }
            else if (refresh && monitor.LastError == null)
            {
                monitor.Live.RestartSkillLabSimulation();
                ActivateTraining();
            }
        }

        void ActivateTraining()
        {
            _displayedVersion = _publications.Version;
            _replayError = null;
            ActivateSession();
            _status = UsingDefaultSkills
                ? "发布技能后自动载入"
                : "已载入 " + string.Join(" / ", SlotSkills.Keys) + " · " + Revision.Substring(0, 12);
        }

        void ActivateSession()
        {
            _clock.Reset();
            _input.Cancel();
            _heldSlot = -1;
            _aim = default;
            AimPreview = null;
            PointerInWorld = false;
            CurrentSlot = PublishedSkillSlots.Ordered.First(SlotSkills.ContainsKey);
            Snapshot = Session.Simulation.Snapshot();
            ++Generation;
        }

        public bool SelectSlot(string slot)
        {
            if (ReplayMode || slot == null || !SlotSkills.ContainsKey(slot)) return false;
            CurrentSlot = slot;
            RefreshAimPreview();
            return true;
        }

        internal string DisplaySkillName(string key) =>
            Session?.Simulation?.InspectAim(key, _aim.x, _aim.y)?.Name ?? key;

        public void TogglePause()
        {
            if (ReplayMode) return;
            Paused = !Paused;
            CancelInput();
            _clock.Reset();
        }

        public SkillCastResult CastSlot(string slot, float x, float z)
        {
            var simulation = LiveSimulation;
            if (simulation == null) return null;
            if (slot == null || !SlotSkills.ContainsKey(slot))
            {
                _status = "技能槽未配置，网页发布后将自动载入。";
                return null;
            }
            CurrentSlot = slot;
            var result = (SkillCastResult)simulation.Execute(new SkillSimulationCommand { Kind = "cast", SkillId = SkillId, X = x, Z = z });
            Snapshot = simulation.Snapshot();
            _status = result.Accepted ? "Cast " + SkillId : result.Reason;
            return result;
        }

        public void ReleaseCast()
        {
            ExecuteLive(new SkillSimulationCommand { Kind = "release" });
            _heldSlot = -1;
        }

        public void SetMovement(float x, float z) =>
            LiveSimulation?.Execute(new SkillSimulationCommand { Kind = "move", X = x, Z = z });

        public void PlaceActor(string sceneId, string entityId, float x, float z) =>
            ExecuteLive(new SkillSimulationCommand { Kind = "place", ActorId = sceneId, EntityId = entityId, X = x, Z = z });

        public void SubmitInput(string action, float x, float z) =>
            ExecuteLive(new SkillSimulationCommand { Kind = "input", Input = action, X = x, Z = z });

        public void RemoveEntity(string id) => ExecuteLive(new SkillSimulationCommand { Kind = "remove", EntityId = id });

        public bool RemoveBuff(string actorId, int instanceId) =>
            ExecuteLive(new SkillSimulationCommand { Kind = "remove-buff", ActorId = actorId, InstanceId = instanceId }) is bool result && result;

        public void Interrupt(bool death = false) =>
            ExecuteLive(new SkillSimulationCommand { Kind = death ? "death" : "interrupt" });

        object ExecuteLive(SkillSimulationCommand command)
        {
            var simulation = LiveSimulation;
            if (simulation == null) return null;
            var result = simulation.Execute(command);
            Snapshot = simulation.Snapshot();
            return result;
        }

        public void CancelInput()
        {
            _input.Cancel();
            ExecuteLive(new SkillSimulationCommand { Kind = "cancel" });
            _heldSlot = -1;
        }

        public void AdvanceSimulation(int steps)
        {
            var simulation = LiveSimulation;
            if (simulation == null) return;
            simulation.Advance(steps);
            Snapshot = simulation.Snapshot();
        }

        void Update()
        {
            bool refresh = Input.GetKeyDown(CombatControls.Reload) && ReloadPublished();
            int generation = Generation;
            CheckPublications();
            if (refresh || ReplayMode || Generation != generation) return;
            if (Input.GetKeyDown(CombatControls.Pause)) { TogglePause(); return; }

            UpdateAim();
            if (LiveSimulation != null) SampleSkills();
            if (Paused && Input.GetKeyDown(CombatControls.Step)) { AdvanceSimulation(1); return; }
            if (!AutoAdvance || Paused || LiveSimulation == null) return;
            SetMovement(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            int steps = _clock.BeginFrame(Time.unscaledDeltaTime * _hud.Rate);
            if (steps > 0) AdvanceSimulation(steps);
        }

        void SampleSkills()
        {
            for (int i = 0; i < CombatControls.MainSkillCount; i++)
            {
                var frame = _input.Read(i, PointerInWorld);
                if (frame.Released && _heldSlot == i) ReleaseCast();
                if (!frame.Pressed) continue;
                var result = CastSlot(PublishedSkillSlots.Ordered[i], _aim.x, _aim.y);
                if (result == null || !result.Accepted) continue;
                _heldSlot = i;
                if (frame.Released) ReleaseCast();
            }
        }

        public bool LoadReplayFile(string path)
        {
            var candidate = new PublishedSkillSession { ValidateViews = UnityActorResources.Validate };
            SkillReplayComparison comparison;
            try { comparison = candidate.ReplaceReplay(File.ReadAllText(path)); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ContentValidationException)
            {
                candidate.Dispose();
                _replayError = path + ": " + error.Message;
                return false;
            }

            if (!ReplayMode)
            {
                _resumeAutoAdvance = AutoAdvance;
                _replayBaseVersion = _publications?.Version ?? 0;
            }
            _replay?.Dispose();
            _replay = candidate;
            _replayError = null;
            _refreshAfterRead = 0;
            ReplayComparison = comparison;
            AutoAdvance = false;
            ActivateSession();
            _status = "Replay matched " + Snapshot.Step + " steps / " + ReplayComparison.Values + " values";
            return true;
        }

        public string CaptureReplayJson() => Session?.Simulation == null
            ? null
            : SkillReplayJson.Write(Session.Simulation.CaptureReplay(SkillId));

        void UpdateAim()
        {
            var camera = Camera.main;
            PointerInWorld = false;
            Vector2 pointer = Input.mousePosition;
            if (camera != null && camera.pixelRect.Contains(pointer) && !SkillLabHud.PointerOver)
            {
                var ray = camera.ScreenPointToRay(pointer);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
                {
                    var point = ray.GetPoint(distance);
                    _aim = new Vector2(point.x, point.z);
                    PointerInWorld = true;
                }
            }
            RefreshAimPreview();
            if (PointerInWorld && AutoAdvance && !Paused)
                LiveSimulation?.Execute(new SkillSimulationCommand { Kind = "aim", X = _aim.x, Z = _aim.y });
        }

        void RefreshAimPreview() => AimPreview = LiveSimulation?.InspectAim(SkillId, _aim.x, _aim.y);

        void OnApplicationFocus(bool focused) { if (!focused && !ReplayMode) CancelInput(); }

        void OnGUI()
        {
            var error = LastLoadError;
            var status = UsingDefaultSkills ? "内置试放 · " + _status : _status;
            _hud.Draw(this, error == null ? status : status + "\n" + error);
        }

        void OnDestroy()
        {
            _replay?.Dispose();
            _publications?.Dispose();
            Snapshot = null;
        }
    }
}
