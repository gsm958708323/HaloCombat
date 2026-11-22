using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Combat.Config;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    [DisallowMultipleComponent]
    public sealed class SkillLabRunner : MonoBehaviour
    {
        readonly PublishedSkillSession _publication = new PublishedSkillSession();
        readonly SkillLabHud _hud = new SkillLabHud();
        readonly SimulationClock _clock = new SimulationClock(SkillSimulation.Step, 12);
        string _directory;
        readonly CombatControls.Sampler _input = new CombatControls.Sampler();
        int _heldSlot = -1;
        Vector2 _aim;
        string _status = "在网页发布到技能槽，然后按 F5 载入。";

        public bool AutoAdvance { get; set; } = true;
        public bool Paused { get; set; }
        public string CurrentSlot { get; private set; }
        public string Revision => _publication.Revision;
        public string SkillId => CurrentSlot != null && _publication.Slots.TryGetValue(CurrentSlot, out var skill) ? skill : null;
        public string LastLoadError { get; private set; }
        public int Generation { get; private set; }
        public SkillSimulationSnapshot Snapshot { get; private set; }
        public string ContentDirectory => _directory ?? Path.Combine(Application.streamingAssetsPath, "Combat");
        public IReadOnlyDictionary<string,RuntimeViewDefinition> Views=>_publication.Simulation?.Views;
        public IReadOnlyDictionary<string,string> SlotSkills=>_publication.Slots;
        public IReadOnlyDictionary<string,string> SlotRevisions=>_publication.SlotRevisions;
        public SkillAimPreview AimPreview { get; private set; }
        public bool PointerInWorld { get; private set; }
        public bool ShowRanges => _hud.Ranges;
        public bool ShowTrajectories => _hud.Trajectories;
        public bool ShowDamageNumbers => _hud.DamageNumbers;
        public bool HasDifferentSlotLayouts => !ReplayMode && _publication.HasDifferentSlotLayouts;
        public bool PendingPublication {get;private set;}
        public bool ReplayMode {get;private set;}
        public SkillReplayComparison ReplayComparison {get;private set;}
        bool _resumeAutoAdvance;
        string _loadedFiles;
        float _nextFileCheck;

        public void ConfigureContentDirectory(string directory)
        {
            if (Snapshot != null) throw new InvalidOperationException("Configure the directory before loading content.");
            _directory = Path.GetFullPath(directory);
        }

        public bool ReloadPublished()
        {
            string path = ContentDirectory;
            try
            {
                var before = FilesStamp();
                var slots = PublishedSkillSlots.Read(ContentDirectory);
                _publication.ValidateViews = UnityActorResources.Validate;
                _publication.Replace(slots);
                _heldSlot = -1;
                _input.Cancel();
                _clock.Reset();
                Generation++;
                CurrentSlot = PublishedSkillSlots.Ordered.First(_publication.Slots.ContainsKey);
                AimPreview = null;
                LastLoadError = null;
                Snapshot = _publication.Simulation.Snapshot();
                _loadedFiles=before;CheckPublications();
                if(ReplayMode)AutoAdvance=_resumeAutoAdvance;
                ReplayMode=false;ReplayComparison=null;
                _status = "Loaded " + string.Join(" / ", _publication.Slots.Keys) + " / " + Revision.Substring(0, 12);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ContentValidationException)
            {
                LastLoadError = ContentDirectory + ": " + error.Message;
                _status = LastLoadError;
                return false;
            }
        }

        public bool SelectSlot(string slot)
        {
            if (ReplayMode || !_publication.Slots.ContainsKey(slot)) return false;
            CurrentSlot = slot;
            RefreshAimPreview();
            return true;
        }

        internal string DisplaySkillName(string key) =>
            _publication.Simulation?.InspectAim(key, _aim.x, _aim.y)?.Name ?? key;

        public void TogglePause()
        {
            Paused = !Paused;
            CancelInput();
            _clock.Reset();
        }

        public SkillCastResult CastSlot(string slot, float x, float z)
        {
            if (_publication.Simulation == null && !ReloadPublished()) return null;
            if (slot == null || !_publication.Slots.ContainsKey(slot)) { _status = "Slot is unavailable; publish it and press F5."; return null; }
            CurrentSlot = slot;
            var result = (SkillCastResult)_publication.Simulation.Execute(new SkillSimulationCommand{Kind="cast",SkillId=SkillId,X=x,Z=z});
            Snapshot = _publication.Simulation.Snapshot();
            _status = result.Accepted ? "Cast " + SkillId : result.Reason;
            return result;
        }

        public void ReleaseCast()
        {
            _publication.Simulation?.Execute(new SkillSimulationCommand{Kind="release"});
            if (_publication.Simulation != null) Snapshot = _publication.Simulation.Snapshot();
            _heldSlot = -1;
        }

        public void SetMovement(float x, float z) => _publication.Simulation?.Execute(new SkillSimulationCommand{Kind="move",X=x,Z=z});
        public void PlaceActor(string sceneId,string entityId,float x,float z)
        {_publication.Simulation?.Execute(new SkillSimulationCommand{Kind="place",ActorId=sceneId,EntityId=entityId,X=x,Z=z});if(_publication.Simulation!=null)Snapshot=_publication.Simulation.Snapshot();}
        public void SubmitInput(string action,float x,float z)
        {if(_publication.Simulation==null)return;_publication.Simulation.Execute(new SkillSimulationCommand{Kind="input",Input=action,X=x,Z=z});Snapshot=_publication.Simulation.Snapshot();}
        public void RemoveEntity(string id)
        {_publication.Simulation?.Execute(new SkillSimulationCommand{Kind="remove",EntityId=id});if(_publication.Simulation!=null)Snapshot=_publication.Simulation.Snapshot();}
        public bool RemoveBuff(string actorId,int instanceId)
        {bool removed=_publication.Simulation!=null&&(bool)_publication.Simulation.Execute(new SkillSimulationCommand{Kind="remove-buff",ActorId=actorId,InstanceId=instanceId});if(_publication.Simulation!=null)Snapshot=_publication.Simulation.Snapshot();return removed;}
        public void Interrupt(bool death=false)
        {
            _publication.Simulation?.Execute(new SkillSimulationCommand{Kind=death?"death":"interrupt"});
            if(_publication.Simulation!=null)Snapshot=_publication.Simulation.Snapshot();
        }

        public void CancelInput()
        {
            _input.Cancel();
            _publication.Simulation?.Execute(new SkillSimulationCommand{Kind="cancel"});
            if (_publication.Simulation != null) Snapshot = _publication.Simulation.Snapshot();
            _heldSlot = -1;
        }

        public void AdvanceSimulation(int steps)
        {
            if (_publication.Simulation == null) return;
            _publication.Simulation.Advance(steps);
            Snapshot = _publication.Simulation.Snapshot();
        }

        void Update()
        {
            if (Input.GetKeyDown(CombatControls.Reload) && ReloadPublished()) return;
            if (ReplayMode) return;
            if (Time.unscaledTime >= _nextFileCheck) { _nextFileCheck = Time.unscaledTime + .5f; CheckPublications(); }
            if (Input.GetKeyDown(CombatControls.Pause)) { TogglePause(); return; }

            UpdateAim();
            if (_publication.Simulation != null) SampleSkills();
            if (Paused && Input.GetKeyDown(CombatControls.Step)) { AdvanceSimulation(1); return; }
            if (!AutoAdvance || Paused || _publication.Simulation == null) return;
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

        public void CheckPublications()
        {
            if(ReplayMode){PendingPublication=false;return;}
            try
            {
                PendingPublication = _publication.Simulation == null
                    ? PublishedSkillSlots.Ordered.Any(slot => File.Exists(Path.Combine(ContentDirectory, slot + ".json")))
                    : FilesStamp() != _loadedFiles;
            }
            catch (IOException) { PendingPublication = true; }
            catch (UnauthorizedAccessException) { PendingPublication = true; }
        }
        public bool LoadReplayFile(string path)
        {
            try
            {
                _publication.ValidateViews=UnityActorResources.Validate;var comparison=_publication.ReplaceReplay(File.ReadAllText(path));
                if(!ReplayMode)_resumeAutoAdvance=AutoAdvance;
                ReplayComparison=comparison;
                _clock.Reset();_heldSlot=-1;_input.Cancel();Generation++;CurrentSlot=PublishedSkillSlots.Ordered[0];ReplayMode=true;PendingPublication=false;LastLoadError=null;
                AimPreview = null;
                Snapshot=_publication.Simulation.Snapshot();AutoAdvance=false;_status="Replay matched "+Snapshot.Step+" steps / "+ReplayComparison.Values+" values";return true;
            }
            catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is ContentValidationException)
            {LastLoadError=path+": "+error.Message;_status=LastLoadError;return false;}
        }
        public string CaptureReplayJson()=>_publication.Simulation==null?null:SkillReplayJson.Write(_publication.Simulation.CaptureReplay(SkillId??_publication.Slots[PublishedSkillSlots.Ordered.First(_publication.Slots.ContainsKey)]));
        string FilesStamp()
        {
            var values=new List<string>();foreach(string slot in PublishedSkillSlots.Ordered){string path=Path.Combine(ContentDirectory,slot+".json");var info=new FileInfo(path);values.Add(info.Exists?slot+":"+info.Length+":"+info.LastWriteTimeUtc.Ticks:slot+":missing");}return string.Join("|",values);
        }

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
                _publication.Simulation?.Execute(new SkillSimulationCommand { Kind = "aim", X = _aim.x, Z = _aim.y });
        }

        void RefreshAimPreview() => AimPreview = _publication.Simulation?.InspectAim(SkillId, _aim.x, _aim.y);

        void OnApplicationFocus(bool focused) { if (!focused&&!ReplayMode) CancelInput(); }

        void OnGUI() => _hud.Draw(this, _status);

        void OnDestroy() { _publication.Dispose(); Snapshot = null; }
    }
}
