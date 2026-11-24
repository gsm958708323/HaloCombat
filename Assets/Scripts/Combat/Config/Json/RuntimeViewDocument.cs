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
        [System.ComponentModel.DefaultValue("actor"),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)]public string Kind {get;set;}="actor";
        [System.ComponentModel.DefaultValue(0f),Newtonsoft.Json.JsonProperty(DefaultValueHandling=Newtonsoft.Json.DefaultValueHandling.Ignore)]public float SpinSpeed {get;set;}
        public float Scale {get;set;}=1;
        public float YawOffset {get;set;}
        public AnimationStateBindings States {get;set;}=new AnimationStateBindings();
        [Newtonsoft.Json.JsonProperty(NullValueHandling=Newtonsoft.Json.NullValueHandling.Ignore)]public RuntimeAnchorDocument[] Anchors {get;set;}
    }

    public sealed class RuntimeViewDefinition
    {
        public string Id {get;}
        public string Resource {get;}
        public float Scale {get;}
        public float YawOffset {get;}
        public string Kind {get;}
        public float SpinSpeed {get;}
        public IReadOnlyDictionary<string,string> States {get;}
        public IReadOnlyList<RuntimeAnchorDefinition> Anchors {get;}
        internal RuntimeViewDefinition(RuntimeViewDocument source)
        {
            Id=source.Id;Resource=source.Resource;Scale=source.Scale;YawOffset=source.YawOffset;Kind=source.Kind;SpinSpeed=source.SpinSpeed;
            States=new ReadOnlyDictionary<string,string>(new Dictionary<string,string>(source.States));
            Anchors=Array.AsReadOnly((source.Anchors??Array.Empty<RuntimeAnchorDocument>()).Select(a=>new RuntimeAnchorDefinition(a)).ToArray());
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
                if(source.Kind!="actor"&&source.Kind!="body")Fail(path+".kind","Expected actor or body.");
                AuthoredTimelineCompiler.Number(source.SpinSpeed,-10000,10000,path+".spinSpeed");
                AuthoredTimelineCompiler.Number(source.Scale,.001f,100,path+".scale");AuthoredTimelineCompiler.Number(source.YawOffset,-360,360,path+".yawOffset");
                if(source.States==null||source.States.Count>128)Fail(path+".states","Expected up to128 state bindings.");
                var anchors=source.Anchors??Array.Empty<RuntimeAnchorDocument>();var anchorKeys=new HashSet<string>();
                if(anchors.Length>32)Fail(path+".anchors","Expected up to32 anchors.");
                foreach(var anchor in anchors)
                {
                    if(anchor==null||string.IsNullOrWhiteSpace(anchor.Key)||!anchorKeys.Add(anchor.Key)||anchor.Path==null||anchor.Path.Contains("..")||anchor.Path.StartsWith("/"))Fail(path+".anchors","Invalid or duplicate anchor.");
                    AuthoredTimelineCompiler.Number(anchor.X,-100,100,path+".anchors.x");AuthoredTimelineCompiler.Number(anchor.Y,-100,100,path+".anchors.y");AuthoredTimelineCompiler.Number(anchor.Z,-100,100,path+".anchors.z");
                }
                foreach(var state in source.States)if(string.IsNullOrWhiteSpace(state.Key)||string.IsNullOrWhiteSpace(state.Value)||state.Key.Length>80||state.Value.Length>256)Fail(path+".states","Empty or oversized animation binding.");
                if(source.Kind=="body"&&source.States.Count>0)Fail(path+".states","Body views have no character animation bindings.");
                foreach(string key in source.Kind=="body"?Array.Empty<string>():BodyStates.Concat(actions.SelectMany(a=>a=="Roll"?new[]{"RollForward","RollBack","RollLeft","RollRight"}:new[]{a})))
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
    public sealed class RuntimeAnchorDocument
    {
        public string Key {get;set;}
        public string Path {get;set;}="";
        public float X {get;set;}
        public float Y {get;set;}
        public float Z {get;set;}
    }
    public sealed class RuntimeAnchorDefinition
    {
        public string Key {get;}
        public string Path {get;}
        public float X {get;}
        public float Y {get;}
        public float Z {get;}
        internal RuntimeAnchorDefinition(RuntimeAnchorDocument source){Key=source.Key;Path=source.Path;X=source.X;Y=source.Y;Z=source.Z;}
    }
}
