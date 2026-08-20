using System;

namespace Combat.Core
{
     public enum DirectorStopReason : byte
    {
        Finished = 0,
        Replaced = 1, // 换轴
        Hit = 2,
        Dead = 3,
        Manual = 4,
        Detach = 5
    }
    public sealed class SkillDirectorComp : Comp
    {
        readonly TimelineLibrary _library;
        readonly TimelinePlayer _player;
        StateMachineComp _fsm;
        SkillNodeId _currentSkill = SkillNodeId.None;
        public SkillNodeId CurrentSkill => _currentSkill;
        public bool IsPlaying => _player.IsPlaying;
        public override bool WantsTick => true;
        public SkillDirectorComp(TimelineLibrary library, EffectFactory effects)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _player = new TimelinePlayer(effects ?? throw new ArgumentNullException(nameof(effects)));
        }
        protected override void OnAttach()
        {
            _fsm = Self.GetComp<StateMachineComp>();
        }
        protected override void OnDetach()
        {
            Stop(DirectorStopReason.Detach);
            _fsm = null;
        }
        public void Play(SkillNodeId skill, TimelineId timelineId)
        {
            if (!_library.TryGet(timelineId, out var so))
                throw new InvalidOperationException($"Missing timeline {timelineId}");
            if (_player.IsPlaying)
                _player.Stop(); // 区间 Exit 会清 Tag
            _currentSkill = skill;
            _player.Play(so);
            if (_fsm.Current != ActorStateId.Attack)
                _fsm.TryEnter(ActorStateId.Attack, new StateEnterArgs(_fsm.Current, "PlaySkill"));
        }
        public void Stop(DirectorStopReason reason)
        {
            bool wasPlaying = _player.IsPlaying || _currentSkill.IsValid;
            _player.Stop();
            _currentSkill = SkillNodeId.None;
            // 自然播完 → 回 Root；Hit/Dead/Replace 不在这里抢状态
            if (reason == DirectorStopReason.Finished && wasPlaying)
                _fsm?.NotifyActivityFinished(ActorStateId.Attack, "TimelineFinished");
        }
        public override void Tick(float dt)
        {
            if (!_player.IsPlaying)
                return;
            _player.Tick(dt, Self);
            if (!_player.IsPlaying)
            {
                // 播完
                _currentSkill = SkillNodeId.None;
                _fsm.NotifyActivityFinished(ActorStateId.Attack, "TimelineFinished");
            }
        }
    }

    public struct TimelineCursor
    {
        public TimelineId Id;
        public float Time;
        public bool Stopped;
    }

    public struct StopReason
    {
        public string Reason;
        public static StopReason Hit = new StopReason { Reason = "Hit" };
        public static StopReason Dead = new StopReason { Reason = "Dead" };
        public static StopReason Detach = new StopReason { Reason = "Detach" };
    }
}
