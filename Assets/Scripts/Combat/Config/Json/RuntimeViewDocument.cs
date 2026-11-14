using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Combat.Config
{
    // Dictionary keys are case-sensitive content tokens, not camel-cased DTO properties.
    [Newtonsoft.Json.JsonDictionary(NamingStrategyType=typeof(Newtonsoft.Json.Serialization.DefaultNamingStrategy))]
    public sealed class AnimationStateBindings : Dictionary<string,string> { }

    public sealed class RuntimeViewDocument
    {
        public string Id {get;set;}
        public string Resource {get;set;}
        public float Scale {get;set;}=1;
        public float YawOffset {get;set;}
        public AnimationStateBindings States {get;set;}=new AnimationStateBindings();
    }

    public sealed class RuntimeViewDefinition
    {
        public string Id {get;}
        public string Resource {get;}
        public float Scale {get;}
        public float YawOffset {get;}
        public IReadOnlyDictionary<string,string> States {get;}
        internal RuntimeViewDefinition(RuntimeViewDocument source)
        {
            Id=source.Id;Resource=source.Resource;Scale=source.Scale;YawOffset=source.YawOffset;
            States=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(source.States));
        }
    }

    internal static class RuntimeViewCompiler
    {
        internal static readonly string[] BodyStates={"Stand","MoveForward","MoveBack","MoveLeft","MoveRight","JumpAir","Hurt0","Hurt1","Dead"};
        internal static bool Builtin(string key)=>Array.IndexOf(new[]{"fighter","stake","melee_ai","melee_guard","ranged_ai","summon"},key)>=0;
        internal static IReadOnlyDictionary<string,RuntimeViewDefinition> Compile(RuntimeViewDocument[] sources,SkillProjectDocument project)
        {
            var views=new Dictionary<string,RuntimeViewDefinition>();
            if(sources==null)return new ReadOnlyDictionary<string,RuntimeViewDefinition>(views);
            if(sources.Length>128)Fail("$.runtime.views","View collection exceeds128.");
            var actions=project.Skills.Select(s=>s.Timeline?.Animation).Concat(project.Timelines.Select(t=>t.Timeline?.Animation)).Where(a=>!string.IsNullOrEmpty(a)).Distinct();
            foreach(var source in sources)
            {
                string path="$.runtime.views["+source?.Id+"]";
                if(source==null||string.IsNullOrWhiteSpace(source.Id)||Builtin(source.Id)||views.ContainsKey(source.Id))Fail(path,"Missing, reserved or duplicate view identity.");
                Resource(source.Resource,path+".resource");
                AuthoredTimelineCompiler.Number(source.Scale,.001f,100,path+".scale");AuthoredTimelineCompiler.Number(source.YawOffset,-360,360,path+".yawOffset");
                if(source.States==null||source.States.Count>128)Fail(path+".states","Expected up to128 state bindings.");
                foreach(var state in source.States)if(string.IsNullOrWhiteSpace(state.Key)||string.IsNullOrWhiteSpace(state.Value)||state.Key.Length>80||state.Value.Length>256)Fail(path+".states","Empty or oversized animation binding.");
                foreach(string key in BodyStates.Concat(actions.SelectMany(a=>a=="Roll"?new[]{"RollForward","RollBack","RollLeft","RollRight"}:new[]{a})))
                    if(!source.States.ContainsKey(key))Fail(path+".states["+key+"]","Missing animation semantic binding.");
                views.Add(source.Id,new RuntimeViewDefinition(source));
            }
            return new ReadOnlyDictionary<string,RuntimeViewDefinition>(views);
        }
        static void Resource(string value,string path)
        {
            if(string.IsNullOrWhiteSpace(value)||value.Length>240||value.StartsWith("/")||value.Contains("\\")||value.Contains(":")||value.Split('/').Any(s=>s.Length==0||s=="."||s=="..")||System.IO.Path.HasExtension(value))
                Fail(path,"Expected extensionless Resources relative key.");
        }
        static void Fail(string path,string message)=>throw new ContentValidationException(path,message);
    }
}
