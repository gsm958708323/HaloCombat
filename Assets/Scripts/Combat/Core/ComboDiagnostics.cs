using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class ComboCandidate
    {
        public readonly string Rule;
        public readonly SkillNodeId Skill;
        public readonly int Priority;
        public readonly int[] MissingTags;
        public ComboCandidate(string rule,SkillNodeId skill,int priority,int[] missing){Rule=rule;Skill=skill;Priority=priority;MissingTags=missing;}
    }
    public sealed class ComboDecision
    {
        public readonly int Sequence,InputSequence,CastId,ConditionHash;
        public readonly string Input,Rule,Phase,Reason;
        public readonly SkillNodeId FromSkill,ToSkill,ActualSkill;
        public readonly bool WindowOpen,Confirmed;
        public readonly ComboCandidate[] Candidates;
        public ComboDecision(int sequence,int inputSequence,string input,SkillNodeId from,string rule,SkillNodeId to,SkillNodeId actual,
            string phase,string reason,bool window,bool confirmed,int castId,int hash,ComboCandidate[] candidates)
        {Sequence=sequence;InputSequence=inputSequence;Input=input;FromSkill=from;Rule=rule;ToSkill=to;ActualSkill=actual;Phase=phase;Reason=reason;
            WindowOpen=window;Confirmed=confirmed;CastId=castId;ConditionHash=hash;Candidates=candidates;}
    }
    public readonly struct EvComboDecision
    {
        public readonly EntityId Source;
        public readonly ComboDecision Decision;
        public EvComboDecision(EntityId source,ComboDecision decision){Source=source;Decision=decision;}
    }
    public sealed partial class ComboComp
    {
        public bool DiagnosticsEnabled {get;set;}
        public ComboDecision LastDecision {get;private set;}
        int _decisionSequence;
        void CaptureDecision(in InputToken input,in ComboResolveResult resolved,string phase,string reason)
        {
            if(!DiagnosticsEnabled)return;
            var from=_director?.CurrentSkill??SkillNodeId.None;
            bool window=_director!=null&&(!_director.IsPlaying||_tags.Has(CommonTags.Cancel)),confirmed=_director?.HasComboConfirmation??false;
            int press=_input?.HeadSequence??0,hash=17;
            unchecked{foreach(var row in _table.Entries)if(row.Input==input&&Predecessor(row.PreSkills,from))
                {hash=hash*31+row.Priority;foreach(int tag in row.RequiredTags??Array.Empty<int>())hash=hash*31+(_tags.Has(new TagId(tag))?tag:-tag);}}
            var old=LastDecision;
            bool same=old!=null&&old.InputSequence==press&&old.Input==input.Action&&old.FromSkill==from&&old.Rule==resolved.Key&&old.ToSkill==resolved.ToSkill&&old.ConditionHash==hash&&old.WindowOpen==window&&old.Confirmed==confirmed;
            if(same&&(old.Phase==phase&&old.Reason==reason||phase=="matched"&&(old.Phase=="cast"||old.Phase=="rejected")))return;
            var candidates=new List<ComboCandidate>();
            foreach(var row in _table.Entries)if(row.Input==input&&Predecessor(row.PreSkills,from))
            {
                var missing=new List<int>();foreach(int tag in row.RequiredTags??Array.Empty<int>())if(!_tags.Has(new TagId(tag)))missing.Add(tag);
                candidates.Add(new ComboCandidate(row.Key,row.ToSkill,row.Priority,missing.ToArray()));
            }
            PublishDecision(new ComboDecision(++_decisionSequence,press,input.Action,from,resolved.Key,resolved.ToSkill,SkillNodeId.None,phase,reason,window,confirmed,
                _director?.CurrentOrigin.CastId??0,hash,candidates.ToArray()));
        }
        public void ReportCastOutcome(bool accepted,SkillNodeId actual,string rejection)
        {
            var old=LastDecision;if(!DiagnosticsEnabled||old==null||old.Rule==null)return;
            string phase=accepted?"cast":"rejected",reason=accepted?null:rejection;
            if(!accepted&&old.Phase==phase&&old.Reason==reason&&old.ActualSkill==actual)return;
            PublishDecision(new ComboDecision(++_decisionSequence,old.InputSequence,old.Input,old.FromSkill,old.Rule,old.ToSkill,actual,phase,reason,
                old.WindowOpen,old.Confirmed,accepted?(_director?.CurrentOrigin.CastId??0):old.CastId,old.ConditionHash,old.Candidates));
        }
        public void ReportBlockedInput(in InputToken input)
            =>CaptureDecision(input,default,"blocked","当前角色状态禁止接招");
        void PublishDecision(ComboDecision decision){LastDecision=decision;Self.World.Events.Publish(new EvComboDecision(Self.Id,decision));}
        static bool Predecessor(SkillNodeId[] predecessors,SkillNodeId current)
        {if(predecessors==null||predecessors.Length==0)return !current.IsValid;foreach(var id in predecessors)if(id==current)return true;return false;}
        void ResetDiagnostics(){LastDecision=null;_decisionSequence=0;}
    }
}
