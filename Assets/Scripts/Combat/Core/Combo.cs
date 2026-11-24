using System;

namespace Combat.Core
{
    [Serializable]
    public struct ComboEntry
    {
        public string Key;
        public SkillNodeId[] PreSkills;
        public InputToken Input;
        public ComboCondition[] Conditions;
        public ComboWindowSpec Window;
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
        public readonly bool WindowVerified;
        public ComboResolveResult(SkillNodeId toSkill, TimelineId timeline, int priority, string key = null, bool windowVerified = false)
        {
            ToSkill = toSkill;
            Timeline = timeline;
            Priority = priority;
            Key = key;
            WindowVerified = windowVerified;
        }
    }

    public sealed class ComboTableDefinition
    {
        public ComboEntry[] Entries = Array.Empty<ComboEntry>();

        public bool TryResolve(SkillNodeId currentSkill, in InputToken input, TagComp tags, out ComboResolveResult result)
            => TryResolve(currentSkill, input, ComboEvalContext.FromTagsOnly(tags), out result, out _, out _);

        public bool TryResolve(SkillNodeId currentSkill, in InputToken input, in ComboEvalContext ctx,
            out ComboResolveResult result, out bool anyCandidateWindowOpen, out bool hadPredecessorCandidates)
        {
            ComboEntry? best = null;
            int bestPri = int.MinValue;
            anyCandidateWindowOpen = false;
            hadPredecessorCandidates = false;
            for (int i = 0; i < Entries.Length; i++)
            {
                var e = Entries[i];
                if (!e.Input.Equals(input)) continue;
                if (!PreMatches(e.PreSkills, currentSkill)) continue;
                hadPredecessorCandidates = true;
                if (!e.Window.IsOpen(ctx)) continue;
                anyCandidateWindowOpen = true;
                if (!ConditionsMatch(e.Conditions, ctx)) continue;
                if (e.Priority < bestPri) continue;
                best = e;
                bestPri = e.Priority;
            }

            if (best.HasValue)
            {
                var e = best.Value;
                result = new ComboResolveResult(e.ToSkill, e.Timeline, e.Priority, e.Key, windowVerified: true);
                return true;
            }

            result = default;
            return false;
        }

        internal static bool PreMatches(SkillNodeId[] pres, SkillNodeId current)
        {
            if (pres == null || pres.Length == 0)
                return !current.IsValid;
            for (int i = 0; i < pres.Length; i++)
                if (pres[i] == current) return true;
            return false;
        }

        internal static bool ConditionsMatch(ComboCondition[] conditions, in ComboEvalContext ctx)
        {
            if (conditions == null || conditions.Length == 0) return true;
            for (int i = 0; i < conditions.Length; i++)
                if (!conditions[i].Evaluate(ctx)) return false;
            return true;
        }
    }

    public sealed partial class ComboComp : Comp
    {
        ComboTableDefinition _table;
        public void ReplaceTable(ComboTableDefinition table) { _table = table ?? throw new ArgumentNullException(nameof(table)); ResetDiagnostics(); LastFailure = null; }
        InputBufferComp _input;
        TagComp _tags;
        SkillDirectorComp _director;
        public string LastFailure { get; private set; }

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
            return TryResolveCurrent(token, out result);
        }

        public bool TryResolveCurrent(in InputToken token, out ComboResolveResult result)
        {
            result = default;
            var ctx = ComboEvalContext.FromActor(Self);
            if (_director == null || !_director.CanStartSkill)
            {
                LastFailure = "当前角色状态禁止接招";
                CaptureDecision(token, result, "blocked", LastFailure, ctx);
                return false;
            }
            bool resolved = _table.TryResolve(_director.CurrentSkill, token, ctx, out result, out bool anyWindowOpen, out bool hadCandidates);
            if (!resolved && _director.IsPlaying && (hadCandidates ? !anyWindowOpen : !_tags.Has(CommonTags.Cancel)))
            {
                LastFailure = "当前动作接续窗口尚未开启";
                CaptureDecision(token, result, "waiting", LastFailure, ctx);
                return false;
            }
            bool confirmed = _director.HasComboConfirmation;
            LastFailure = resolved ? null : "没有满足条件的接续";
            CaptureDecision(token, result, resolved ? "matched" : "unmatched", LastFailure, ctx);
            _director.ConsumeComboConfirm("Lookup");
            Self.World.Events.Publish(new EvComboResolved(Self.Id, token.Action, resolved, result.Key, result.ToSkill, confirmed, _director.CurrentOrigin));
            return resolved;
        }

        public void ConsumeInput() => _input?.Consume();
    }

    public readonly struct EvComboResolved
    {
        public readonly EntityId Source;
        public readonly string Input, Entry;
        public readonly bool Resolved, ConsumedConfirmation;
        public readonly SkillNodeId Skill;
        public readonly SkillOrigin Origin;
        public EvComboResolved(EntityId source, string input, bool resolved, string entry, SkillNodeId skill, bool consumed, SkillOrigin origin)
        { Source = source; Input = input; Resolved = resolved; Entry = entry; Skill = skill; ConsumedConfirmation = consumed; Origin = origin; }
    }

    public readonly struct EvComboConfirmation
    {
        public readonly EntityId Source, Target;
        public readonly SkillOrigin Origin;
        public readonly string Result;
        public EvComboConfirmation(EntityId source, EntityId target, SkillOrigin origin, string result) { Source = source; Target = target; Origin = origin; Result = result; }
    }
}
