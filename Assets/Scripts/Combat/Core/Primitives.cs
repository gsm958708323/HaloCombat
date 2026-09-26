using System;

namespace Combat.Core
{
    /// <summary>
    /// 实体的强标识：Index 是对象槽位（池化会复用），Generation 是该槽位被复用的代数。
    /// 只带 Index 的旧 id 在槽位换主后会指向错误对象，所以相等性必须同时比较 Generation；
    /// 槽位复用时必须递增 Generation，旧 id 才会自动失效。Invalid 为 (0,0)，Index 与 Generation 都 &gt; 0 才算有效。
    /// </summary>
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public static readonly EntityId Invalid = new EntityId(0, 0);

        public readonly int Index;         // 槽位索引
        public readonly int Generation;    // 槽位复用代数：旧 id 靠它失效

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

    /// <summary>
    /// 模拟层三维向量（可变 struct，值语义）。逻辑代码只用它交换位置，不依赖 UnityEngine.Vector3，
    /// 以保证核心可脱离引擎测试；只提供 Zero 与加法，缺的运算按需再加，避免逻辑层悄悄依赖完整数学库。
    /// </summary>
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

    /// <summary>
    /// 标签的强类型包装（值语义，相等性只看 Value）。用 struct 包一层是为了防止 TagId 与普通 int 或其他 id 混用。
    /// </summary>
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

    /// <summary>
    /// 标签来源的可读说明，仅用于排查“这个标签是谁加的”。它不参与任何逻辑判断，因此文案可以自由改写。
    /// </summary>
    public readonly struct TagSource
    {
        public readonly string Reason;
        public TagSource(string reason) => Reason = reason ?? string.Empty;
        public static TagSource StateEnter(string name) => new TagSource("StateEnter:" + name);
        public static TagSource StateExit(string name) => new TagSource("StateExit:" + name);
        public static TagSource Effect(string name) => new TagSource("Effect:" + name);
        public static TagSource Debug => new TagSource("Debug");
    }

    /// <summary>
    /// 跨玩法共享的内置标签 id。数值即身份，改动等于改语义，不要重排这些常量。
    /// </summary>
    public static class CommonTags
    {
        public static readonly TagId BlockMove = new TagId(1011);
        public static readonly TagId BlockRotate = new TagId(1012);
        public static readonly TagId BlockSkill = new TagId(1013);
        public static readonly TagId BlockSkillMotion = new TagId(1014);
        public static readonly TagId BlockHitMotion = new TagId(1015);
        public static readonly TagId BlockGravity = new TagId(1016);
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

    /// <summary>
    /// 技能节点标识（值语义，相等性只看 Value）。None=0 表示“无技能”，所以 IsValid 用 Value != 0 判定。
    /// 它是不透明 id，只用于在技能表、时间轴与 Director 之间对齐，不表达技能树结构。
    /// </summary>
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

    /// <summary>
    /// 技能动画的表现分类。底层用 byte 是为了紧凑布局；数值是契约，不要重排（表现层按值查表）。
    /// </summary>
    public enum SkillAnimationMode : byte
    {
        Attack = 0,
        AirAttack = 1,
        Dash = 2,
        Slide = 3,
        Shot = 4
    }

    /// <summary>
    /// 时间轴标识（值语义，相等性只看 Value）。None=0 表示“无时间轴”；TL_* 是内置固定 id，
    /// 具体技能实际绑定的时间轴由技能资产提供（玩家技能的归属见 BuffArenaIds）。
    /// </summary>
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

    /// <summary>
    /// 输入意图 token（字符串值语义，Ordinal 比较）。契约：它必须与技能资产上的 InputToken 字段逐字相等——
    /// BuffArenaSession.ApplyInput 把按键映射到 token，BuffArenaData.Skills 的 Input 再与之匹配。
    /// 坑：改名即改契约；对不上时 FindByInput 返回 null、这次输入被静默丢弃，不会报错。
    /// </summary>
    public readonly struct InputToken : IEquatable<InputToken>
    {
        public static readonly InputToken Attack = new InputToken("Attack");
        public static readonly InputToken Skill1 = new InputToken("Skill1");
        public static readonly InputToken Skill2 = new InputToken("Skill2");
        public static readonly InputToken Skill3 = new InputToken("Skill3");
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

    /// <summary>
    /// 第二赛季新增的输入 token，与代码拥有的按键映射一一对应（同样受 InputToken 的匹配契约约束）。
    /// </summary>
    public static class Season2Tokens
    {
        public static readonly InputToken Dodge = new InputToken("Dodge");
    }
    /// <summary>
    /// 第二赛季的行为契约常量：这些 bool 表达的是设计约束而非可调参数，所以用 const 固定，并在启动期显式断言。
    /// </summary>
    public static class Season2Contracts
    {
        public const bool AiMayStopDirector = false;
        public const bool CloneTreePerActor = true;
        /// <summary>启动期断言：AI 不得停止 Director；约束一旦被改回 true 就在这里立刻失败，而不是留到运行期。</summary>
        public static void EnsureAiMustNotStopDirector()
        {
            if (AiMayStopDirector) throw new InvalidOperationException("AI must not Director.Stop");
        }
    }
    /// <summary>
    /// 源工程对照用的战斗 id（buff / 弹道 / AoE / cue）。这些数值是内容身份，用于和迁移前工程逐条比对；
    /// 改动前必须确认对应的 SO 资产同步改了。
    /// </summary>
    public static class CombatIds
    {
        public const int Burn = 1, AuraSlow = 2;
        public const int Fireball = 901, HomingBolt = 902;
        public const int FireGround = 801, AuraField = 802;
        public const int MeleeSummon = 701;
        public const int CueFireballHit = 201, CueFireGround = 202;
    }
}
