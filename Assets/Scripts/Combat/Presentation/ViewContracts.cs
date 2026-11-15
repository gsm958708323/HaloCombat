using Combat.Core;

namespace Combat.Presentation
{
    public struct PoseSample
    {
        public EntityId Id;
        public SimVec3 LogicPos;
        public float YawDeg;
        public bool Grounded;
        public ActivityId Activity;
        public SkillNodeId Skill;
        public bool InHitstop;
        public float Alpha;
        public float ActionTime,ActionDuration,ActionRate;
        public string Animation;
        public long CastId;
        public float ActorTime;
        public SimVec3 MoveIntent;
        public bool MoveBlocked;
        public bool PresentationPaused;
        public float BodyScale;
    }

    public interface IActorView
    {
        EntityId Id { get; }
        void Bind(EntityId id, string blueprintId);
        void Sample(in PoseSample sample);
        void OnDead(in EvEntityDead e);
        void OnHurt(in EvHurt e);
        void Release();
    }

    public interface IViewFactory
    {
        IActorView Create(string blueprintId);
    }

    public interface ICuePlayer { void Play(EvCue e); }
    public interface IFloaterPlayer { void Play(EvDamage e); void PlayImmune(EvImmune e); }
    public interface IHitstopOverlay { void ShowFlash(); void SetActive(bool on); }
    public interface IDebugOverlay { bool Enabled { get; set; } void Refresh(CombatWorld world); }
}
