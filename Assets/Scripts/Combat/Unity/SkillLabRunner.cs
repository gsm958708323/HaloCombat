using System;
using System.IO;
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
        readonly SimulationClock _clock = new SimulationClock(SkillSimulation.Step, 12);
        string _directory;
        KeyCode _heldKey;
        bool _heldMouse;
        Vector2 _aim;
        string _status = "Publish skills from the web editor, then press F5 to load.";

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
                var slots = new Dictionary<string, string>();
                foreach (string slot in new[] { "f6", "f7" })
                {
                    path = Path.Combine(ContentDirectory, slot + ".json");
                    if (File.Exists(path)) slots.Add(slot, File.ReadAllText(path));
                }
                _publication.ValidateViews=UnityActorResources.Validate;_publication.Replace(slots);
                _heldKey = KeyCode.None; _heldMouse = false;
                _clock.Reset(); Generation++; CurrentSlot = null; LastLoadError = null;
                Snapshot = _publication.Simulation.Snapshot();
                _loadedFiles=FilesStamp();PendingPublication=false;
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
            _heldKey = KeyCode.None; _heldMouse = false;
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
            _publication.Simulation?.Execute(new SkillSimulationCommand{Kind="cancel"});
            if (_publication.Simulation != null) Snapshot = _publication.Simulation.Snapshot();
            _heldKey = KeyCode.None; _heldMouse = false;
        }

        public void StepLogicForTests(int steps)
        {
            if (_publication.Simulation == null) return;
            _publication.Simulation.Advance(steps);
            Snapshot = _publication.Simulation.Snapshot();
        }

        void Update()
        {
            if(ReplayMode){if(Input.GetKeyDown(KeyCode.F5))ReloadPublished();return;}
            if(Time.unscaledTime>=_nextFileCheck){_nextFileCheck=Time.unscaledTime+.5f;CheckPublications();}
            UpdateAim();
            foreach (var key in new[] { KeyCode.F6, KeyCode.F7 })
                if (Input.GetKeyDown(key))
                { var result = CastSlot(key == KeyCode.F6 ? "f6" : "f7", _aim.x, _aim.y); if (result != null && result.Accepted) _heldKey = key; }
            if (_heldKey != KeyCode.None && Input.GetKeyUp(_heldKey)) ReleaseCast();
            if (Input.GetKeyDown(KeyCode.F5)) ReloadPublished();
            if (Input.GetKeyDown(KeyCode.Space)) { Paused = !Paused; _clock.Reset(); }
            if (Input.GetMouseButtonDown(0))
            { var result = CastSlot(CurrentSlot, _aim.x, _aim.y); _heldMouse = result != null && result.Accepted; }
            if (_heldMouse && Input.GetMouseButtonUp(0)) ReleaseCast();
            if (AutoAdvance&&!Paused) SetMovement(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (!AutoAdvance || Paused || _publication.Simulation == null) return;
            int steps = _clock.BeginFrame(Time.unscaledDeltaTime);
            if (steps > 0) StepLogicForTests(steps);
        }
        public void CheckPublications()
        {
            if(ReplayMode){PendingPublication=false;return;}
            try{PendingPublication=_publication.Simulation==null?File.Exists(Path.Combine(ContentDirectory,"f6.json"))||File.Exists(Path.Combine(ContentDirectory,"f7.json")):FilesStamp()!=_loadedFiles;}catch(IOException){PendingPublication=true;}catch(UnauthorizedAccessException){PendingPublication=true;}
        }
        public bool LoadReplayFile(string path)
        {
            try
            {
                _publication.ValidateViews=UnityActorResources.Validate;var comparison=_publication.ReplaceReplay(File.ReadAllText(path));
                if(!ReplayMode)_resumeAutoAdvance=AutoAdvance;
                ReplayComparison=comparison;
                _clock.Reset();_heldKey=KeyCode.None;_heldMouse=false;Generation++;CurrentSlot="f6";ReplayMode=true;PendingPublication=false;LastLoadError=null;
                Snapshot=_publication.Simulation.Snapshot();AutoAdvance=false;_status="Replay matched "+Snapshot.Step+" steps / "+ReplayComparison.Values+" values";return true;
            }
            catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is ContentValidationException)
            {LastLoadError=path+": "+error.Message;_status=LastLoadError;return false;}
        }
        public string CaptureReplayJson()=>_publication.Simulation==null?null:SkillReplayJson.Write(_publication.Simulation.CaptureReplay(SkillId??(_publication.Slots.ContainsKey("f6")?_publication.Slots["f6"]:_publication.Slots["f7"])));
        string FilesStamp()
        {
            var values=new List<string>();foreach(string slot in new[]{"f6","f7"}){string path=Path.Combine(ContentDirectory,slot+".json");var info=new FileInfo(path);values.Add(info.Exists?slot+":"+info.Length+":"+info.LastWriteTimeUtc.Ticks:slot+":missing");}return string.Join("|",values);
        }

        void UpdateAim()
        {
            var camera = Camera.main;
            if (camera != null)
            {
                var ray = camera.ScreenPointToRay(Input.mousePosition);
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance))
                { var point = ray.GetPoint(distance); _aim = new Vector2(point.x, point.z); }
            }
            if(AutoAdvance&&!Paused)_publication.Simulation?.Execute(new SkillSimulationCommand{Kind="aim",X=_aim.x,Z=_aim.y});
        }

        void OnApplicationFocus(bool focused) { if (!focused&&!ReplayMode) CancelInput(); }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 520, 270), GUI.skin.box);
            GUILayout.Label("HaloCombat / JSON Skill Lab");
            GUILayout.Label("F6 / F7: cast slot | Mouse: cast selected slot");
            GUILayout.Label("F5: reload publications + reset | Space: pause");
            GUILayout.Label(_status);
            foreach(var slot in SlotSkills)GUILayout.Label(slot.Key.ToUpperInvariant()+" loaded: "+slot.Value+" / "+SlotRevisions[slot.Key].Substring(0,8));
            if(PendingPublication)GUILayout.Label("New publication on disk. Press F5 or Reload to apply.");
            if(GUILayout.Button("Reload published F6 / F7"))ReloadPublished();
            if (Snapshot != null)
            {
                GUILayout.Label("Version " + Revision.Substring(0, 12) + " / " + SkillId);
                GUILayout.Label("Time " + Snapshot.Time.ToString("F2") + "s / Energy " + Snapshot.Energy.ToString("F1"));
                GUILayout.Label("Ammo "+Snapshot.Ammo+" / "+Snapshot.AmmoCapacity);
                if (Snapshot.Charging) GUILayout.Label("Charging " + (Snapshot.ChargeProgress * 100).ToString("F0") + "% / " + Snapshot.CastPower.ToString("F2") + "x");
                GUILayout.Label("Casts " + Snapshot.Statistics.Casts + " / Projectiles " + Snapshot.Statistics.Projectiles
                    + " / Hits " + Snapshot.Statistics.Hits + " / Damage " + Snapshot.Statistics.Damage.ToString("F1"));
            }
            GUILayout.EndArea();
        }

        void OnDestroy() { _publication.Dispose(); Snapshot = null; }
    }
}
