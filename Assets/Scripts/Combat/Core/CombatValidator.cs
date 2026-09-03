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

            if (data.Timelines == null)
                r.Errors.AppendLine("no timelines");
            else
            {
                CheckTl(data, TimelineId.TL_G1, r);
                CheckTl(data, TimelineId.TL_G2, r);
            }

            if (data.Motor.JumpSpeed <= 0f)
                r.Errors.AppendLine("JumpSpeed");
            return r;
        }

        static void CheckTl(BakedCombatData data, TimelineId id, ValidateReport r)
        {
            if (!data.Timelines.TryGet(id, out var so) || so == null)
            {
                r.Errors.AppendLine("missing " + id.Value);
                return;
            }

            if (so.Duration <= 0f)
                r.Errors.AppendLine("duration " + id.Value);
            if (so.Clips == null) return;
            for (int i = 0; i < so.Clips.Length; i++)
            {
                var c = so.Clips[i];
                if (c.Start < 0f || c.End < c.Start || c.Start > so.Duration)
                    r.Errors.AppendLine("clip range " + id.Value + " #" + i);
                if (c.Kind == ClipKind.Move && c.End - c.Start < 1e-5f)
                    r.Errors.AppendLine("move duration 0 " + id.Value);
                if (c.Kind == ClipKind.Hitbox && (c.OnHit == null || c.OnHit.Length == 0))
                    r.Warnings.AppendLine("empty hitbox " + id.Value + " #" + i);
            }
        }
    }
}
