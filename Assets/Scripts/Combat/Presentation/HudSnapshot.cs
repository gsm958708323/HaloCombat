using Combat.Core;

namespace Combat.Presentation
{
    public struct HudSnapshot
    {
        public bool Valid;
        public float Hp,
            MaxHp,
            Shield;
        public int BurnStacks,
            AuraSlowStacks;
        public bool Cancel,
            IFrame,
            Downed,
            Dead,
            Hitstop;
        public int SkillId;
        public bool Casting;
        public float Hp01
        {
            get
            {
                if (MaxHp <= 1e-5f)
                    return 0f;
                var t = Hp / MaxHp;
                return t < 0f ? 0f
                    : t > 1f ? 1f
                    : t;
            }
        }

        public static HudSnapshot Capture(Actor a, bool hitstop)
        {
            var s = default(HudSnapshot);
            if (a == null || !a.IsActive)
                return s;
            s.Valid = true;
            s.Hitstop = hitstop;
            if (a.TryGetComp<AttributeSet>(out var at))
            {
                s.Hp = at.GetBase(AttrId.Hp);
                s.MaxHp = at.GetFinal(AttrId.MaxHp);
                s.Shield = at.GetBase(AttrId.Shield);
            }
            if (a.TryGetComp<BuffComp>(out var b))
            {
                s.BurnStacks = b.StacksOf(CombatIds.Burn);
                s.AuraSlowStacks = b.StacksOf(CombatIds.AuraSlow);
            }
            if (a.TryGetComp<TagComp>(out var t))
            {
                s.Cancel = t.Has(CommonTags.Cancel);
                s.IFrame = t.Has(CommonTags.Invincible);
                s.Downed = t.Has(CommonTags.Downed);
                s.Dead = t.Has(CommonTags.Dead);
                s.Casting = t.Has(CommonTags.Casting);
            }
            if (a.TryGetComp<StateMachineComp>(out var sm))
            {
                s.Dead |= sm.Current == ActivityId.Dead;
                s.Downed |= sm.Current == ActivityId.Knockdown;
            }
            if (a.TryGetComp<SkillDirectorComp>(out var d) && d.IsPlaying)
                s.SkillId = d.CurrentSkill.Value;
            return s;
        }

        public bool SameAs(HudSnapshot o) =>
            Valid == o.Valid
            && Hp == o.Hp
            && MaxHp == o.MaxHp
            && Shield == o.Shield
            && BurnStacks == o.BurnStacks
            && AuraSlowStacks == o.AuraSlowStacks
            && Cancel == o.Cancel
            && IFrame == o.IFrame
            && Downed == o.Downed
            && Dead == o.Dead
            && Hitstop == o.Hitstop
            && SkillId == o.SkillId
            && Casting == o.Casting;
    }

    public sealed class PlayerHudSourcePresent : PresentComp
    {
        public override bool WantsLogicSync => true;
        public HudSnapshot Snapshot { get; private set; }
        public bool Dirty { get; private set; }

        public override void SyncLogic(CombatWorld world)
        {
            var next = Self.TryLogic(world, out var a)
                ? HudSnapshot.Capture(a, world.InHitstop)
                : default(HudSnapshot);
            Dirty = !next.SameAs(Snapshot);
            Snapshot = next;
        }

        protected override void OnDetach()
        {
            Snapshot = default(HudSnapshot);
            Dirty = true;
        }
    }
}
