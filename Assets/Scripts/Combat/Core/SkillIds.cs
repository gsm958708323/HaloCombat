using System;

namespace Combat.Core
{
    public struct SkillNodeId : IEquatable<SkillNodeId>
    {
        public static SkillNodeId None = new SkillNodeId(0);

        public int Value;

        public SkillNodeId(int value) => Value = value;

        public bool IsValid => Value != 0;

        public bool Equals(SkillNodeId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SkillNodeId id && Equals(id);
        public override int GetHashCode() => Value;
        public static bool operator ==(SkillNodeId a, SkillNodeId b) => a.Equals(b);
        public static bool operator !=(SkillNodeId a, SkillNodeId b) => !a.Equals(b);
        public override string ToString() => $"Skill({Value})";

        // MVP 常用节点（正式项目改成配置表生成）
        public static SkillNodeId G1 = new SkillNodeId(101);
        public static SkillNodeId G2 = new SkillNodeId(102);
        public static SkillNodeId AirUp = new SkillNodeId(201);
    }

    public struct TimelineId : IEquatable<TimelineId>
    {
        public static TimelineId None = new TimelineId(0);
        public int Value;
        public TimelineId(int value) => Value = value;
        public bool IsValid => Value != 0;
        public bool Equals(TimelineId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TimelineId id && Equals(id);
        public override int GetHashCode() => Value;
        public static bool operator ==(TimelineId a, TimelineId b) => a.Equals(b);
        public static bool operator !=(TimelineId a, TimelineId b) => !a.Equals(b);

        public static TimelineId TL_G1 = new TimelineId(1001);
        public static TimelineId TL_G2 = new TimelineId(1002);
    }
}
