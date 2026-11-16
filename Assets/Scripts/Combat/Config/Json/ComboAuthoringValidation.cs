using System;
using System.Collections.Generic;
using System.Linq;

namespace Combat.Config
{
    // Author organization is validated independently from priority resolution.
    internal static class ComboAuthoringValidation
    {
        internal static void Rule(RuntimeComboDocument rule,string path)
        {
            if(rule.Purpose==null)return; // Existing public JSON has no author intent.
            if(rule.Purpose!="entry"&&rule.Purpose!="transition"&&rule.Purpose!="cancel")
                Fail(path+".purpose","Expected entry, transition or cancel.");
            if(rule.PreSkills==null)return;
            if(rule.Purpose=="entry"&&rule.PreSkills.Length!=0||rule.Purpose!="entry"&&rule.PreSkills.Length==0)
                Fail(path+".purpose","Entry rules require an empty predecessor list; transitions and cancels require explicit predecessors.");
        }
        internal static void Views(SkillProjectDocument project)
        {
            var views=project.Editor?.ComboViews;if(views==null)return;
            if(views.Length>256)Fail("$.editor.comboViews","At most256 author views are allowed.");
            var skills=new HashSet<string>(project.Skills.Select(s=>s.Id));
            var rules=(project.Runtime?.Combos??Array.Empty<RuntimeComboDocument>()).ToDictionary(r=>r.Id);
            var ids=new HashSet<string>();
            foreach(var view in views)
            {
                string path="$.editor.comboViews["+view?.Id+"]";
                if(view==null||string.IsNullOrWhiteSpace(view.Id)||!ids.Add(view.Id)||string.IsNullOrWhiteSpace(view.Name)||view.Name.Length>128)
                    Fail(path,"Expected a unique identity and a name up to128 characters.");
                if(!skills.Contains(view.RootSkill??""))Fail(path+".rootSkill","Unknown root skill.");
                if(view.RuleIds==null||view.RuleIds.Length>1024||view.RuleIds.Distinct().Count()!=view.RuleIds.Length)
                    Fail(path+".ruleIds","Expected unique rule identities.");
                foreach(string id in view.RuleIds)
                    if(!rules.TryGetValue(id??"",out var rule)||rule.PreSkills.Length==0||rule.Purpose=="cancel")
                        Fail(path+".ruleIds","Views may reference existing action transitions only.");
                if(view.Positions==null||view.Positions.Length>513)Fail(path+".positions","Invalid positions.");
                var positions=new HashSet<string>();
                foreach(var position in view.Positions)
                    if(position==null||!positions.Add(position.SkillId??"")||position.SkillId!="$start"&&!skills.Contains(position.SkillId??"")||
                        !Finite(position.X)||!Finite(position.Y))Fail(path+".positions","Expected unique existing skills and finite coordinates.");
            }
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static void Fail(string path,string message)=>throw new ContentValidationException(path,message);
    }
}
