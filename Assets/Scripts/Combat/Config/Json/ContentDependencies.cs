using System.Collections.Generic;
using System.Linq;

namespace Combat.Config
{
    public sealed class ContentDependencies
    {
        internal static string Skill(string id)=>"skill:"+id;
        internal static string Buff(string id)=>"buff:"+id;
        readonly Dictionary<string,HashSet<string>> _edges=new Dictionary<string,HashSet<string>>();
        internal void Register(string key)=>_edges.Add(key,new HashSet<string>());
        internal void Add(string source,string target)=>_edges[source].Add(target);
        internal void Validate()
        {
            var depths=new Dictionary<string,int>();
            foreach(string key in _edges.Keys)Depth(key,new HashSet<string>(),depths);
        }
        int Depth(string key,HashSet<string> visiting,Dictionary<string,int> depths)
        {
            if(depths.TryGetValue(key,out int known))return known;
            string path=key.StartsWith("buff:")?"$.buffs["+key.Substring(5)+"]":"$.skills["+key.Substring(6)+"]";
            if(!visiting.Add(key))throw new ContentValidationException(path,"Recursive skill/Buff dependency is not allowed.");
            int depth=0;
            foreach(string target in _edges[key])depth=System.Math.Max(depth,1+Depth(target,visiting,depths));
            visiting.Remove(key);
            if(depth>8)throw new ContentValidationException(path,"Content dependency depth exceeds8.");
            return depths[key]=depth;
        }
        public HashSet<string> Closure(string root)
        {
            root=Skill(root);
            if(!_edges.ContainsKey(root))throw new ContentValidationException("$.skillId","Unknown selected skill: "+root);
            var result=new HashSet<string>();var pending=new Stack<string>();pending.Push(root);
            while(pending.Count>0){string key=pending.Pop();if(result.Add(key))foreach(string next in _edges[key])pending.Push(next);}
            return result;
        }
    }

    internal static class SkillContentClosure
    {
        public static SkillProjectDocument Select(SkillProjectDocument source,CombatContentSnapshot content,string root)
        {
            var keys=content.Dependencies.Closure(root);
            var blueprints=new HashSet<string>(content.Summons.All.Where(d=>keys.Contains(d.OriginContentKey??"")).Select(d=>d.BlueprintId));
            var selected=new SkillProjectDocument
            {
                FormatVersion=source.FormatVersion,Id=source.Id,Name=source.Name,Preview=source.Preview,
                Skills=source.Skills.Where(s=>keys.Contains(ContentDependencies.Skill(s.Id))).ToArray(),
                SummonBlueprints=source.SummonBlueprints.Where(b=>blueprints.Contains(b.Id)).ToArray(),
                Buffs=source.Buffs.Where(b=>keys.Contains(ContentDependencies.Buff(b.Id))).ToArray()
            };
            if(source.Runtime!=null)selected.Runtime=new RuntimeContentDocument{Motor=source.Runtime.Motor,Cues=source.Runtime.Cues,Views=source.Runtime.Views};
            // A selected closure owns its graph data. Templates still need authoring positions.
            selected=SkillProjectJson.Read(SkillProjectJson.Write(selected));
            return selected;
        }
    }
}
