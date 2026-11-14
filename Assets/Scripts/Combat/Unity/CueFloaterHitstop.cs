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
            if(e.HasPoint)gameObject.transform.position=new Vector3(e.Point.X,e.Point.Y,e.Point.Z);
            else if(_world!=null&&_world.TryGetActor(e.Source,out var actor)){var p=actor.GetComp<TransformComp>().Position;gameObject.transform.position=new Vector3(p.X,p.Y,p.Z);}
            if(_sounds.TryGetValue(e.CueId,out var sound))AudioSource.PlayClipAtPoint(sound,gameObject.transform.position);
            Object.Destroy(gameObject, definition.LifeTime);
        }
    }

    public sealed class UnityFloater : IFloaterPlayer
    {
        public void Play(EvDamage e)
        {
            Debug.Log("DMG " + e.Amount.ToString("F0") + (e.IsCrit ? " CRIT" : "") + " src=" + e.Source);
        }

        public void PlayImmune(EvImmune e) => Debug.Log("IMMUNE " + e.Target);
    }

    public sealed class UnityHitstopOverlay : IHitstopOverlay
    {
        float _flash;
        Texture2D _texture;

        public void ShowFlash() => _flash = 0.08f;
        public void SetActive(bool on) { }

        public void TickWall(float dt)
        {
            if (_flash > 0f) _flash -= dt;
        }

        public void OnGUI()
        {
            if (_flash <= 0f) return;
            if (_texture == null)
            {
                _texture = new Texture2D(1, 1);
                _texture.SetPixel(0, 0, Color.white);
                _texture.Apply();
            }
            var color = new Color(1f, 1f, 1f, _flash / 0.08f * 0.25f);
            GUI.color = color;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _texture);
            GUI.color = Color.white;
        }
    }
}
