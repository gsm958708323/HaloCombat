using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    public sealed class SkillReplayDocument
    {
        [JsonProperty(Required=Required.Always)] public int FormatVersion {get;set;}=1;
        [JsonProperty(Required=Required.Always)] public float StepSeconds {get;set;}=SkillSimulation.Step;
        [JsonProperty(Required=Required.Always)] public SkillProjectDocument Project {get;set;}
        [JsonProperty(Required=Required.Always)] public string RootSkillId {get;set;}
        [JsonProperty(Required=Required.Always)] public int EndStep {get;set;}
        [JsonProperty(Required=Required.Always)] public SkillReplayInput[] Inputs {get;set;}=Array.Empty<SkillReplayInput>();
        [JsonProperty(Required=Required.Always)] public JObject ExpectedState {get;set;}
    }
    public sealed class SkillReplayInput
    {
        [JsonProperty(Required=Required.Always)] public int AtStep {get;set;}
        [JsonProperty(Required=Required.Always)] public SkillSimulationCommand Command {get;set;}
    }
    public sealed class SkillReplayStatus
    {
        public int Inputs {get;internal set;}
        public int Steps {get;internal set;}
        public string Error {get;internal set;}
    }
    public sealed class SkillReplayDifference
    {
        public string Path {get;internal set;}
        public string Expected {get;internal set;}
        public string Actual {get;internal set;}
    }
    public sealed class SkillReplayComparison
    {
        public int Values {get;internal set;}
        public float Tolerance {get;}=.0001f;
        public double MaximumNumericDifference {get;internal set;}
        public SkillReplayDifference[] Differences {get;internal set;}
        public bool Matches=>Differences.Length==0;
    }
}
