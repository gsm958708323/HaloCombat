using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    public sealed class ArenaProject
    {
        public SkillProjectDocument Document {get;}
        public CombatContentSnapshot Content {get;}
        public IReadOnlyDictionary<string,SkillNodeId> Slots {get;}
        public ArenaProject(SkillProjectDocument project,IReadOnlyDictionary<string,string> publications=null)
        {
            Document=SkillProjectJson.Read(SkillProjectJson.Write(project));
            var roots=new Dictionary<string,string>();var shared=new Dictionary<string,string>();
            if(publications!=null)foreach(var entry in publications.OrderBy(e=>e.Key,System.StringComparer.Ordinal))
            {
                if(entry.Key!="f6"&&entry.Key!="f7")throw new ContentValidationException("$.slots","Unknown slot.");
                var pub=SkillPublicationJson.Read(entry.Value).Document;
                if(pub.Project.Id!=project.Id)throw new ContentValidationException("$.slots."+entry.Key,"Publish this slot from the current formal project: "+project.Id);
                if(SkillJsonCodec.Write(pub.Project.Runtime?.Motor)!=SkillJsonCodec.Write(project.Runtime?.Motor)||SkillJsonCodec.Write(pub.Project.Runtime?.Cues)!=SkillJsonCodec.Write(project.Runtime?.Cues)||SkillJsonCodec.Write(pub.Project.Runtime?.Views)!=SkillJsonCodec.Write(project.Runtime?.Views))
                    throw new ContentValidationException("$.slots."+entry.Key,"Motor/Cue/View version conflicts with formal content; publish formal content and both slots together.");
                foreach(var value in pub.Project.Skills){Check("skill:"+value.Id,value,shared);Document.Skills=Document.Skills.Where(s=>s.Id!=value.Id).Concat(new[]{value}).ToArray();}
                foreach(var value in pub.Project.Buffs){Check("buff:"+value.Id,value,shared);Document.Buffs=Document.Buffs.Where(b=>b.Id!=value.Id).Concat(new[]{value}).ToArray();}
                foreach(var value in pub.Project.Timelines){Check("timeline:"+value.Id,value,shared);Document.Timelines=Document.Timelines.Where(t=>t.Id!=value.Id).Concat(new[]{value}).ToArray();}
                foreach(var value in pub.Project.Projectiles){Check("projectile:"+value.Id,value,shared);Document.Projectiles=Document.Projectiles.Where(p=>p.Id!=value.Id).Concat(new[]{value}).ToArray();}
                foreach(var value in pub.Project.Areas){Check("area:"+value.Id,value,shared);Document.Areas=Document.Areas.Where(a=>a.Id!=value.Id).Concat(new[]{value}).ToArray();}
                foreach(var value in pub.Project.SummonBlueprints){Check("blueprint:"+value.Id,value,shared);Document.SummonBlueprints=Document.SummonBlueprints.Where(b=>b.Id!=value.Id).Concat(new[]{value}).ToArray();}
                roots.Add(entry.Key,pub.SkillId);
            }
            Content=CombatContentCompiler.Compile(Document);
            if(Content.Runtime?.Arena==null)throw new ContentValidationException("$.runtime.arena","Formal content has no Arena definition.");
            Slots=roots.ToDictionary(e=>e.Key,e=>Content.Skills.All.First(s=>s.Key==e.Value).Id);
        }
        static void Check(string key,object value,Dictionary<string,string> versions)
        {string text=SkillJsonCodec.Write(value);if(versions.TryGetValue(key,out var old)&&old!=text)throw new ContentValidationException("$.slots", "Conflicting publication versions: "+key);versions[key]=text;}
        public ArenaSession Create(bool training=false)=>new ArenaSession(Content,training,Slots);
    }
}
