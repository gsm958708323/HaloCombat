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
        ArenaSession _session;
        ArenaPresentation _present;
        readonly SimulationClock _clock=new SimulationClock(ArenaSession.Step,12);
        public ArenaSession Session=>_session;
        public string LastLoadError {get;private set;}
        public string Status {get;private set;}="";
        public bool AutoAdvance {get;set;}=true;
        public bool Training=>_training;
        public string ContentFile=>_contentFile;
        void Start(){_present=GetComponent<ArenaPresentation>();if(!Reload(false)&&_session==null)Debug.LogError(LastLoadError,this);}
        public void Configure(string contentFile,bool training=false){_contentFile=contentFile;_training=training;}
        public bool Reload(bool includePublished=true)
        {
            ArenaSession candidate=null;
            ArenaPresentationBinding prepared=null;
            try
            {
                var loaded=UnityRuntimeContent.Load(_contentFile);var files=new Dictionary<string,string>();
                if(includePublished)foreach(string slot in new[]{"f6","f7"}){string path=Path.Combine(Path.GetDirectoryName(loaded.Path),slot+".json");if(File.Exists(path))files.Add(slot,File.ReadAllText(path));}
                var project=new ArenaProject(loaded.Project,files);candidate=project.Create(_training);
                _present=GetComponent<ArenaPresentation>();prepared=_present.Prepare(candidate,project.Content);
                candidate.Start(); // The previous session stays live until spawn and view construction succeed.
                candidate.World.Events.Subscribe<EvProjectileTeleport>(OnProjectileTeleport);
                var previous=_session;_present.Replace(prepared);prepared=null;_session=candidate;candidate=null;
                if(previous!=null)previous.World.Events.Unsubscribe<EvProjectileTeleport>(OnProjectileTeleport);
                previous?.Dispose();_clock.Reset();_present.Sample(1);
                LastLoadError=null;Status=includePublished?"JSON reloaded, F6/F7 updated":"JSON Arena ready";return true;
            }
            catch(Exception error) when(error is IOException||error is UnauthorizedAccessException||error is ContentValidationException||error is ArgumentException||error is InvalidOperationException)
            {prepared?.Dispose();candidate?.Dispose();LastLoadError=error.Message;Status=LastLoadError;return false;}
        }
        public void Restart(){Reload(false);}
        public void SetTraining(bool value){bool previous=_training;_training=value;if(!Reload(false))_training=previous;}
        public void SetPaused(bool paused){_session?.SetPaused(paused);_clock.Reset();}
        public void Submit(in ArenaInputFrame frame)=>_session?.ApplyInput(frame);
        public bool CastPublished(string slot)
        {
            if(_session==null)return false;
            if(!_session.Slots.ContainsKey(slot)){Status="Publish "+slot.ToUpperInvariant()+" from the formal project, then F5 reload.";return false;}
            bool result=_session.CastSlot(slot);if(!result&&_session.World.TryGetActor(_session.PlayerId,out var player))Status=player.GetComp<SkillCastComp>().LastRejection??"Casting blocked";return result;
        }
        public void StepForTests(int steps)=>_session?.Advance(steps);
        public void ReleasePublished(string slot)=>_session?.ReleaseSlot(slot);
        public void AdvanceWallForTests(float dt)=>_session?.AdvanceWall(dt);
        void OnProjectileTeleport(EvProjectileTeleport result)=>Status=result.Result;
        void Update()
        {
            if(_session==null)return;
            if(Input.GetKeyDown(KeyCode.F5)){Reload();return;}
            if(Input.GetKeyDown(KeyCode.Escape))SetPaused(!_session.Paused);
            if(!AutoAdvance)return;
            if(!_session.Paused&&!_session.PlayerDead&&_session.World.TryGetActor(_session.PlayerId,out var player))
            {
                var p=player.GetComp<TransformComp>().Position;Submit(ArenaInputSource.Sample(_present.Camera,new Vector3(p.X,p.Y,p.Z)));
                if(Input.GetKeyDown(KeyCode.F6))CastPublished("f6");if(Input.GetKeyDown(KeyCode.F7))CastPublished("f7");
                if(Input.GetKeyUp(KeyCode.F6))ReleasePublished("f6");if(Input.GetKeyUp(KeyCode.F7))ReleasePublished("f7");
                _session.Advance(_clock.BeginFrame(Time.unscaledDeltaTime));
            }
            else _clock.Reset();
            _session.AdvanceWall(Time.unscaledDeltaTime);
        }
        void LateUpdate(){if(_session!=null)_present?.Sample(_session.Paused||_session.PlayerDead?1:_clock.Alpha);}
        void OnApplicationFocus(bool focused){if(!focused){_session?.CancelInput();_clock.Reset();}}
        void OnDisable(){_session?.CancelInput();_clock.Reset();}
        void OnDestroy(){if(_session!=null)_session.World.Events.Unsubscribe<EvProjectileTeleport>(OnProjectileTeleport);_present?.Unbind();_session?.Dispose();_session=null;}
    }
}
