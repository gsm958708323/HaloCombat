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
        Transform _attachment;
        ParticleSystem[] _particles;
        double _time;
        public int CastId=>_cast;
        public bool StopWithCast=>_stopWithCast;
        public void Initialize(CombatWorld world,EvCue cue,Action removed,Transform attachment=null)
        {
            _world=world;_anchor=cue.Source;_caster=cue.Origin.Caster;_cast=cue.Origin.CastId;_stopWithCast=cue.StopWithCast;_removed=removed;
            _attachment=attachment;
            _particles=GetComponentsInChildren<ParticleSystem>();
            foreach(var particle in _particles){var main=particle.main;main.loop=true;particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);particle.useAutoRandomSeed=false;particle.randomSeed=1;}
            if(world!=null&&world.TryGetActor(_anchor,out var owner))_time=owner.Time.Time;
            LateUpdate();
        }
        void LateUpdate()
        {
            if(_world==null)return;
            if(!_world.TryGetActor(_anchor,out var actor)||!actor.IsActive||actor.GetComp<TagComp>().Has(CommonTags.Dead)){End();return;}
            if(_stopWithCast&&_cast>0&&(!_world.TryGetActor(_caster,out var caster)||!caster.TryGetComp<SkillDirectorComp>(out var director)||!director.IsPlaying||director.CurrentOrigin.CastId!=_cast)){End();return;}
            float dt=(float)Math.Max(0,actor.Time.Time-_time);_time=actor.Time.Time;
            if(dt>0)foreach(var particle in _particles)particle.Simulate(dt,false,false,false);
            if(_attachment==null){var p=actor.GetComp<TransformComp>().Position;transform.position=new Vector3(p.X,p.Y,p.Z);}
        }
        public void End(){gameObject.SetActive(false);Destroy(gameObject);}
        void OnDestroy(){_removed?.Invoke();_removed=null;_world=null;}
    }
}
