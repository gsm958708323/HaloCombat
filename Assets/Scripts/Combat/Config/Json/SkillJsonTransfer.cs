using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    public sealed class SkillJsonImport
    {
        public SkillProjectDocument Project {get;internal set;}
        public bool NeedsLayout {get;internal set;}
    }
    public sealed class SkillJsonExport
    {
        public string Filename {get;internal set;}
        public string Json {get;internal set;}
        public string Revision {get;internal set;}
        public int Skills {get;internal set;}
        public int Buffs {get;internal set;}
        public int Projectiles {get;internal set;}
        public int Areas {get;internal set;}
        public int Actors {get;internal set;}
    }
    public static class SkillJsonTransfer
    {
        public static SkillJsonExport Export(SkillProjectDocument source,string kind,string skillId=null)
        {
            SkillProjectDocument project;string json,revision,suffix;
            if(kind=="project")
            {SkillProjectStructure.Validate(source);project=source;json=SkillProjectJson.Write(project);revision=SkillJsonCodec.Fingerprint(json);suffix="project";}
            else if(kind=="skill")
            {
                var publication=SkillPublicationJson.Create(source,skillId);project=publication.Project;json=SkillPublicationJson.Write(publication);revision=publication.Revision;suffix=Filename(skillId)+".skill";
            }
            else if(kind=="runtime")
            {
                var compiled=CombatContentCompiler.Compile(source);
                if(compiled.Runtime==null||source.Runtime.Actors.Length==0)
                    throw new ContentValidationException("$.runtime.actors","完整运行内容需要角色定义；请先打开正式内容，或使用当前技能运行包在 SkillLab 验证。");
                project=source;
                var token=JObject.Parse(SkillProjectJson.Write(source));token.Property("editor")?.Remove();
                foreach(var node in Nodes(token))((JObject)node).Property("position")?.Remove();
                json=token.ToString(Formatting.Indented);revision=SkillJsonCodec.Fingerprint(json);suffix="runtime";
            }
            else throw new ContentValidationException("$.kind","Expected project, skill or runtime.");
            return new SkillJsonExport{Filename=Filename(project.Id)+"."+suffix+".json",Json=json,Revision=revision,
                Skills=project.Skills.Length,Buffs=project.Buffs.Length,Projectiles=project.Projectiles.Length,Areas=project.Areas.Length,Actors=project.Runtime?.Actors.Length??0};
        }
        public static SkillJsonImport Import(string text)
        {
            // Parse raw file text on the host. A browser JSON.parse would silently discard
            // duplicate properties before our protocol validation could see them.
            if(string.IsNullOrWhiteSpace(text))throw new ContentValidationException("$","Import file text is required.");
            JObject token;
            try{token=JObject.Parse(text,new JsonLoadSettings{DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});}
            catch(JsonException error){throw new ContentValidationException("$",error.Message);}
            if(token["files"]!=null)throw new ContentValidationException("$.files","这是磁盘工程索引，需要同目录功能文件；请在编辑器导出完整工程JSON后导入。");
            SkillProjectDocument project;
            bool isPublication=token.Property("project")!=null||token.Property("revision")!=null||token.Property("skillId")!=null;
            if(isPublication)
            {
                var publication=SkillPublicationJson.Read(text).Document;project=publication.Project;
                project.Editor=new SkillEditorDocument{SelectedSkill=publication.SkillId};
            }
            else project=SkillProjectJson.Read(text);
            SkillProjectStructure.Validate(project);
            return new SkillJsonImport{Project=project,NeedsLayout=isPublication||Nodes(token).Any(n=>n["position"]==null)};
        }
        static System.Collections.Generic.IEnumerable<JToken> Nodes(JObject token)
            =>new[]{"skills","buffs","timelines","projectiles","areas"}.SelectMany(k=>(token[k] as JArray??new JArray()).Children())
                .SelectMany(g=>(g["nodes"] as JArray??new JArray()).Children());
        internal static void NormalizeLayout(SkillProjectDocument project)
        {
            project.Editor=null;
            var graphs=project.Skills.Select(s=>s.Nodes).Concat(project.Buffs.Select(b=>b.Nodes)).Concat(project.Timelines.Select(t=>t.Nodes))
                .Concat(project.Projectiles.Select(p=>p.Nodes)).Concat(project.Areas.Select(a=>a.Nodes));
            foreach(var nodes in graphs)foreach(var node in nodes)node.Position=new NodePosition();
        }
        static string Filename(string identity)
        {
            string value=Regex.Replace(identity??"","[^A-Za-z0-9_-]","_");
            if(value.Length>100)value=value.Substring(0,100);
            if(Regex.IsMatch(value,"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$",RegexOptions.IgnoreCase))value="_"+value;
            return value.Length==0?"skill-content":value;
        }
    }
}
