using System;

namespace Combat.Config
{
    // Authoring-only metadata. Runtime publication deliberately omits this object.
    public sealed class SkillEditorDocument
    {
        public string SelectedSkill {get;set;}
        public string SelectedBuff {get;set;}
        [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)]public string SelectedTimeline {get;set;}
        [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)]public string SelectedProjectile {get;set;}
        [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)]public string SelectedArea {get;set;}
        public GraphViewportDocument[] Viewports {get;set;}=Array.Empty<GraphViewportDocument>();
    }

    public sealed class GraphViewportDocument
    {
        public string Kind {get;set;}
        public string Id {get;set;}
        public float X {get;set;}
        public float Y {get;set;}
        public float Zoom {get;set;}=1;
    }
}
