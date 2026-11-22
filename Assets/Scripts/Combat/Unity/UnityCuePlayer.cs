using Combat.Config;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class UnityCuePlayer : ICuePlayer
    {
        readonly CueLibrary _cues;
        readonly System.Collections.Generic.Dictionary<int,GameObject> _prefabs=new System.Collections.Generic.Dictionary<int,GameObject>();
        readonly System.Collections.Generic.Dictionary<int,AudioClip> _sounds=new System.Collections.Generic.Dictionary<int,AudioClip>();
        readonly Transform _root;
        readonly CombatWorld _world;
        readonly System.Collections.Generic.Dictionary<(EntityId,string),UnityCueLoop> _loops=new System.Collections.Generic.Dictionary<(EntityId,string),UnityCueLoop>();
        public int ActiveLoopCount=>_loops.Count;
        public UnityCuePlayer(CueLibrary cues,Transform root=null,CombatWorld world=null)
        {
            _root=root;_world=world;
            _cues=cues??throw new System.ArgumentNullException(nameof(cues));
            foreach(var cue in cues.All)
            {
                if(cue.PresentationKind=="prefab")_prefabs.Add(cue.CueId,Resources.Load<GameObject>(cue.ResourceKey));
                if(!string.IsNullOrEmpty(cue.SfxKey))_sounds.Add(cue.CueId,Resources.Load<AudioClip>(cue.SfxKey));
            }
        }

        public void Play(EvCue e)
        {
            if(!_cues.TryGet(e.CueId,out var definition))throw new System.InvalidOperationException("Unknown cue "+e.CueId);
            var key=(e.Source,e.InstanceKey??"");
            if(e.Playback==CuePlayback.Stop)
            {
                if(_loops.TryGetValue(key,out var existing)&&existing!=null&&(!existing.StopWithCast||e.Origin.CastId==0||existing.CastId==e.Origin.CastId))existing.End();
                return;
            }
            if(e.Playback==CuePlayback.Start&&_loops.TryGetValue(key,out var previous)&&previous!=null)previous.End();
            _prefabs.TryGetValue(e.CueId,out var prefab);
            if(definition.PresentationKind=="prefab"&&prefab==null)throw new System.InvalidOperationException("Missing cue prefab "+definition.ResourceKey);
            var gameObject = prefab != null
                ? Object.Instantiate(prefab)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (prefab == null)
            {
                var collider = gameObject.GetComponent<Collider>();
                if (collider != null) Object.Destroy(collider);
                gameObject.transform.localScale = Vector3.one * 0.2f;
            }

            gameObject.name = "Cue_" + e.CueId;
            if(_root!=null)gameObject.transform.SetParent(_root,false);
            var anchor=Anchor(e.Source,e.Anchor);
            if(anchor!=null){gameObject.transform.SetParent(anchor,false);gameObject.transform.localPosition=Vector3.zero;}
            else if(e.HasPoint)gameObject.transform.position=new Vector3(e.Point.X,e.Point.Y,e.Point.Z);
            else if(_world!=null&&_world.TryGetActor(e.Source,out var actor)){var p=actor.GetComp<TransformComp>().Position;gameObject.transform.position=new Vector3(p.X,p.Y,p.Z);}
            if(_sounds.TryGetValue(e.CueId,out var sound))
            {
                if(e.Playback==CuePlayback.Start){var audio=gameObject.AddComponent<AudioSource>();audio.clip=sound;audio.loop=true;audio.Play();}
                else AudioSource.PlayClipAtPoint(sound,gameObject.transform.position);
            }
            if(e.Playback==CuePlayback.Start)
            {
                var loop=gameObject.AddComponent<UnityCueLoop>();_loops[key]=loop;
                loop.Initialize(_world,e,()=>{if(_loops.TryGetValue(key,out var current)&&current==loop)_loops.Remove(key);},anchor);
            }
            else Object.Destroy(gameObject, definition.LifeTime);
        }
        Transform Anchor(EntityId id,string key)
        {
            if(string.IsNullOrEmpty(key)||_root==null)return null;
            var parent=_root.parent??_root;foreach(var view in parent.GetComponentsInChildren<UnityActorView>(true))if(view.Id==id)return view.Anchor(key);return null;
        }
    }
}
