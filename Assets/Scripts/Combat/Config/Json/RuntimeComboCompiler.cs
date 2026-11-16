using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    internal static class RuntimeComboCompiler
    {
        public static ComboEntry[] Compile(RuntimeComboDocument[] rows,Func<string,string,SkillDefinition> skill)
        {
            var keys=new HashSet<string>();var result=new List<ComboEntry>();
            foreach(var row in rows)
            {
                string path="$.runtime.combos["+row?.Id+"]";
                if(row==null||string.IsNullOrWhiteSpace(row.Id)||!keys.Add(row.Id))throw new ContentValidationException(path,"Missing or duplicate identity.");
                ComboAuthoringValidation.Rule(row,path);
                if(string.IsNullOrWhiteSpace(row.Input)||row.Input.Length>128||row.RequiredTags==null||row.RequiredTags.Any(t=>t<=0)||row.PreSkills==null)
                    throw new ContentValidationException(path,"Invalid input, tags or predecessor array.");
                var target=skill(row.ToSkill,path+".toSkill");
                var entry=new ComboEntry{Key=row.Id,Input=new InputToken(row.Input),Priority=row.Priority,RequiredTags=(int[])row.RequiredTags.Clone(),
                    PreSkills=row.PreSkills.Select(id=>skill(id,path+".preSkills").Id).ToArray(),ToSkill=target.Id,Timeline=target.Timeline};
                foreach(var other in result)
                    if(other.Input==entry.Input&&other.Priority==entry.Priority&&Overlaps(other.PreSkills,entry.PreSkills))
                        throw new ContentValidationException(path,"Ambiguous Combo priority with "+other.Key+"; positive RequiredTags can coexist.");
                result.Add(entry);
            }
            return result.ToArray();
        }
        static bool Overlaps(SkillNodeId[] left,SkillNodeId[] right)
        {if(left.Length==0||right.Length==0)return left.Length==right.Length;return left.Any(id=>Array.IndexOf(right,id)>=0);}
    }
}
