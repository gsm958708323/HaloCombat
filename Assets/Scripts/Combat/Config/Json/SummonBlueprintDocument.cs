namespace Combat.Config
{
    public sealed class SummonBlueprintDocument
    {
        public string Id {get;set;}
        public string Name {get;set;}
        public string SkillId {get;set;}
        public float Hp {get;set;}=100;
        public float Attack {get;set;}=80;
        public float Defense {get;set;}
        public float MoveSpeed {get;set;}=3;
        [System.ComponentModel.DefaultValue(1f),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)]public float ActionSpeed {get;set;}=1;
        public float CollisionRadius {get;set;}=.4f;
        public float Energy {get;set;}=100;
        public float? InitialEnergy {get;set;}
        public float Recovery {get;set;}=10;
        [System.ComponentModel.DefaultValue(0),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)] public int Ammo {get;set;}
        [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)] public int? InitialAmmo {get;set;}
        public float AcquireRadius {get;set;}=20;
        public float AttackRange {get;set;}=12;
        public float FollowRange {get;set;}=2;
        public float HoldDuration {get;set;}=.75f;
    }
}
