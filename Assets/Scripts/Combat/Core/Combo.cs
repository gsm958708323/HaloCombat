using System;

namespace Combat.Core
{
    [Serializable]
    public struct ComboEntry
    {
        public string Key;
        public SkillNodeId[] PreSkills;
        public InputToken Input;
        public int[] RequiredTags;
        public int Priority;
        public SkillNodeId ToSkill;
        public TimelineId Timeline;
    }

    public readonly struct ComboResolveResult
    {
        public readonly SkillNodeId ToSkill;
        public readonly TimelineId Timeline;
        public readonly int Priority;
        public readonly string Key;
        public ComboResolveResult(SkillNodeId toSkill, TimelineId timeline, int priority,string key=null)
        {
            ToSkill = toSkill;
            Timeline = timeline;
            Priority = priority;
            Key=key;
        }
    }

    public sealed class ComboTableDefinition
    {
        public ComboEntry[] Entries = Array.Empty<ComboEntry>();

        public bool TryResolve(SkillNodeId currentSkill, in InputToken input, TagComp tags, out ComboResolveResult result)
        {
            ComboEntry? best = null;
            int bestPri = int.MinValue;
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                if (!e.Input.Equals(input)) continue;
                if (!PreMatches(e.PreSkills, currentSkill)) continue;
                if (!TagsMatch(e.RequiredTags, tags)) continue;
                if (e.Priority < bestPri) continue;
                best = e;
                bestPri = e.Priority;
            }

            if (best.HasValue)
            {
                var e = best.Value;
                result = new ComboResolveResult(e.ToSkill, e.Timeline, e.Priority,e.Key);
                return true;
            }

            result = default;
            return false;
        }

        static bool PreMatches(SkillNodeId[] pres, SkillNodeId current)
        {
            if (pres == null || pres.Length == 0)
                return !current.IsValid;
            for (int i = 0; i < pres.Length; i++)
                if (pres[i] == current) return true;
            return false;
        }

        static bool TagsMatch(int[] required, TagComp tags)
        {
            if (required == null || required.Length == 0) return true;
            for (int i = 0; i < required.Length; i++)
                if (!tags.Has(new TagId(required[i]))) return false;
            return true;
        }
    }

    public sealed partial class ComboComp : Comp
    {
        ComboTableDefinition _table;
        public void ReplaceTable(ComboTableDefinition table){_table=table??throw new ArgumentNullException(nameof(table));ResetDiagnostics();LastFailure=null;}
        InputBufferComp _input;
        TagComp _tags;
        SkillDirectorComp _director;
        public string LastFailure {get;private set;}

        public ComboComp(ComboTableDefinition table)
        {
            _table = table ?? throw new ArgumentNullException(nameof(table));
        }

        protected override void OnAttach()
        {
            _input = Self.GetComp<InputBufferComp>();
            _tags = Self.GetComp<TagComp>();
            _director = Self.GetComp<SkillDirectorComp>();
        }

        protected override void OnDetach()
        {
            ResetDiagnostics();
            _input = null;
            _tags = null;
            _director = null;
        }

        public bool TryResolve(out ComboResolveResult result)
        {
            result = default;
            if (_input == null || !_input.TryPeek(out var token))
                return false;
            return TryResolveCurrent(token,out result);
        }
        public bool TryResolveCurrent(in InputToken token,out ComboResolveResult result)
        {
            result=default;
            if(_director==null||!_director.CanStartSkill){LastFailure="当前角色状态禁止接招";CaptureDecision(token,result,"blocked",LastFailure);return false;}
            if(_director.IsPlaying&&!_tags.Has(CommonTags.Cancel)){LastFailure="当前动作接续窗口尚未开启";CaptureDecision(token,result,"waiting",LastFailure);return false;}
            bool resolved=_table.TryResolve(_director.CurrentSkill,token,_tags,out result);bool confirmed=_director.HasComboConfirmation;
            LastFailure=resolved?null:"没有满足输入、前置技能和状态条件的接续";
            CaptureDecision(token,result,resolved?"matched":"unmatched",LastFailure);
            _director.ConsumeComboConfirm("Lookup");
            Self.World.Events.Publish(new EvComboResolved(Self.Id,token.Action,resolved,result.Key,result.ToSkill,confirmed,_director.CurrentOrigin));return resolved;
        }
        public void ConsumeInput()=>_input?.Consume();
    }

    public readonly struct EvComboResolved
    {
        public readonly EntityId Source;
        public readonly string Input,Entry;
        public readonly bool Resolved,ConsumedConfirmation;
        public readonly SkillNodeId Skill;
        public readonly SkillOrigin Origin;
        public EvComboResolved(EntityId source,string input,bool resolved,string entry,SkillNodeId skill,bool consumed,SkillOrigin origin)
        {Source=source;Input=input;Resolved=resolved;Entry=entry;Skill=skill;ConsumedConfirmation=consumed;Origin=origin;}
    }
    public readonly struct EvComboConfirmation
    {
        public readonly EntityId Source,Target;
        public readonly SkillOrigin Origin;
        public readonly string Result;
        public EvComboConfirmation(EntityId source,EntityId target,SkillOrigin origin,string result){Source=source;Target=target;Origin=origin;Result=result;}
    }

}
