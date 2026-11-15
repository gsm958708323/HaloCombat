using System;
using Combat.Core;
using UnityEngine;

namespace Combat.Unity
{
    public sealed class UnityCueLoop : MonoBehaviour
    {
        CombatWorld _world;
        EntityId _anchor,_caster;
        int _cast;
        bool _stopWithCast;
        Action _removed;
        public int CastId=>_cast;
        public void Initialize(CombatWorld world,EvCue cue,Action removed)
        {
            _world=world;_anchor=cue.Source;_caster=cue.Origin.Caster;_cast=cue.Origin.CastId;_stopWithCast=cue.StopWithCast;_removed=removed;
            foreach(var particles in GetComponentsInChildren<ParticleSystem>()){var main=particles.main;main.loop=true;particles.Play();}
            LateUpdate();
        }
        void LateUpdate()
        {
            if(_world==null)return;
            if(!_world.TryGetActor(_anchor,out var actor)||!actor.IsActive||actor.GetComp<TagComp>().Has(CommonTags.Dead)){End();return;}
            if(_stopWithCast&&_cast>0&&(!_world.TryGetActor(_caster,out var caster)||!caster.TryGetComp<SkillDirectorComp>(out var director)||!director.IsPlaying||director.CurrentOrigin.CastId!=_cast)){End();return;}
            var p=actor.GetComp<TransformComp>().Position;transform.position=new Vector3(p.X,p.Y,p.Z);
        }
        public void End(){gameObject.SetActive(false);Destroy(gameObject);}
        void OnDestroy(){_removed?.Invoke();_removed=null;_world=null;}
    }
}
