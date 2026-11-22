using System;
using System.Collections.Generic;
using System.IO;
using Combat.Config;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    [DisallowMultipleComponent,RequireComponent(typeof(ArenaHud),typeof(ArenaPresentation))]
    public sealed class ArenaBootstrap : MonoBehaviour
    {
        [SerializeField]string _contentFile=UnityRuntimeContent.DefaultFile;
        [SerializeField]bool _training;
        [SerializeField]bool _practice;
        readonly ArenaInputSource _input = new ArenaInputSource();
        string[] _skillNames = System.Array.Empty<string>();
        ArenaSession _session;
        ArenaPresentation _present;
        readonly SimulationClock _clock=new SimulationClock(ArenaSession.Step,12);
        public ArenaSession Session=>_session;
        public string LastLoadError {get;private set;}
        public string Status {get;private set;}="";
        public bool AutoAdvance {get;set;}=true;
        public bool Training=>_training;
        public string ContentFile=>_contentFile;
        public bool IsPractice => _practice;
        internal IReadOnlyList<string> SkillNames => _skillNames;
        public const string EditorModeKey = "HaloCombat.Arena.NextPlayTraining";

        void Start()
        {
#if UNITY_EDITOR
            if (!_practice && gameObject.scene.path == "Assets/Scenes/Arena.unity")
            {
                _training = UnityEditor.SessionState.GetBool(EditorModeKey, _training);
                UnityEditor.SessionState.EraseBool(EditorModeKey);
            }
#endif
            _present = GetComponent<ArenaPresentation>();
            if (!Reload(false) && _session == null) Debug.LogError(LastLoadError, this);
        }

        public void Configure(string contentFile,bool training=false){_contentFile=contentFile;_training=training;}
        public bool Reload(bool includePublished=true)
        {
            includePublished &= !_practice;
            ArenaSession candidate=null;
            ArenaPresentationBinding prepared=null;
            try
            {
                var loaded=UnityRuntimeContent.Load(_contentFile);var files=new Dictionary<string,string>();
                if (includePublished)
                {
                    foreach (var snapshot in PublishedSkillSlots.ReadArenaSlots(Path.GetDirectoryName(loaded.Path)))
                        files.Add(snapshot.Key, snapshot.Value);
                }
                var project=new ArenaProject(loaded.Project,files);candidate=project.Create(_training);
                _present=GetComponent<ArenaPresentation>();prepared=_present.Prepare(candidate,project.Content);
                candidate.Start(); // The previous session stays live until spawn and view construction succeed.
                candidate.World.Events.Subscribe<EvProjectileTeleport>(OnProjectileTeleport);
                _skillNames = ResolveSkillNames(project.Document);
                var previous=_session;_present.Replace(prepared);prepared=null;_session=candidate;candidate=null;
                if(previous!=null)previous.World.Events.Unsubscribe<EvProjectileTeleport>(OnProjectileTeleport);
                previous?.Dispose();_input.Cancel();_clock.Reset();_present.Sample(1,0,Time.unscaledTime);
                LastLoadError=null;Status=includePublished?"JSON reloaded, F6/F7 updated":"JSON Arena ready";return true;
            }
            catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is ContentValidationException||error is ArgumentException||error is InvalidOperationException)
            {prepared?.Dispose();candidate?.Dispose();LastLoadError=error.Message;Status=LastLoadError;return false;}
        }
        public void Restart(){Reload(false);}
        public void SetTraining(bool value){if(_practice)return;bool previous=_training;_training=value;if(!Reload(false))_training=previous;}
        public void SetPaused(bool paused){_session?.SetPaused(paused);_input.Cancel();_clock.Reset();}
        public void Submit(in ArenaInputFrame frame)=>_session?.ApplyInput(frame);
        public bool CastPublished(string slot)
        {
            if(_session==null)return false;
            if(!_session.Slots.ContainsKey(slot)){Status="Publish "+slot.ToUpperInvariant()+" from the formal project, then F5 reload.";return false;}
            bool result=_session.CastSlot(slot);if(!result&&_session.World.TryGetActor(_session.PlayerId,out var player))Status=player.GetComp<SkillCastComp>().LastRejection??"Casting blocked";return result;
        }
        public void ReleasePublished(string slot)=>_session?.ReleaseSlot(slot);
        void OnProjectileTeleport(EvProjectileTeleport result)=>Status=result.Result;
        static string[] ResolveSkillNames(SkillProjectDocument project)
        {
            var names = new string[CombatControls.Count];
            for (int i = 0; i < names.Length; i++)
            {
                var control = CombatControls.At(i);
                var binding = System.Array.Find(project.Runtime.Arena.Bindings, b => b.Action == control.Action);
                if (binding == null) continue;
                var combo = System.Linq.Enumerable.OrderByDescending(project.Runtime.Combos, rule => rule.Priority);
                var entry = System.Linq.Enumerable.FirstOrDefault(combo, r => r.Input == binding.Input && r.PreSkills.Length == 0);
                if (entry != null) names[i] = System.Array.Find(project.Skills, skill => skill.Id == entry.ToSkill)?.Name;
            }
            return names;
        }

        void Update()
        {
            if (Input.GetKeyDown(CombatControls.Reload) && Reload()) return;
            if (_session == null) return;
            if (Input.GetKeyDown(CombatControls.Pause)) { SetPaused(!_session.Paused); return; }
            if (!AutoAdvance) return;
            if (!_session.Paused && !_session.PlayerDead && _session.World.TryGetActor(_session.PlayerId,out var player))
            {
                var p = player.GetComp<TransformComp>().Position;
                Submit(_input.Sample(_present.Camera,new Vector3(p.X,p.Y,p.Z),ArenaHud.PointerOver(_session.PlayerDead)));
                if (!_practice)
                {
                    if(Input.GetKeyDown(KeyCode.F6))CastPublished("f6");if(Input.GetKeyDown(KeyCode.F7))CastPublished("f7");
                    if(!Input.GetKey(KeyCode.F6))ReleasePublished("f6");if(!Input.GetKey(KeyCode.F7))ReleasePublished("f7");
                }
                _session.Advance(_clock.BeginFrame(Time.unscaledDeltaTime));
            }
            else
            {
                _input.Cancel();
                _clock.Reset();
            }
            _session.AdvanceWall(Time.unscaledDeltaTime);
        }
        void LateUpdate(){if(_session!=null)_present?.Sample(_session.Paused||_session.PlayerDead?1:_clock.Alpha,Time.deltaTime,Time.unscaledTime);}
        void OnApplicationFocus(bool focused){if(!focused){_session?.CancelInput();_input.Cancel();_clock.Reset();}}
        void OnDisable(){_session?.CancelInput();_input.Cancel();_clock.Reset();}
        void OnDestroy(){if(_session!=null)_session.World.Events.Unsubscribe<EvProjectileTeleport>(OnProjectileTeleport);_present?.Unbind();_session?.Dispose();_session=null;}
    }
}
