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
        readonly EffectFactory _effects;
        readonly TimelinePlayer _player;
        TagComp _tags;
        SkillNodeId _currentSkill = SkillNodeId.None;
        public SkillNodeId CurrentSkill => _currentSkill;
        public bool IsPlaying => _player.IsPlaying;
        public override bool WantsTick => true;
        public SkillDirectorComp(TimelineLibrary library, EffectFactory effects)
        {
            _library = library ?? throw new ArgumentNullException(nameof(library));
            _effects = effects ?? throw new ArgumentNullException(nameof(effects));
            _player = new TimelinePlayer(_effects);
        }
        protected override void OnAttach()
        {
            _tags = Self.GetComp<TagComp>();
        }
        protected override void OnDetach()
        {
            Stop(DirectorStopReason.Detach);
            _tags = null;
            _currentSkill = SkillNodeId.None;
        }
        /// <summary>换招 = 换轴。先停旧轴，再播新轴。</summary>
        public void Play(SkillNodeId skill, TimelineId timelineId)
        {
            if (!skill.IsValid || !timelineId.IsValid)
                throw new ArgumentException("Play requires valid skill and timeline");
            if (!_library.TryGet(timelineId, out var so))
                throw new InvalidOperationException($"Timeline not registered: {timelineId}");
            if (_player.IsPlaying)
                Stop(DirectorStopReason.Replaced);
            _currentSkill = skill;
            _player.Play(so);
        }
        public void Stop(DirectorStopReason reason)
        {
            if (!_player.IsPlaying && !_currentSkill.IsValid)
                return;
            _player.Stop();
            // 受击/死亡：技能节点清空（Attack 主状态是否退出由 StateMachine 决定）
            if (reason == DirectorStopReason.Hit ||
                reason == DirectorStopReason.Dead ||
                reason == DirectorStopReason.Detach)
            {
                _currentSkill = SkillNodeId.None;
            }
            else if (reason == DirectorStopReason.Finished)
            {
                _currentSkill = SkillNodeId.None;
            }
            // Replaced：Play 会马上写入新 skill，这里不必清
            if (reason == DirectorStopReason.Replaced)
                return;
        }
        public override void Tick(float dt)
        {
            if (!_player.IsPlaying)
                return;
            _player.Tick(dt, Self, _tags);
            if (!_player.IsPlaying)
            {
                // 自然播完
                _currentSkill = SkillNodeId.None;
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
