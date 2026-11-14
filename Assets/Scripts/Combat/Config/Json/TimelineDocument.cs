using System;

namespace Combat.Config
{
    public sealed class TimelineDocument
    {
        public int RuntimeId {get;set;}
        public float Duration {get;set;}
        public int[] ControlTags {get;set;}=Array.Empty<int>();
        public bool ShouldSerializeControlTags()=>ControlTags==null||ControlTags.Length>0;
        [System.ComponentModel.DefaultValue(false),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)]public bool ScaleWithActionSpeed {get;set;}
        public TimelineClipDocument[] Clips {get;set;}=Array.Empty<TimelineClipDocument>();
        public TimelinePayloadDocument[] Payloads {get;set;}=Array.Empty<TimelinePayloadDocument>();
    }
    public sealed class TimelineClipDocument
    {
        public string Id {get;set;}
        public string Kind {get;set;}
        public float Start {get;set;}
        public float End {get;set;}
        public float MoveX {get;set;}
        public float MoveY {get;set;}
        public float MoveZ {get;set;}
        public float Steer {get;set;}
        public float HitRadius {get;set;}
        public float HitOffsetX {get;set;}
        public float HitOffsetY {get;set;}
        public float HitOffsetZ {get;set;}
        public string[] Effects {get;set;}=Array.Empty<string>();
    }
    public sealed class TimelinePayloadDocument
    {
        public string Id {get;set;}
        public float Time {get;set;}
        public float RepeatInterval {get;set;}
        public string[] Effects {get;set;}=Array.Empty<string>();
    }
}
