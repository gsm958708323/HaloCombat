using Combat.Config;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    // Runtime bodies have no character Animator or gameplay movement. Particles and spin
    // sample their actor clock, so render frames and pause cannot become another clock.
    public sealed class UnityBodyVisual
    {
        readonly RuntimeViewDefinition _definition;
        readonly Transform _visual;
        readonly ParticleSystem[] _particles;
        float _age;
        bool _sampled;
        public UnityBodyVisual(GameObject instance,RuntimeViewDefinition definition,Transform visual)
        {
            _definition=definition;_visual=visual;_particles=instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach(var particle in _particles){particle.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);particle.useAutoRandomSeed=false;particle.randomSeed=1;}
        }
        public void Sample(in PoseSample pose)
        {
            float dt=!_sampled?Mathf.Max(0,pose.ActorTime):!pose.InHitstop&&!pose.PresentationPaused?Mathf.Max(0,pose.ActorTime-_age):0;
            _age=pose.ActorTime;_sampled=true;
            _visual.localPosition=Vector3.zero;
            _visual.localRotation=Quaternion.Euler(0,_definition.SpinSpeed*pose.ActorTime,0);
            _visual.localScale=Vector3.one*(pose.BodyScale>0?pose.BodyScale:1);
            foreach(var particle in _particles)if(dt>0)particle.Simulate(dt,false,false,false);
        }
    }
}
