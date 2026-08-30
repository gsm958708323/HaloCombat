using System;

namespace Combat.Core
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId Invalid = new EntityId(0, 0);

        public readonly int Index;
        public readonly int Generation;

        public EntityId(int index, int generation)
        {
            Index = index;
            Generation = generation;
        }

        public bool IsValid => Index > 0 && Generation > 0;

        public bool Equals(EntityId other)
            => Index == other.Index && Generation == other.Generation;

        public override bool Equals(object obj) => obj is EntityId e && Equals(e);
        public override int GetHashCode() => unchecked((Index * 397) ^ Generation);
        public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
        public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
        public override string ToString()
            => IsValid ? ("Entity(" + Index + ":" + Generation + ")") : "Entity(Invalid)";
    }

    public struct SimVec3
    {
        public float X, Y, Z;

        public SimVec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static SimVec3 Zero => new SimVec3(0f, 0f, 0f);

        public static SimVec3 operator +(SimVec3 a, SimVec3 b)
            => new SimVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    }

    public readonly struct TagId : IEquatable<TagId>
    {
        public readonly int Value;
        public TagId(int value) => Value = value;
        public bool Equals(TagId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TagId t && Equals(t);
        public override int GetHashCode() => Value;
        public static bool operator ==(TagId a, TagId b) => a.Value == b.Value;
        public static bool operator !=(TagId a, TagId b) => a.Value != b.Value;
        public override string ToString() => "Tag(" + Value + ")";
    }

    public readonly struct TagSource
    {
        public readonly string Reason;
        public TagSource(string reason) => Reason = reason ?? string.Empty;
        public static TagSource StateEnter(string name) => new TagSource("StateEnter:" + name);
        public static TagSource StateExit(string name) => new TagSource("StateExit:" + name);
        public static TagSource Effect(string name) => new TagSource("Effect:" + name);
        public static TagSource Debug => new TagSource("Debug");
    }

    public static class CommonTags
    {
        public static readonly TagId Cancel = new TagId(1001);
        public static readonly TagId Grounded = new TagId(1002);
        public static readonly TagId Airborne = new TagId(1003);
        public static readonly TagId SuperArmor = new TagId(1004);
        public static readonly TagId Dead = new TagId(1005);
        public static readonly TagId Casting = new TagId(1006);
        public static readonly TagId Stunned = new TagId(1007);
        public static readonly TagId Silence = new TagId(1008);
        public static readonly TagId Invincible = new TagId(1009);
        public static readonly TagId Downed = new TagId(1010);
    }

    public readonly struct SkillNodeId : IEquatable<SkillNodeId>
    {
        public static readonly SkillNodeId None = new SkillNodeId(0);
        public static readonly SkillNodeId G1 = new SkillNodeId(101);
        public static readonly SkillNodeId G2 = new SkillNodeId(102);
        public static readonly SkillNodeId Ranged = new SkillNodeId(201);
        public static readonly SkillNodeId Dodge = new SkillNodeId(301);
        public readonly int Value;
        public SkillNodeId(int value) => Value = value;
        public bool IsValid => Value != 0;
        public bool Equals(SkillNodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SkillNodeId s && Equals(s);
        public override int GetHashCode() => Value;
        public static bool operator ==(SkillNodeId a, SkillNodeId b) => a.Equals(b);
        public static bool operator !=(SkillNodeId a, SkillNodeId b) => !a.Equals(b);
        public override string ToString() => "Skill(" + Value + ")";
    }

    public readonly struct TimelineId : IEquatable<TimelineId>
    {
        public static readonly TimelineId None = new TimelineId(0);
        public static readonly TimelineId TL_G1 = new TimelineId(1001);
        public static readonly TimelineId TL_G2 = new TimelineId(1002);
        public static readonly TimelineId TL_Dodge = new TimelineId(1003);
        public static readonly TimelineId TL_Homing = new TimelineId(1004);
        public readonly int Value;
        public TimelineId(int value) => Value = value;
        public bool IsValid => Value != 0;
        public bool Equals(TimelineId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TimelineId t && Equals(t);
        public override int GetHashCode() => Value;
        public static bool operator ==(TimelineId a, TimelineId b) => a.Equals(b);
        public static bool operator !=(TimelineId a, TimelineId b) => !a.Equals(b);
    }

    public readonly struct InputToken : IEquatable<InputToken>
    {
        public static readonly InputToken Attack = new InputToken("Attack");
        public static readonly InputToken Jump = new InputToken("Jump");
        public static readonly InputToken UpAttack = new InputToken("UpAttack");
        public readonly string Action;
        public InputToken(string action) => Action = action ?? string.Empty;
        public bool IsValid => !string.IsNullOrEmpty(Action);
        public bool Equals(InputToken other)
            => string.Equals(Action, other.Action, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is InputToken t && Equals(t);
        public override int GetHashCode()
            => Action != null ? StringComparer.Ordinal.GetHashCode(Action) : 0;
        public static bool operator ==(InputToken a, InputToken b) => a.Equals(b);
        public static bool operator !=(InputToken a, InputToken b) => !a.Equals(b);
        public override string ToString() => Action;
    }

    public static class Season2Tokens
    {
        public static readonly InputToken Dodge = new InputToken("Dodge");
    }
    public static class Season2Contracts
    {
        public const bool AiMayStopDirector = false;
        public const bool CloneTreePerActor = true;
        public static void EnsureAiMustNotStopDirector()
        {
            if (AiMayStopDirector) throw new InvalidOperationException("AI must not Director.Stop");
        }
    }
    public static class CombatIds
    {
        public const int Burn = 1, AuraSlow = 2;
        public const int Fireball = 901, HomingBolt = 902;
        public const int FireGround = 801, AuraField = 802;
        public const int MeleeSummon = 701;
        public const int CueFireballHit = 201, CueFireGround = 202;
    }
}
