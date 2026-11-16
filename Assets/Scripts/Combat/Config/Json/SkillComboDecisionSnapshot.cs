using System.Linq;
using Combat.Core;

namespace Combat.Config
{
    public sealed class SkillComboCandidateSnapshot
    {
        public string Rule {get;internal set;}
        public string Skill {get;internal set;}
        public int Priority {get;internal set;}
        public int[] MissingTags {get;internal set;}
    }
    public sealed class SkillComboDecisionSnapshot
    {
        public int Sequence {get;internal set;}
        public int InputSequence {get;internal set;}
        public string Input {get;internal set;}
        public string FromSkill {get;internal set;}
        public string Rule {get;internal set;}
        public string ToSkill {get;internal set;}
        public string ActualSkill {get;internal set;}
        public string Phase {get;internal set;}
        public string Reason {get;internal set;}
        public bool WindowOpen {get;internal set;}
        public bool Confirmed {get;internal set;}
        public int CastId {get;internal set;}
        public SkillComboCandidateSnapshot[] Candidates {get;internal set;}
        internal static SkillComboDecisionSnapshot From(ComboDecision value,SkillCatalog skills)
        {
            if(value==null)return null;
            string Key(SkillNodeId id)=>id.IsValid&&skills.TryGet(id,out var skill)?skill.Key:null;
            return new SkillComboDecisionSnapshot{Sequence=value.Sequence,InputSequence=value.InputSequence,Input=value.Input,FromSkill=Key(value.FromSkill),Rule=value.Rule,
                ToSkill=Key(value.ToSkill),ActualSkill=Key(value.ActualSkill),Phase=value.Phase,Reason=value.Reason,WindowOpen=value.WindowOpen,Confirmed=value.Confirmed,CastId=value.CastId,
                Candidates=value.Candidates.Select(c=>new SkillComboCandidateSnapshot{Rule=c.Rule,Skill=Key(c.Skill),Priority=c.Priority,MissingTags=(int[])c.MissingTags.Clone()}).ToArray()};
        }
    }
}
