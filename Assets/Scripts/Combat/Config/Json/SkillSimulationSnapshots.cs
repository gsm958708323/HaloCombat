using System.Collections.Generic;

namespace Combat.Config
{
    // Read models cross the host/Unity boundary without exposing mutable Actors or components.
    public sealed class SkillSimulationSnapshot
    {
        public int Step { get; internal set; }
        public float Time { get; internal set; }
        public SkillActorSnapshot[] Actors { get; internal set; }
        public SkillEventSnapshot[] Events { get; internal set; }
        public SkillTrailSnapshot[] Trails { get; internal set; }
        public SkillAreaSnapshot[] Areas { get; internal set; }
        public SkillSpatialSnapshot[] Volumes { get; internal set; }
        public SkillObstacleSnapshot[] Obstacles {get;internal set;}
        public SkillRaySnapshot[] Rays {get;internal set;}
        public float Energy { get; internal set; }
        public int Ammo {get;internal set;}
        public int AmmoCapacity {get;internal set;}
        public bool Channeling { get; internal set; }
        public bool Charging { get; internal set; }
        public float ChargeProgress { get; internal set; }
        public float CastPower { get; internal set; }
        public Dictionary<string, float> Cooldowns { get; internal set; }
        public SkillStatistics Statistics { get; internal set; }
    }

    public sealed class SkillActorSnapshot
    {
        public string Id { get; internal set; }
        public string SceneId {get;internal set;}
        public int Team {get;internal set;}
        public string Name { get; internal set; }
        public string Kind { get; internal set; }
        public string View {get;internal set;}
        public AuthorNodeSource Author {get;internal set;}
        public float ViewScale {get;internal set;}=1;
        public float Yaw {get;internal set;}
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public float Z { get; internal set; }
        public float? Hp { get; internal set; }
        public float? MaxHp { get; internal set; }
        public float Radius { get; internal set; }
        public float? Remaining { get; internal set; }
        public bool Dead { get; internal set; }
        public string Owner {get;internal set;}
        public string Blueprint {get;internal set;}
        public string Skill {get;internal set;}
        public float? Energy {get;internal set;}
        public int? Ammo {get;internal set;}
        public string TrackingKey {get;internal set;}
        public string Group {get;internal set;}
        public float? DamageCap {get;internal set;}
        public float? NextDecay {get;internal set;}
        public bool Stopped {get;internal set;}
        public int HitstopFrames {get;internal set;}
        public double LogicalTime {get;internal set;}
        public string HurtLevel {get;internal set;}
        public float? HurtAge {get;internal set;}
        public bool ComboConfirmed {get;internal set;}
        public string CurrentSkill {get;internal set;}
        public string BufferedInput {get;internal set;}
        public float? ActionTime {get;internal set;}
        public float? ActionDuration {get;internal set;}
        public float ActionRate {get;internal set;}
        public int[] ControlTags {get;internal set;}
        public int CastId {get;internal set;}
        public int Generation {get;internal set;}
        public int ChainsLeft {get;internal set;}
        public float? Distance {get;internal set;}
        public float? Range {get;internal set;}
        public float? Speed {get;internal set;}
        public float? VerticalSpeed {get;internal set;}
        public bool Returning {get;internal set;}
        public int GroundBouncesLeft {get;internal set;}
        public int WallBouncesLeft {get;internal set;}
        public int AbsorbedProjectiles {get;internal set;}
        public float? AreaVelocityX {get;internal set;}
        public float? AreaVelocityZ {get;internal set;}
        public SkillBuffSnapshot[] Buffs {get;internal set;}
        public Dictionary<int,int> Tags {get;internal set;}
        public Dictionary<string,float> Attributes {get;internal set;}
    }
    public sealed class SkillBuffSnapshot
    {
        public int InstanceId {get;internal set;}
        public string Id {get;internal set;}
        public string Name {get;internal set;}
        public string Source {get;internal set;}
        public int Stacks {get;internal set;}
        public float? Remaining {get;internal set;}
    }

    public sealed class SkillAreaSnapshot
    {
        public int Sequence { get; internal set; }
        public int Step { get; internal set; }
        public float X { get; internal set; }
        public float Z { get; internal set; }
        public float Radius { get; internal set; }
        public int Targets { get; internal set; }
    }

    public sealed class SkillSpatialSnapshot
    {
        public int Sequence { get; internal set; }
        public string Source { get; internal set; }
        public string Shape { get; internal set; }
        public float X { get; internal set; }
        public float Z { get; internal set; }
        public float Yaw { get; internal set; }
        public float Length { get; internal set; }
        public float Width { get; internal set; }
        public float Angle { get; internal set; }
    }

    public sealed class SkillObstacleSnapshot
    {
        public string Id {get;internal set;}
        public float X {get;internal set;}
        public float Z {get;internal set;}
        public float Width {get;internal set;}
        public float Depth {get;internal set;}
    }
    public sealed class SkillRaySnapshot
    {
        public int Sequence {get;internal set;}
        public int Step {get;internal set;}
        public float FromX {get;internal set;}
        public float FromZ {get;internal set;}
        public float ToX {get;internal set;}
        public float ToZ {get;internal set;}
        public float Width {get;internal set;}
        public int Hits {get;internal set;}
    }

    public sealed class SkillEventSnapshot
    {
        public int Sequence { get; internal set; }
        public float Time { get; internal set; }
        public string Kind { get; internal set; }
        public string Source { get; internal set; }
        public string Result { get; internal set; }
        public int Step {get;internal set;}
        public AuthorNodeSource Node {get;internal set;}
        public string SourceId {get;internal set;}
        public string TargetId {get;internal set;}
        public string BodyId {get;internal set;}
        public int ExecutionId {get;internal set;}
        public int ParentExecutionId {get;internal set;}
        public int CastId {get;internal set;}
        public string Skill {get;internal set;}
        public int HitIndex {get;internal set;}
        public SkillDamageSnapshot Damage {get;internal set;}
    }
    public sealed class SkillDamageSnapshot
    {
        public string Kind {get;internal set;}
        public string Target {get;internal set;}
        public float Base {get;internal set;}
        public float Scaled {get;internal set;}
        public float AfterDefense {get;internal set;}
        public float AfterCritical {get;internal set;}
        public float Final {get;internal set;}
        public float Absorbed {get;internal set;}
        public float HpLost {get;internal set;}
        public float Overkill {get;internal set;}
        public bool Killed {get;internal set;}
        public string Feedback {get;internal set;}
    }

    public sealed class SkillTrailSnapshot
    {
        public string Entity { get; internal set; }
        public float X { get; internal set; }
        public float Z { get; internal set; }
        public int Step { get; internal set; }
    }

    public sealed class SkillStatistics
    {
        public float Damage { get; internal set; }
        public int Hits { get; internal set; }
        public int Casts { get; internal set; }
        public int Projectiles { get; internal set; }
        public int Chains {get;internal set;}
        public int Splits {get;internal set;}
        public int Areas { get; internal set; }
        public int Summons {get;internal set;}
        public int Constructs {get;internal set;}
    }

    public sealed class SkillCastResult
    {
        public bool Accepted { get; }
        public string Reason { get; }
        internal SkillCastResult(bool accepted, string reason) { Accepted = accepted; Reason = reason; }
    }
}
