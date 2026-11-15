using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    public sealed class SkillSimulationCommand
    {
        [JsonProperty(Required=Required.Always)] public string Kind {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public string SkillId {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public string Input {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public string ActorId {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public string EntityId {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public float? X {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public float? Z {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public int? Steps {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public int? Frames {get;set;}
        [JsonProperty(NullValueHandling=NullValueHandling.Ignore)]public int? InstanceId {get;set;}
    }

    public static class SkillSimulationCommands
    {
        static readonly Dictionary<string,string[]> Fields=new Dictionary<string,string[]>
        {
            {"step",new[]{"steps"}},{"snapshot",Array.Empty<string>()},{"cast",new[]{"skillId","x","z"}},
            {"input",new[]{"input","x","z"}},{"release",new[]{"x","z"}},{"cancel",Array.Empty<string>()},
            {"aim",new[]{"x","z"}},{"move",new[]{"x","z"}},{"place",new[]{"actorId","x","z"}},
            {"remove",new[]{"entityId"}},{"remove-buff",new[]{"actorId","instanceId"}},
            {"interrupt",Array.Empty<string>()},{"death",Array.Empty<string>()},{"hitstop",new[]{"frames"}}
        };
        public static SkillSimulationCommand Read(string text)
        {
            if(string.IsNullOrWhiteSpace(text))throw new ContentValidationException("$","Command text is required.");
            var value=SkillJsonCodec.Read<SkillSimulationCommand>(text);ValidateToken(JObject.Parse(text));Validate(value);return value;
        }
        internal static void ValidateToken(JObject token)
        {
            if(token==null)throw new ContentValidationException("$.command","Command object is required.");
            if(token["kind"]?.Type!=JTokenType.String||!Fields.TryGetValue((string)token["kind"],out var fields))
                throw new ContentValidationException("$.kind","Unknown preview command.");
            foreach(var property in token.Properties())if(property.Name!="kind"&&!fields.Contains(property.Name))
                throw new ContentValidationException("$."+property.Name,"Field is not supported by this command.");
            foreach(string field in new[]{"skillId","input","actorId","entityId"})
                if(token[field]!=null&&token[field].Type!=JTokenType.String)throw new ContentValidationException("$."+field,"Expected a string identity.");
            foreach(string field in new[]{"steps","frames","instanceId"})
                if(token[field]!=null&&token[field].Type!=JTokenType.Integer)throw new ContentValidationException("$."+field,"Expected an integer.");
            foreach(string field in new[]{"x","z"})
                if(token[field]!=null&&token[field].Type!=JTokenType.Float&&token[field].Type!=JTokenType.Integer)throw new ContentValidationException("$."+field,"Expected a numeric coordinate.");
        }
        public static void Validate(SkillSimulationCommand command)
        {
            if(command==null||command.Kind==null||!Fields.TryGetValue(command.Kind,out var fields))throw new ContentValidationException("$.kind","Unknown preview command.");
            var raw=JObject.Parse(SkillJsonCodec.Write(command));
            foreach(var property in raw.Properties())if(property.Name!="kind"&&!fields.Contains(property.Name))throw new ContentValidationException("$."+property.Name,"Field is not supported by this command.");
            if(command.X.HasValue&&!SkillSceneValidation.Finite(command.X.Value)||command.Z.HasValue&&!SkillSceneValidation.Finite(command.Z.Value))throw new ContentValidationException("$.aim","Coordinates must be finite.");
            if(command.X.HasValue!=command.Z.HasValue)throw new ContentValidationException("$.aim","Provide x and z together.");
            if((command.Kind=="aim"||command.Kind=="move"||command.Kind=="place")&&!command.X.HasValue)
                throw new ContentValidationException("$.x","This command requires x and z.");
            if(command.Kind=="remove-buff"&&command.InstanceId<=0)throw new ContentValidationException("$.instanceId","Expected a positive instance identity.");
            foreach(var value in new[]{command.SkillId,command.Input,command.ActorId,command.EntityId})
                if(value!=null&&(string.IsNullOrWhiteSpace(value)||value.Length>256))throw new ContentValidationException("$","Expected a nonempty command identity up to256 characters.");
            if(command.Kind=="cast"&&command.SkillId==null||command.Kind=="input"&&command.Input==null||command.Kind=="place"&&command.ActorId==null||command.Kind=="remove"&&command.EntityId==null||command.Kind=="remove-buff"&&(command.ActorId==null||!command.InstanceId.HasValue))
                throw new ContentValidationException("$","Command identity is required.");
            if(command.Kind=="step"&&(command.Steps??1)<1||command.Kind=="step"&&(command.Steps??1)>500)throw new ContentValidationException("$.steps","Expected1..500 steps.");
            if(command.Kind=="hitstop"&&((command.Frames??10)<0||(command.Frames??10)>1000))throw new ContentValidationException("$.frames","Expected0..1000 frames.");
        }
    }
}
