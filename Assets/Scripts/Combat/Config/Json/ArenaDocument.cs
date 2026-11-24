using System;

namespace Combat.Config
{
    public sealed class ArenaDocument
    {
        public string Player {get;set;}
        public string Enemy {get;set;}
        public string TrainingTarget {get;set;}
        public int Seed {get;set;}=1;
        public int MaxEnemies {get;set;}=10;
        public float SpawnPeriod {get;set;}=10;
        public float CleanupDelay {get;set;}=5;
        public float HalfWidth {get;set;}=16;
        public float HalfDepth {get;set;}=12;
        public float SpawnDistance {get;set;}=4;
        public float PlayerX {get;set;}
        public float PlayerZ {get;set;}
        public float PlayerYaw {get;set;}
        public float CameraDistance {get;set;}=2.5f;
        public float CameraHeight {get;set;}=2.5f;
        public float CameraFov {get;set;}=60;
        public float CameraFocusHeight {get;set;}=.75f;
        [System.ComponentModel.DefaultValue(.55f),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)]public float CameraDownedFocusHeight {get;set;}=.55f;
        [System.ComponentModel.DefaultValue(8f),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)]public float CameraSpringHz {get;set;}=8;
        [System.ComponentModel.DefaultValue(true),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)]public bool CameraShakeEnabled {get;set;}=true;
        public ArenaBindingDocument[] Bindings {get;set;}=Array.Empty<ArenaBindingDocument>();
        public PreviewObstacleDocument[] Obstacles {get;set;}=Array.Empty<PreviewObstacleDocument>();
    }
    public sealed class ArenaBindingDocument
    {
        public string Action {get;set;}
        public string Input {get;set;}
    }
}
