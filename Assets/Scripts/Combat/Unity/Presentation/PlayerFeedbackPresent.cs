using Combat.Core;
using UnityEngine;

namespace Combat.Unity.Presentation
{
    public static class PresentFxIds
    {
        public const int DodgeGhost = 9101;
    }

    public sealed class PlayerFeedbackPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;
        public float Flash { get; private set; }
        public float Rumble { get; private set; }
        public float HitImpulse { get; private set; }
        public HitFeedbackLevel LastFeedback { get; private set; }
        public bool GhostOn { get; private set; }
        public int GhostHandle { get; private set; }
        readonly LoopVfxController _ghost;
        bool _wanted;

        public PlayerFeedbackPresent(Transform vfxRoot) => _ghost = new LoopVfxController(vfxRoot);

        public void OnHurt(in EvDamage e)
        {
            if (e.Amount <= 0f && !e.IsKill && e.ShieldAbsorb <= 0f)
                return;
            Flash = 1f;
            Rumble = 0.35f;
            LastFeedback = e.Feedback;
            HitImpulse = Mathf.Max(HitImpulse, ImpulseFor(e.Feedback));
        }

        public void OnImmune(in EvImmune e)
        {
            Flash = 0.35f;
            Rumble = 0.12f;
            LastFeedback = HitFeedbackLevel.Light;
        }

        public override void SyncLogic(CombatWorld world)
        {
            _wanted = false;
            if (!Self.TryLogic(world, out var actor))
            {
                StopGhost();
                return;
            }
            bool iframe =
                actor.TryGetComp<TagComp>(out var tags) && tags.Has(CommonTags.Invincible);
            bool dodge =
                actor.TryGetComp<SkillDirectorComp>(out var d)
                && d.IsPlaying
                && d.CurrentSkill == SkillNodeId.Dodge;
            _wanted = iframe && dodge;
            SyncGhost();
        }

        public override void LateTick(float dt)
        {
            if (Flash > 0f)
            {
                Flash -= dt / 0.18f;
                if (Flash < 0f)
                    Flash = 0f;
            }
            if (Rumble > 0f)
            {
                Rumble -= dt;
                if (Rumble < 0f)
                    Rumble = 0f;
            }
            if (HitImpulse > 0f)
            {
                HitImpulse -= dt * 3.5f;
                if (HitImpulse < 0f) HitImpulse = 0f;
            }
        }

        void SyncGhost()
        {
            if (!_wanted)
            {
                StopGhost();
                return;
            }
            if (!GhostOn)
            {
                GhostHandle = _ghost.PlayLoop(PresentFxIds.DodgeGhost);
                GhostOn = GhostHandle != 0;
            }
            if (GhostOn)
                _ghost.SetIntensity(GhostHandle, 1);
        }

        void StopGhost()
        {
            if (!GhostOn)
                return;
            _ghost.Stop(GhostHandle);
            GhostOn = false;
            GhostHandle = 0;
        }

        protected override void OnDetach()
        {
            StopGhost();
            _ghost.Release();
            Flash = 0f;
            Rumble = 0f;
            HitImpulse = 0f;
            LastFeedback = HitFeedbackLevel.None;
        }

        static float ImpulseFor(HitFeedbackLevel level)
        {
            switch (level)
            {
                case HitFeedbackLevel.ComboConfirm: return .65f;
                case HitFeedbackLevel.Heavy: return .9f;
                case HitFeedbackLevel.Finisher: return 1.2f;
                case HitFeedbackLevel.Light: return .35f;
                default: return 0f;
            }
        }
    }
}
