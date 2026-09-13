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
        const float YawEpsilon = 1e-3f;

        /// <summary>
        /// Frozen geometry contract (see TransformComp.YawDegrees): yaw 0 faces +Z like
        /// Unity's forward, a positive angle turns towards +X, and local offsets use
        /// Unity's frame (+Z forward, +X right) so the view layer needs no correction.
        /// Throws when the convention is broken.
        /// </summary>
        public static void VerifyYawConvention()
        {
            var forward = LocomotionComp.ForwardFromYaw(0f);
            if (Math.Abs(forward.X) > YawEpsilon || Math.Abs(forward.Z - 1f) > YawEpsilon)
                throw new InvalidOperationException(
                    "Yaw convention: ForwardFromYaw(0) must be +Z, got X=" + forward.X + " Z=" + forward.Z);
            var right = LocomotionComp.ForwardFromYaw(90f);
            if (Math.Abs(right.X - 1f) > YawEpsilon || Math.Abs(right.Z) > YawEpsilon)
                throw new InvalidOperationException(
                    "Yaw convention: ForwardFromYaw(90) must be +X, got X=" + right.X + " Z=" + right.Z);

            var probes = new[] { -135f, -90f, -45f, 0f, 45f, 90f, 135f, 180f, 225f, 270f };
            for (int i = 0; i < probes.Length; i++)
            {
                float yaw = probes[i];
                float roundTrip = LocomotionComp.YawFromStick(LocomotionComp.ForwardFromYaw(yaw));
                if (Math.Abs(NormalizeDelta(roundTrip - yaw)) > YawEpsilon)
                    throw new InvalidOperationException(
                        "Yaw convention: YawFromStick(ForwardFromYaw(" + yaw + ")) == " + roundTrip);

                var origin = new SimVec3(2f, 1f, -3f);
                var aheadDir = LocomotionComp.ForwardFromYaw(yaw);
                var ahead = CombatGeom.WorldPoint(origin, yaw, new SimVec3(0f, 0f, 1.5f));
                if (!Near(ahead, new SimVec3(origin.X + aheadDir.X * 1.5f, origin.Y, origin.Z + aheadDir.Z * 1.5f)))
                    throw new InvalidOperationException(
                        "Yaw convention: local +Z must be ForwardFromYaw(" + yaw + "), got " + ahead.X + "," + ahead.Z);

                var sideDir = LocomotionComp.ForwardFromYaw(yaw + 90f);
                var side = CombatGeom.WorldPoint(origin, yaw, new SimVec3(1.5f, 0f, 0f));
                if (!Near(side, new SimVec3(origin.X + sideDir.X * 1.5f, origin.Y, origin.Z + sideDir.Z * 1.5f)))
                    throw new InvalidOperationException(
                        "Yaw convention: local +X must be the actor's right at yaw " + yaw + ", got " + side.X + "," + side.Z);
            }
        }

        public static ValidateReport Validate(BakedCombatData data)
        {
            var r = new ValidateReport();
            try
            {
                VerifyYawConvention();
            }
            catch (InvalidOperationException e)
            {
                r.Errors.AppendLine(e.Message);
            }

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

        static bool Near(in SimVec3 a, in SimVec3 b)
            => Math.Abs(a.X - b.X) <= YawEpsilon && Math.Abs(a.Y - b.Y) <= YawEpsilon &&
               Math.Abs(a.Z - b.Z) <= YawEpsilon;

        static float NormalizeDelta(float deg)
        {
            while (deg > 180f) deg -= 360f;
            while (deg < -180f) deg += 360f;
            return deg;
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
