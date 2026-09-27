using System;

namespace Combat.Core
{
    /// <summary>Runs an effect bag only after positive HP damage. It is independent of DamageEffect's formula.</summary>
    public sealed class AfterDamageEffect : IEffect
    {
        public IEffect[] Effects = Array.Empty<IEffect>();
        public bool ToSource;
        public bool RequireHostile;
        public bool RequireLatestCast;

        public void Apply(ref EffectContext ctx)
        {
            if (!ctx.DamageApplied || ctx.World == null || ctx.Target == null || Effects == null || Effects.Length == 0) return;
            if (RequireHostile && (ctx.Source == null || !ctx.Source.TryGetComp<TeamComp>(out var sourceTeam) ||
                !ctx.Target.TryGetComp<TeamComp>(out var targetTeam) || !sourceTeam.IsHostileTo(targetTeam))) return;
            if (RequireLatestCast && (ctx.Source == null || !ctx.CastId.IsValid ||
                !ctx.Source.TryGetComp<SkillDirectorComp>(out var director) || director.LastCastId != ctx.CastId)) return;
            var receiver = ToSource ? ctx.Source : ctx.Target;
            if (receiver == null) return;
            ctx.World.Deliver(Effects, ctx.Source, receiver, ctx.SnapshotAtk,
                ctx.HasPoint ? (SimVec3?)ctx.Point : null, ctx.HasDir ? (SimVec3?)ctx.Dir : null,
                ctx.BuffStacks, ctx.CastId, ctx.Skill, ctx.HitIndex);
        }
    }
}
