using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace Combat.Config
{
    public sealed class SkillProjectDocument
    {
        [JsonProperty(Required = Required.Always)] public int FormatVersion { get; set; } = 1;
        [JsonProperty(Required = Required.Always)] public string Id { get; set; }
        public string Name { get; set; }
        public SkillGraphDocument[] Skills { get; set; } = Array.Empty<SkillGraphDocument>();
        public SummonBlueprintDocument[] SummonBlueprints {get;set;}=Array.Empty<SummonBlueprintDocument>();
        public bool ShouldSerializeSummonBlueprints()=>SummonBlueprints==null||SummonBlueprints.Length>0;
        public BuffDocument[] Buffs {get;set;}=Array.Empty<BuffDocument>();
        public bool ShouldSerializeBuffs()=>Buffs==null||Buffs.Length>0;
        public PreviewDocument Preview { get; set; } = new PreviewDocument();
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public SkillEditorDocument Editor {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public RuntimeContentDocument Runtime {get;set;}
    }
    public sealed class SkillGraphDocument
    {
        public string Id { get; set; }
        public int RuntimeId { get; set; }
        public string Name { get; set; }
        public string Category { get; set; } = "自定义";
        public SkillNodeDocument[] Nodes { get; set; } = Array.Empty<SkillNodeDocument>();
        public SkillEdgeDocument[] Edges { get; set; } = Array.Empty<SkillEdgeDocument>();
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public TimelineDocument Timeline {get;set;}
    }
    public sealed class SkillNodeDocument
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public int RuntimeId { get; set; }
        public bool Enabled { get; set; } = true;
        public NodePosition Position { get; set; } = new NodePosition();
        public JObject Parameters { get; set; } = new JObject();
    }
    public sealed class NodePosition
    {
        public float X { get; set; }
        public float Y { get; set; }
    }
    public sealed class SkillEdgeDocument
    {
        public string Id { get; set; }
        public string Source { get; set; }
        public string SourcePort { get; set; }
        public string Target { get; set; }
        public string TargetPort { get; set; }
        public int Order { get; set; }
    }
    public sealed class PreviewDocument
    {
        public int Seed { get; set; } = 1;
        public float Energy { get; set; } = 100;
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public float? InitialEnergy { get; set; }
        public float Recovery { get; set; } = 10;
        [DefaultValue(60),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public int Ammo {get;set;}=60;
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)] public int? InitialAmmo {get;set;}
        public PreviewActorDocument Caster { get; set; } = new PreviewActorDocument { Id = "caster", X = -6 };
        public PreviewActorDocument[] Targets { get; set; } = Array.Empty<PreviewActorDocument>();
        public PreviewObstacleDocument[] Obstacles { get; set; } = Array.Empty<PreviewObstacleDocument>();
        public bool ShouldSerializeObstacles()=>Obstacles==null||Obstacles.Length>0;
    }
    public sealed class PreviewActorDocument
    {
        public string Id { get; set; }
        public float X { get; set; }
        public float Z { get; set; }
        public float Hp { get; set; } = 1000;
        public float Atk { get; set; } = 80;
        public float Def { get; set; }
        [DefaultValue(0f),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)]public float Shield {get;set;}
        [DefaultValue(1f),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)]public float DamageDealt {get;set;}=1;
        [DefaultValue(1f),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)]public float DamageTaken {get;set;}=1;
        [DefaultValue(1f),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)]public float ActionSpeed {get;set;}=1;
        [DefaultValue(.4f),JsonProperty(DefaultValueHandling=DefaultValueHandling.Ignore)] public float CollisionRadius { get; set; } = .4f;
    }
    public sealed class PreviewObstacleDocument
    {
        public string Id {get;set;}
        public float X {get;set;}
        public float Z {get;set;}
        public float Width {get;set;} = .5f;
        public float Depth {get;set;} = 3;
    }
    public sealed class ContentValidationException : Exception
    {
        public string Path { get; }
        public ContentValidationException(string path, string reason) : base(path + ": " + reason) => Path = path;
    }
}
