namespace Combat.Core
{
    // BT supplies input to the same guard as players and debug slots.
    public sealed class ActCastSkill : BtNode
    {
        readonly SkillNodeId _skill;
        readonly float _holdDuration;
        bool _started;
        float _held;
        public ActCastSkill(SkillNodeId skill,float holdDuration){_skill=skill;_holdDuration=holdDuration;}
        public override BtStatus Tick(in BtTick context)
        {
            if(!CondHasTarget.IsTargetValid(context)||!context.World.TryGetActor(context.Board.Target,out var target))return BtStatus.Failure;
            var source=context.Self;var cast=source.GetComp<SkillCastComp>();var director=source.GetComp<SkillDirectorComp>();
            var origin=source.GetComp<TransformComp>().Position;var point=target.GetComp<TransformComp>().Position;
            var aim=new SkillAim(target.Id,point,LocomotionComp.YawFromStick(new SimVec3(point.X-origin.X,0,point.Z-origin.Z)));
            if(!_started)
            {
                if(!cast.TryCast(_skill,aim))return BtStatus.Failure;
                _started=true;_held=0;return BtStatus.Running;
            }
            cast.UpdateAim(aim);
            if(!director.IsPlaying){_started=false;return BtStatus.Success;}
            _held+=context.Dt;
            if(_held+.000001f>=_holdDuration&&(cast.IsChanneling||cast.IsCharging))cast.Release();
            return BtStatus.Running;
        }
        public override void Abort(in BtTick context)
        {if(context.Self!=null&&context.Self.TryGetComp<SkillCastComp>(out var cast))cast.CancelHeld();_started=false;_held=0;}
        public override BtNode Clone()=>new ActCastSkill(_skill,_holdDuration);
    }

    public static class SummonBehavior
    {
        public static BtNode Create(SkillNodeId skill,float holdDuration)
        {
            var combat=new BtSelector(new BtSequence(new CondInRange(),new ActStopMove(),new ActFaceTarget(),new ActCastSkill(skill,holdDuration)),new ActMoveToward());
            return new BtSelector(
                new BtSequence(new CondHasTag(CommonTags.Dead),new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Downed),new ActStopMove()),
                new BtSequence(new CondHasTag(CommonTags.Stunned),new ActStopMove()),
                // Acquisition and an already-selected target converge on one action instance.
                // Switching between two cloned combat branches would abort the held cast
                // immediately after the acquisition branch starts it.
                new BtSequence(new BtSelector(new CondHasTarget(),new ActAcquireHostile()),combat),
                new ActFollowOwner());
        }
    }
}
