using Combat.Core;

namespace Combat.Presentation
{
    public struct AnimFlags
    {
        public float Speed;
        public bool Grounded,
            InAir,
            Attack,
            Hit,
            Downed,
            Dead,
            IFrame;
        public int SkillId;
        public bool Hitstop;

        public static AnimFlags Capture(Actor a, bool hitstop)
        {
            var f = default(AnimFlags);
            if (a == null)
                return f;
            f.Hitstop = hitstop;
            if (a.TryGetComp<TagComp>(out var tags))
            {
                f.Grounded = tags.Has(CommonTags.Grounded);
                f.InAir = tags.Has(CommonTags.Airborne);
                f.Downed = tags.Has(CommonTags.Downed);
                f.Dead = tags.Has(CommonTags.Dead);
                f.IFrame = tags.Has(CommonTags.Invincible);
            }
            if (a.TryGetComp<StateMachineComp>(out var sm))
            {
                f.Attack = sm.Current == ActivityId.Attack;
                f.Hit = sm.Current == ActivityId.Hit;
                f.Dead |= sm.Current == ActivityId.Dead;
                f.Downed |= sm.Current == ActivityId.Knockdown;
            }
            if (a.TryGetComp<SkillDirectorComp>(out var dir) && dir.IsPlaying)
                f.SkillId = dir.CurrentSkill.Value;
            if (a.TryGetComp<LocomotionComp>(out var loco))
            {
                float m = LocomotionComp.StickMag(loco.MoveIntent);
                f.Speed = m > 1f ? 1f : m;
            }
            return f;
        }
    }

    public sealed class AnimationPresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public override bool WantsLateTick => true;
        public AnimFlags Flags { get; private set; }
        public AnimFlags Applied { get; private set; }
        public bool DeathPlayed { get; private set; }

        public override void SyncLogic(CombatWorld world)
        {
            if (Self.TryLogic(world, out var a))
                Flags = AnimFlags.Capture(a, world.InHitstop);
        }

        public override void LateTick(float dt)
        {
            Applied = Flags;
        }

        public void PlayDeath() => DeathPlayed = true;

        protected override void OnDetach()
        {
            Flags = default;
            Applied = default;
            DeathPlayed = false;
        }
    }
}
