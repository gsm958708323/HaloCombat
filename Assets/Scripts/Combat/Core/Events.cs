namespace Combat.Core
{
    public enum HitFeedbackLevel : byte { None, Light, ComboConfirm, Heavy, Finisher }

    public static class HitFeedbackRules
    {
        public static HitFeedbackLevel ForSkill(SkillNodeId skill)
        {
            switch (skill.Value)
            {
                case 2001: return HitFeedbackLevel.Light;
                case 2012: return HitFeedbackLevel.ComboConfirm;
                case 2013: return HitFeedbackLevel.Finisher;
                case 2003:
                case 2005:
                case 2007:
                case 2008: return HitFeedbackLevel.Heavy;
                default: return HitFeedbackLevel.None;
            }
        }

        public static int TargetFrames(HitFeedbackLevel level, int hitIndex = 0)
        {
            switch (level)
            {
                case HitFeedbackLevel.Finisher: return 4;
                case HitFeedbackLevel.Heavy: return 3;
                case HitFeedbackLevel.ComboConfirm: return hitIndex >= 2 ? 3 : 2;
                case HitFeedbackLevel.Light: return 2;
                default: return 0;
            }
        }

        public static int SourceFrames(HitFeedbackLevel level)
        {
            switch (level)
            {
                case HitFeedbackLevel.Finisher: return 2;
                case HitFeedbackLevel.ComboConfirm: return 1;
                case HitFeedbackLevel.Light: return 1;
                default: return 0;
            }
        }
    }

    public readonly struct CastId : System.IEquatable<CastId>
    {
        public readonly EntityId Owner;
        public readonly int Sequence;
        public bool IsValid => Owner.IsValid && Sequence > 0;
        public CastId(EntityId owner, int sequence) { Owner = owner; Sequence = sequence; }
        public bool Equals(CastId other) => Owner == other.Owner && Sequence == other.Sequence;
        public override bool Equals(object obj) => obj is CastId other && Equals(other);
        public override int GetHashCode() => HitboxComp.Pack(Owner).GetHashCode() ^ Sequence;
        public static bool operator ==(CastId left, CastId right) => left.Equals(right);
        public static bool operator !=(CastId left, CastId right) => !left.Equals(right);
    }

    public readonly struct EvEntitySpawn
    {
        public readonly EntityId Id;
        public readonly string BlueprintId;
        public readonly EntityId Owner;
        public readonly string ViewBlueprintId;

        public EvEntitySpawn(EntityId id, string blueprintId, EntityId owner, string viewBlueprintId = null)
        {
            Id = id;
            BlueprintId = blueprintId ?? string.Empty;
            Owner = owner;
            ViewBlueprintId = viewBlueprintId ?? string.Empty;
        }
    }

    public readonly struct EvCue
    {
        public readonly int CueId;
        public readonly EntityId Source;
        public readonly string Name;
        public readonly EntityId Target;
        public readonly SimVec3 Point;
        public readonly bool HasPoint;
        public readonly string AnchorKey;
        public readonly string InstanceKey;
        public readonly bool Loop;
        public readonly bool Stop;

        public EvCue(int cueId, EntityId source, string name)
            : this(cueId, source, name, EntityId.Invalid, SimVec3.Zero, false, string.Empty, string.Empty, false, false)
        {
        }

        public EvCue(int cueId, EntityId source, string name, EntityId target, in SimVec3 point,
            bool hasPoint, string anchorKey, string instanceKey, bool loop, bool stop)
        {
            CueId = cueId;
            Source = source;
            Name = name ?? string.Empty;
            Target = target;
            Point = point;
            HasPoint = hasPoint;
            AnchorKey = anchorKey ?? string.Empty;
            InstanceKey = instanceKey ?? string.Empty;
            Loop = loop;
            Stop = stop;
        }
    }

    public readonly struct EvHurt
    {
        public readonly EntityId Target;
        public readonly HitFeedbackLevel Feedback;
        public EvHurt(EntityId target, HitFeedbackLevel feedback = HitFeedbackLevel.Light)
        {
            Target = target;
            Feedback = feedback;
        }
    }

    public readonly struct EvGameplayMessage
    {
        public readonly EntityId Target;
        public readonly string Text;
        public EvGameplayMessage(EntityId target, string text)
        {
            Target = target;
            Text = text ?? string.Empty;
        }
    }

    public readonly struct EvDamage
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly float Amount;
        public readonly bool IsCrit;
        public readonly float ShieldAbsorb;
        public readonly bool IsKill;
        public readonly CastId CastId;
        public readonly SkillNodeId Skill;
        public readonly HitFeedbackLevel Feedback;
        public readonly int HitIndex;
        public EvDamage(EntityId source, EntityId target, float amount, bool isCrit, float shieldAbsorb, bool isKill,
            CastId castId = default, SkillNodeId skill = default, HitFeedbackLevel feedback = HitFeedbackLevel.None,
            int hitIndex = 0)
        {
            Source = source;
            Target = target;
            Amount = amount;
            IsCrit = isCrit;
            ShieldAbsorb = shieldAbsorb;
            IsKill = isKill;
            CastId = castId;
            Skill = skill;
            Feedback = feedback;
            HitIndex = hitIndex;
        }
    }

    public readonly struct EvImmune
    {
        public readonly EntityId Target;
        public readonly EntityId Source;
        public EvImmune(EntityId target, EntityId source)
        {
            Target = target;
            Source = source;
        }
    }

    public readonly struct EvHitstop
    {
        public readonly EntityId Source;
        public readonly EntityId Target;
        public readonly int SourceFrames;
        public readonly int TargetFrames;
        public int LogicFrames => System.Math.Max(SourceFrames, TargetFrames);
        public int Frames => LogicFrames;
        public bool FreezeSource => SourceFrames > 0;
        public bool FreezeTarget => TargetFrames > 0;
        public EvHitstop(EntityId source, EntityId target, int sourceFrames, int targetFrames)
        {
            Source = source; Target = target;
            SourceFrames = sourceFrames; TargetFrames = targetFrames;
        }
    }

    public readonly struct EvHeal
    {
        public readonly EntityId Target;
        public readonly float Amount;
        public EvHeal(EntityId target, float amount)
        {
            Target = target;
            Amount = amount;
        }
    }

    public readonly struct EvEntityDead
    {
        public readonly EntityId Id;
        public readonly EntityId Killer;
        public EvEntityDead(EntityId id, EntityId killer)
        {
            Id = id;
            Killer = killer;
        }
    }

    public readonly struct EvEntityCleanup
    {
        public readonly EntityId Id;
        public readonly string Reason;
        public EvEntityCleanup(EntityId id, string reason)
        {
            Id = id;
            Reason = reason ?? string.Empty;
        }
    }
}
