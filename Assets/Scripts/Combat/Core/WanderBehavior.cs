using System;

namespace Combat.Core
{
    public sealed class WanderDefinition
    {
        public float FirstTurn,TurnIntervalMin,TurnIntervalMax,TurnAngle;
        public float FirstShot,ShotIntervalMin,ShotIntervalMax;
    }
    public static class WanderBehavior
    {
        public static BtNode Create(SkillNodeId skill,WanderDefinition definition)
            =>new BtSelector(
                new BtSequence(new CondHasTag(CommonTags.Dead),new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Downed),new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Stunned),new ActStopMove()),
                new BtParallel(new ActRandomWander(definition.FirstTurn,definition.TurnIntervalMin,definition.TurnIntervalMax,definition.TurnAngle),
                    new ActPeriodicCast(skill,definition.FirstShot,definition.ShotIntervalMin,definition.ShotIntervalMax)));
    }
    public sealed class ActRandomWander : BtNode
    {
        readonly float _first,_minimum,_maximum,_angle;
        double _remaining;
        float _yaw;
        bool _initialized;
        public ActRandomWander(float first,float minimum,float maximum,float angle)
        {_first=first;_minimum=minimum;_maximum=maximum;_angle=angle;_remaining=first;}
        public override BtStatus Tick(in BtTick context)
        {
            if(context.Dt<=0)return BtStatus.Running;
            var transform=context.Self.GetComp<TransformComp>();var locomotion=context.Self.GetComp<LocomotionComp>();
            if(!_initialized){_yaw=transform.YawDegrees;_initialized=true;}
            _remaining-=context.Dt;
            if(_remaining<=.000001)
            {
                // Current yaw = 90 - the old +Z yaw, so the old subtracted turn
                // becomes an added offset while retaining the same random draw.
                _yaw+=(context.World.Random.Next01()*2-1)*_angle;
                _remaining=_minimum+(_maximum-_minimum)*context.World.Random.Next01();
            }
            var move=LocomotionComp.ForwardFromYaw(_yaw);locomotion.RequestMoveIntent(move.X,move.Z);return BtStatus.Running;
        }
        public override void Abort(in BtTick context)=>context.Self.GetComp<LocomotionComp>().RequestMoveIntent(0,0);
        public override BtNode Clone()=>new ActRandomWander(_first,_minimum,_maximum,_angle);
    }
    public sealed class ActPeriodicCast : BtNode
    {
        readonly SkillNodeId _skill;
        readonly float _first,_minimum,_maximum;
        double _remaining;
        public ActPeriodicCast(SkillNodeId skill,float first,float minimum,float maximum)
        {_skill=skill;_first=first;_minimum=minimum;_maximum=maximum;_remaining=first;}
        public override BtStatus Tick(in BtTick context)
        {
            if(context.Dt<=0)return BtStatus.Running;
            var self=context.Self;var transform=self.GetComp<TransformComp>();var point=transform.Position;var target=EntityId.Invalid;
            float yaw=transform.YawDegrees;bool hasTarget=CondHasTarget.IsTargetValid(context);
            if(hasTarget&&context.World.TryGetActor(context.Board.Target,out var actor))
            {
                target=actor.Id;point=actor.GetComp<TransformComp>().Position;
                yaw=LocomotionComp.YawFromStick(new SimVec3(point.X-transform.Position.X,0,point.Z-transform.Position.Z));self.GetComp<LocomotionComp>().RequestAimYaw(yaw);
            }
            _remaining-=context.Dt;
            if(_remaining<=.000001&&!self.GetComp<SkillDirectorComp>().IsPlaying&&self.GetComp<SkillCastComp>().TryCast(_skill,new SkillAim(target,point,yaw,hasTarget)))
                _remaining=_minimum+(_maximum-_minimum)*context.World.Random.Next01();
            return BtStatus.Running;
        }
        public override BtNode Clone()=>new ActPeriodicCast(_skill,_first,_minimum,_maximum);
    }
}
