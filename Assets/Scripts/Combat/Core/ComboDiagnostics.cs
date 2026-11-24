using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public sealed class ComboCandidate
    {
        public readonly string Rule;
        public readonly SkillNodeId Skill;
        public readonly int Priority;
        public readonly bool WindowOpen;
        public readonly int[] MissingTags;
        public readonly string[] FailedConditions;
        public ComboCandidate(string rule, SkillNodeId skill, int priority, bool windowOpen, int[] missingTags, string[] failedConditions)
        {
            Rule = rule;
            Skill = skill;
            Priority = priority;
            WindowOpen = windowOpen;
            MissingTags = missingTags ?? Array.Empty<int>();
            FailedConditions = failedConditions ?? Array.Empty<string>();
        }
    }

    public sealed class ComboDecision
    {
        public readonly int Sequence, InputSequence, CastId, ConditionHash;
        public readonly string Input, Rule, Phase, Reason;
        public readonly SkillNodeId FromSkill, ToSkill, ActualSkill;
        public readonly bool WindowOpen, Confirmed;
        public readonly ComboCandidate[] Candidates;
        public ComboDecision(int sequence, int inputSequence, string input, SkillNodeId from, string rule, SkillNodeId to, SkillNodeId actual,
            string phase, string reason, bool window, bool confirmed, int castId, int hash, ComboCandidate[] candidates)
        {
            Sequence = sequence; InputSequence = inputSequence; Input = input; FromSkill = from; Rule = rule; ToSkill = to; ActualSkill = actual;
            Phase = phase; Reason = reason; WindowOpen = window; Confirmed = confirmed; CastId = castId; ConditionHash = hash; Candidates = candidates;
        }
    }

    public readonly struct EvComboDecision
    {
        public readonly EntityId Source;
        public readonly ComboDecision Decision;
        public EvComboDecision(EntityId source, ComboDecision decision) { Source = source; Decision = decision; }
    }

    public sealed partial class ComboComp
    {
        public bool DiagnosticsEnabled { get; set; }
        public ComboDecision LastDecision { get; private set; }
        int _decisionSequence;

        void CaptureDecision(in InputToken input, in ComboResolveResult resolved, string phase, string reason, in ComboEvalContext ctx)
        {
            if (!DiagnosticsEnabled) return;
            var from = _director?.CurrentSkill ?? SkillNodeId.None;
            bool confirmed = _director?.HasComboConfirmation ?? false;
            int press = _input?.HeadSequence ?? 0, hash = 17;
            bool anyWindow = !ctx.IsPlaying;
            unchecked
            {
                foreach (var row in _table.Entries)
                {
                    if (row.Input != input || !ComboTableDefinition.PreMatches(row.PreSkills, from)) continue;
                    bool winOpen = row.Window.IsOpen(ctx);
                    if (winOpen) anyWindow = true;
                    hash = hash * 31 + row.Priority;
                    hash = hash * 31 + (winOpen ? 1 : 0);
                    foreach (var cond in row.Conditions ?? Array.Empty<ComboCondition>())
                        hash = hash * 31 + (cond.Evaluate(ctx) ? 1 : -1) * ((int)cond.Kind * 97 + cond.Tag + cond.BuffRuntimeId);
                }
            }
            bool window = anyWindow || (_tags != null && _tags.Has(CommonTags.Cancel));
            var old = LastDecision;
            bool same = old != null && old.InputSequence == press && old.Input == input.Action && old.FromSkill == from
                && old.Rule == resolved.Key && old.ToSkill == resolved.ToSkill && old.ConditionHash == hash
                && old.WindowOpen == window && old.Confirmed == confirmed;
            if (same && (old.Phase == phase && old.Reason == reason || phase == "matched" && (old.Phase == "cast" || old.Phase == "rejected"))) return;

            var candidates = new List<ComboCandidate>();
            foreach (var row in _table.Entries)
            {
                if (row.Input != input || !ComboTableDefinition.PreMatches(row.PreSkills, from)) continue;
                bool winOpen = row.Window.IsOpen(ctx);
                var missingTags = new List<int>();
                var failed = new List<string>();
                foreach (var cond in row.Conditions ?? Array.Empty<ComboCondition>())
                {
                    if (cond.Evaluate(ctx)) continue;
                    if (cond.Kind == ComboConditionKind.Tag && !cond.Negate) missingTags.Add(cond.Tag);
                    failed.Add(cond.DescribeFailure());
                }
                candidates.Add(new ComboCandidate(row.Key, row.ToSkill, row.Priority, winOpen, missingTags.ToArray(), failed.ToArray()));
            }
            PublishDecision(new ComboDecision(++_decisionSequence, press, input.Action, from, resolved.Key, resolved.ToSkill, SkillNodeId.None,
                phase, reason, window, confirmed, _director?.CurrentOrigin.CastId ?? 0, hash, candidates.ToArray()));
        }

        public void ReportCastOutcome(bool accepted, SkillNodeId actual, string rejection)
        {
            var old = LastDecision;
            if (!DiagnosticsEnabled || old == null || old.Rule == null) return;
            string phase = accepted ? "cast" : "rejected", reason = accepted ? null : rejection;
            if (!accepted && old.Phase == phase && old.Reason == reason && old.ActualSkill == actual) return;
            PublishDecision(new ComboDecision(++_decisionSequence, old.InputSequence, old.Input, old.FromSkill, old.Rule, old.ToSkill, actual,
                phase, reason, old.WindowOpen, old.Confirmed, accepted ? (_director?.CurrentOrigin.CastId ?? 0) : old.CastId, old.ConditionHash, old.Candidates));
        }

        public void ReportBlockedInput(in InputToken input)
            => CaptureDecision(input, default, "blocked", "当前角色状态禁止接招", ComboEvalContext.FromActor(Self));

        void PublishDecision(ComboDecision decision) { LastDecision = decision; Self.World.Events.Publish(new EvComboDecision(Self.Id, decision)); }
        void ResetDiagnostics() { LastDecision = null; _decisionSequence = 0; }
    }
}
