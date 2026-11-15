using System;
using Newtonsoft.Json;

namespace Combat.Config
{
    public sealed class RuntimeContentDocument
    {
        public RuntimeMotorDocument Motor {get;set;}=new RuntimeMotorDocument();
        public RuntimeComboDocument[] Combos {get;set;}=Array.Empty<RuntimeComboDocument>();
        public RuntimeActorDocument[] Actors {get;set;}=Array.Empty<RuntimeActorDocument>();
        public RuntimeCueDocument[] Cues {get;set;}=Array.Empty<RuntimeCueDocument>();
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public RuntimeViewDocument[] Views {get;set;}
        [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)]public ArenaDocument Arena {get;set;}
    }
    public sealed class RuntimeMotorDocument
    {
        public float Gravity {get;set;}=-20;
        public float JumpSpeed {get;set;}=6;
        public float AirSteer {get;set;}=.35f;
        public float GroundY {get;set;}
        public float StickDeadzone {get;set;}=.25f;
    }
    public sealed class RuntimeComboDocument
    {
        public string Id {get;set;}
        public string[] PreSkills {get;set;}=Array.Empty<string>();
        public string Input {get;set;}="Attack";
        public int[] RequiredTags {get;set;}=Array.Empty<int>();
        public int Priority {get;set;}
        public string ToSkill {get;set;}
    }
    public sealed class RuntimeActorDocument
    {
        public string Id {get;set;}
        public string Role {get;set;}="target";
        public int Team {get;set;}=2;
        public float Hp {get;set;}=100;
        public float Attack {get;set;}=10;
        public float Defense {get;set;}
        public float MoveSpeed {get;set;}=5;
        [System.ComponentModel.DefaultValue(1f),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)]public float ActionSpeed {get;set;}=1;
        public float Shield {get;set;}
        public float CritRate {get;set;}
        public float DamageDealt {get;set;}=1;
        public float DamageTaken {get;set;}=1;
        public float CollisionRadius {get;set;}
        public float Energy {get;set;}=100;
        public float Recovery {get;set;}=10;
        [System.ComponentModel.DefaultValue(0),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public int Ammo {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public int? InitialAmmo {get;set;}
        public string SkillId {get;set;}
        public string DodgeSkill {get;set;}
        public string[] Slots {get;set;}=Array.Empty<string>();
        public string Ai {get;set;}="none";
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public WanderDocument Wander {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public ActorSpawnStatsDocument SpawnStats {get;set;}
        public float AcquireRadius {get;set;}=8;
        public float AttackRange {get;set;}=1.15f;
        public float FollowRange {get;set;}=1.5f;
        public float LeashRange {get;set;}=20;
        public float PatrolRadius {get;set;}
        public string View {get;set;}
    }
    public sealed class RuntimeCueDocument
    {
        public int Id {get;set;}
        public string Key {get;set;}
        public string Kind {get;set;}="cube";
        public string Resource {get;set;}
        public string Sound {get;set;}
        public float Lifetime {get;set;}=.45f;
    }
}
