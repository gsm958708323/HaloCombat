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
                _publication.Replace(slots);
                _heldKey = KeyCode.None; _heldMouse = false;
                _clock.Reset(); Generation++; CurrentSlot = null; LastLoadError = null;
                Snapshot = _publication.Simulation.Snapshot();
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
            var result = _publication.Simulation.Cast(SkillId, x, z);
            Snapshot = _publication.Simulation.Snapshot();
            _status = result.Accepted ? "Cast " + SkillId : result.Reason;
            return result;
        }

        public void ReleaseCast()
        {
            _publication.Simulation?.Release();
            if (_publication.Simulation != null) Snapshot = _publication.Simulation.Snapshot();
            _heldKey = KeyCode.None; _heldMouse = false;
        }

        public void SetMovement(float x, float z) => _publication.Simulation?.Move(x, z);
        public void SubmitInput(string action,float x,float z)
        {if(_publication.Simulation==null)return;_publication.Simulation.Input(action,x,z);Snapshot=_publication.Simulation.Snapshot();}
        public void RemoveEntity(string id)
        {_publication.Simulation?.Remove(id);if(_publication.Simulation!=null)Snapshot=_publication.Simulation.Snapshot();}
        public bool RemoveBuff(string actorId,int instanceId)
        {bool removed=_publication.Simulation!=null&&_publication.Simulation.RemoveBuff(actorId,instanceId);if(_publication.Simulation!=null)Snapshot=_publication.Simulation.Snapshot();return removed;}
        public void Interrupt(bool death=false)
        {
            _publication.Simulation?.Interrupt(death);
            if(_publication.Simulation!=null)Snapshot=_publication.Simulation.Snapshot();
        }

        public void CancelInput()
        {
            _publication.Simulation?.CancelInput();
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
            if (AutoAdvance) SetMovement(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (!AutoAdvance || Paused || _publication.Simulation == null) return;
            int steps = _clock.BeginFrame(Time.unscaledDeltaTime);
            if (steps > 0) StepLogicForTests(steps);
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
            _publication.Simulation?.Aim(_aim.x, _aim.y);
        }

        void OnApplicationFocus(bool focused) { if (!focused) CancelInput(); }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(12, 12, 470, 200), GUI.skin.box);
            GUILayout.Label("HaloCombat / JSON Skill Lab");
            GUILayout.Label("F6 / F7: cast slot | Mouse: cast selected slot");
            GUILayout.Label("F5: reload publications + reset | Space: pause");
            GUILayout.Label(_status);
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
