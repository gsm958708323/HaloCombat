using Combat.Config;
using Combat.Core;
using Combat.Presentation;
using UnityEngine;

namespace Combat.Unity
{
    // Explicit sampling prevents controller transitions, render frame rate and root motion
    // from becoming a second gameplay clock. Loop phase is never reset on every frame.
    public sealed class UnityAnimatorDriver
    {
        readonly Animator _animator;
        readonly RuntimeViewDefinition _definition;
        float _clock,_bodyTime,_hurtTime,_deathTime,_length=1;
        bool _sampled,_hurt,_dead;
        double _wallTime;
        HitFeedbackLevel _hurtLevel;
        public string State {get;private set;}
        public float Phase {get;private set;}
        public long CastId {get;private set;}
        public int ActionStarts {get;private set;}
        public Animator Animator=>_animator;

        public UnityAnimatorDriver(Animator animator,RuntimeViewDefinition definition)
        {
            _animator=animator;_definition=definition;
            animator.applyRootMotion=false;animator.fireEvents=false;animator.speed=0;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
        }
        public void Hurt(HitFeedbackLevel level){if(!_dead){_hurt=true;_hurtTime=0;_hurtLevel=level;}}
        public void Dead(){_dead=true;_hurt=false;_deathTime=0;_wallTime=Time.realtimeSinceStartupAsDouble;}
        public void Sample(in PoseSample pose)
        {
            if(!_animator.gameObject.activeInHierarchy)return;
            if(pose.Activity==ActivityId.Dead&&!_dead)Dead();
            float dt=_sampled&&!pose.InHitstop&&!pose.PresentationPaused?Mathf.Max(0,pose.ActorTime-_clock):0;
            _clock=pose.ActorTime;_sampled=true;
            if(_hurt)_hurtTime+=dt;
            if(_dead)
            {
                double now=Time.realtimeSinceStartupAsDouble;
                if(!pose.PresentationPaused)_deathTime+=(float)System.Math.Max(0,now-_wallTime);
                _wallTime=now;
            }
            bool action=pose.CastId>0&&!string.IsNullOrEmpty(pose.Animation);
            if(action&&pose.CastId!=CastId)ActionStarts++;
            CastId=action?pose.CastId:0;
            string state;
            if(_dead)state="Dead";
            else if(_hurt)state=_hurtLevel>=HitFeedbackLevel.Heavy?"Hurt1":"Hurt0";
            else if(pose.Activity==ActivityId.Hit)state="Hurt0";
            else if(action)state=pose.Animation=="Roll"?"Roll"+Direction(pose):pose.Animation;
            else if(!pose.Grounded)state="JumpAir";
            else state=!pose.MoveBlocked&&LocomotionComp.StickMag(pose.MoveIntent)>.01f?"Move"+Direction(pose):"Stand";
            if(State!=state)
            {
                State=state;_bodyTime=0;
                _animator.Play(Hash(state),0,0);_animator.Update(0);
                // AnimatorStateInfo.length becomes Infinity when animator.speed is zero.
                // The source clip length stays finite and is the body/reaction clock unit.
                _length=Mathf.Max(.001f,_animator.GetCurrentAnimatorClipInfo(0)[0].clip.length);
            }
            else _bodyTime+=dt;
            Phase=_dead?Mathf.Min(.9999f,_deathTime/_length):_hurt?Mathf.Min(.9999f,_hurtTime/_length):action?
                Mathf.Clamp01(pose.ActionTime/Mathf.Max(.001f,pose.ActionDuration)):pose.Activity==ActivityId.Hit?Mathf.Min(.9999f,_bodyTime/_length):Mathf.Repeat(_bodyTime/_length,1);
            _animator.Play(Hash(state),0,Phase);_animator.Update(0);
            if(_hurt&&_hurtTime>=_length)_hurt=false;
        }
        int Hash(string state)=>Animator.StringToHash(_definition.States[state]);
        static string Direction(in PoseSample pose)
        {
            if(LocomotionComp.StickMag(pose.MoveIntent)<.01f)return "Forward";
            float delta=Mathf.DeltaAngle(pose.YawDeg,LocomotionComp.YawFromStick(pose.MoveIntent));
            return Mathf.Abs(delta)<=45?"Forward":Mathf.Abs(delta)>=135?"Back":delta>0?"Left":"Right";
        }
    }
}
