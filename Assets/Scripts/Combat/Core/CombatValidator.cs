using System;
using System.Text;

namespace Combat.Core
{
    public sealed class ValidateReport
    {
        public readonly StringBuilder Errors = new StringBuilder();
        public readonly StringBuilder Warnings = new StringBuilder();
        public bool HasError => Errors.Length > 0;
        public override string ToString() => Errors.ToString() + Warnings.ToString();
    }

    public static class CombatValidator
    {
        public static ValidateReport Validate(BakedCombatData data)
        {
            var r = new ValidateReport();
            if (data == null)
            {
                r.Errors.AppendLine("data null");
                return r;
            }

            if (data.Timelines == null || data.Timelines.Count == 0)
                r.Errors.AppendLine("no timelines");
            else
                foreach (var timeline in data.Timelines.All)
                    CheckTl(timeline, r);

            if (data.Skills != null && data.Skills.Count == 0)
                r.Errors.AppendLine("no skills");
            if (data.Characters != null && data.Characters.Count == 0)
                r.Errors.AppendLine("no characters");
            if (data.Skills != null && data.Timelines != null)
            {
                foreach (var skill in data.Skills.All)
                {
                    if (skill == null || !skill.Id.IsValid || !skill.Timeline.IsValid)
                        r.Errors.AppendLine("invalid skill");
                    else if (!data.Timelines.TryGet(skill.Timeline, out _))
                        r.Errors.AppendLine("skill timeline " + skill.Id.Value);
                    if (skill.Cooldown < 0f)
                        r.Errors.AppendLine("skill cooldown " + skill.Id.Value);
                }
            }
            if (data.Characters != null)
            {
                foreach (var character in data.Characters.All)
                {
                    if (character == null || string.IsNullOrEmpty(character.BlueprintId))
                        r.Errors.AppendLine("invalid character");
                    else if (string.IsNullOrEmpty(character.ViewBlueprintId))
                        r.Errors.AppendLine("character view " + character.BlueprintId);
                }
            }
            if (data.Cues == null)
                r.Errors.AppendLine("no cues");
            if (data.Motor.JumpSpeed <= 0f)
                r.Errors.AppendLine("JumpSpeed");
            return r;
        }

        static void CheckTl(TimelineSO so, ValidateReport r)
        {
            if (so == null)
            {
                r.Errors.AppendLine("null timeline");
                return;
            }

            if (so.Duration <= 0f)
                r.Errors.AppendLine("duration " + so.Id.Value);
            var clips = so.Clips ?? Array.Empty<TimelineClip>();
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                if (c.Start < 0f || c.End <= c.Start || c.End > so.Duration)
                    r.Errors.AppendLine("clip range " + so.Id.Value + " #" + i);
                if (c.Kind == ClipKind.Move && c.End - c.Start < 1e-5f)
                    r.Errors.AppendLine("move duration 0 " + so.Id.Value);
                if (c.Kind == ClipKind.Hitbox && (c.OnHit == null || c.OnHit.Length == 0))
                    r.Warnings.AppendLine("empty hitbox " + so.Id.Value + " #" + i);
            }

            var payloads = so.Payloads ?? Array.Empty<TimelinePayload>();
            for (int i = 0; i < payloads.Length; i++)
            {
                if (payloads[i].Time < 0f || payloads[i].Time > so.Duration)
                    r.Errors.AppendLine("payload range " + so.Id.Value + " #" + i);
                if (payloads[i].Effects == null || payloads[i].Effects.Length == 0)
                    r.Warnings.AppendLine("empty payload " + so.Id.Value + " #" + i);
            }
        }
    }
}
