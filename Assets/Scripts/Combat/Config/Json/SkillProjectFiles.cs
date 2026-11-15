using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    // Disk persistence only. Portable JSON and the in-memory authoring DTO keep their existing contract.
    public static class SkillProjectFiles
    {
        static readonly string[] Collections={"skills","timelines","projectiles","areas","buffs","summonBlueprints"};
        static readonly JsonLoadSettings Parsing=new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error};
        public static SkillProjectDocument Read(string path)=>ProjectFileTransaction.Locked(path,()=>ReadUnlocked(Path.GetFullPath(path)));
        static SkillProjectDocument ReadUnlocked(string path)
        {
            try
            {
                var index=JObject.Parse(File.ReadAllText(path),Parsing);
                if(index["files"]==null)return SkillProjectJson.Read(index.ToString()); // Portable runtime exports are also loadable files.
                foreach(var property in index.Properties())if(!new[]{"formatVersion","id","name","files"}.Contains(property.Name))Fail(path,"Unknown index field: "+property.Name);
                var files=index["files"] as JObject;if(files==null)Fail(path,"files must be an object.");
                var document=new JObject{["formatVersion"]=index["formatVersion"],["id"]=index["id"],["name"]=index["name"]};
                var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var part in files.Properties())
                {
                    if(Collections.Contains(part.Name))
                    {
                        if(!(part.Value is JArray list))throw new ContentValidationException(path+".files."+part.Name,"Expected file paths.");
                        var values=new JArray();foreach(var file in list)values.Add(ReadPart(path,file,seen));document[part.Name]=values;
                    }
                    else if(part.Name=="runtime")
                    {
                        if(!(part.Value is JObject runtime))throw new ContentValidationException(path+".files.runtime","Expected runtime section paths.");
                        var value=new JObject();foreach(var section in runtime.Properties())value[section.Name]=ReadPart(path,section.Value,seen);document[part.Name]=value;
                    }
                    else if(part.Name=="preview"||part.Name=="editor")document[part.Name]=ReadPart(path,part.Value,seen);
                    else Fail(path,"Unknown content section: "+part.Name);
                }
                if(files["skills"]==null||files["preview"]==null)Fail(path,"Index must reference skills and preview.");
                return SkillProjectJson.Read(document.ToString());
            }
            catch(JsonException error){throw new ContentValidationException(path,error.Message);}
            catch(IOException error){throw new ContentValidationException(path,error.Message);}
        }
        static JToken ReadPart(string index,JToken value,HashSet<string> seen)
        {
            if(value?.Type!=JTokenType.String)throw new ContentValidationException(index+".files","Expected a relative JSON path.");
            string path=Resolve(index,(string)value);if(!seen.Add(path))Fail(index,"Duplicate content file: "+value);
            try{return JToken.Parse(File.ReadAllText(path),Parsing);}
            catch(Exception error) when(error is JsonException||error is IOException){throw new ContentValidationException(path,error.Message);}
        }
        public static void Write(string path,SkillProjectDocument project)
        {
            SkillProjectStructure.Validate(project);path=Path.GetFullPath(path);
            ProjectFileTransaction.Locked(path,()=>
            {
                var source=JObject.Parse(SkillProjectJson.Write(project));var references=new JObject();
                var output=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                void Add(string relative,JToken value){string file=Resolve(path,relative);if(output.ContainsKey(file))Fail(path,"Conflicting content file: "+relative);output.Add(file,value.ToString(Formatting.Indented));}
                foreach(string kind in Collections)
                {
                    if(!(source[kind] is JArray items))continue;var paths=new JArray();
                    foreach(var item in items)
                    {string relative=kind+"/"+Filename((string)item["id"])+".json";Add(relative,item);paths.Add(relative);}
                    references[kind]=paths;
                }
                foreach(string kind in new[]{"preview","editor"})if(source[kind]!=null){Add(kind+".json",source[kind]);references[kind]=kind+".json";}
                if(source["runtime"] is JObject runtime)
                {
                    var parts=new JObject();foreach(var part in runtime.Properties()){string relative="runtime/"+part.Name+".json";Add(relative,part.Value);parts[part.Name]=relative;}references["runtime"]=parts;
                }
                var manifest=new JObject{["formatVersion"]=source["formatVersion"],["id"]=source["id"],["name"]=source["name"],["files"]=references};
                var obsolete=ReferencedPaths(path);output.Add(path,manifest.ToString(Formatting.Indented)); // Index is the last replacement.
                ProjectFileTransaction.Commit(output,obsolete);return true;
            });
        }
        public static string[] ReferencedPaths(string index)
        {
            if(!File.Exists(index))return Array.Empty<string>();
            var root=JObject.Parse(File.ReadAllText(index),Parsing);var files=root["files"] as JObject;
            return files==null?Array.Empty<string>():files.Descendants().OfType<JValue>().Where(v=>v.Type==JTokenType.String).Select(v=>Resolve(index,(string)v)).ToArray();
        }
        static string Filename(string identity)
        {
            if(string.IsNullOrWhiteSpace(identity))throw new ContentValidationException("$.id","Content identity is required.");
            string name=Uri.EscapeDataString(identity);if(name.Length>120)name=name.Substring(0,80)+"-"+SkillJsonCodec.Fingerprint(identity).Substring(0,20);
            if(new[]{"CON","PRN","AUX","NUL"}.Contains(name.ToUpperInvariant())||System.Text.RegularExpressions.Regex.IsMatch(name,"^(COM|LPT)[1-9]$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))name="_"+name;
            return name;
        }
        static string Resolve(string index,string relative)
        {
            if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||!relative.EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ContentValidationException(index,"Expected a relative JSON path.");
            string root=Path.GetDirectoryName(Path.GetFullPath(index))+Path.DirectorySeparatorChar,path=Path.GetFullPath(Path.Combine(root,relative));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase)||path==Path.GetFullPath(index))Fail(index,"Content path leaves the project or references its index.");
            return path;
        }
        static void Fail(string path,string reason)=>throw new ContentValidationException(path,reason);
    }
}
