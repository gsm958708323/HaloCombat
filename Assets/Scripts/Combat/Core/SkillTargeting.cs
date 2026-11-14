using System;
using System.Collections.Generic;

namespace Combat.Core
{
    public enum SkillTargetMode { Point, Entity, Nearest, Random, Direction, Self }

    public static class SkillTargeting
    {
        public static bool TryResolve(Actor caster, SkillDefinition definition, in SkillAim input, out SkillAim aim)
        {
            aim = input;
            var origin = caster.GetComp<TransformComp>().Position;
            if (definition.TargetMode == SkillTargetMode.Self)
            { aim = new SkillAim(caster.Id, origin, input.Yaw); return true; }
            if (definition.TargetMode == SkillTargetMode.Point) return input.HasPoint;
            if (definition.TargetMode == SkillTargetMode.Direction)
            {
                float yaw = caster.World.Random.Next01() * 360;
                var f = LocomotionComp.ForwardFromYaw(yaw);
                aim = new SkillAim(EntityId.Invalid, new SimVec3(origin.X+f.X*definition.TargetRange,origin.Y,origin.Z+f.Z*definition.TargetRange),yaw);
                return true;
            }
            var candidates = new List<Actor>();
            foreach (var actor in caster.World.RegistryActive())
            {
                if (actor == caster || !actor.TryGetComp<HealthComp>(out _) || actor.GetComp<TagComp>().Has(CommonTags.Dead)) continue;
                if (!actor.TryGetComp<TeamComp>(out var team) || !caster.GetComp<TeamComp>().IsHostileTo(team)) continue;
                var p = actor.GetComp<TransformComp>().Position;
                if (DistanceSquared(p,origin) <= definition.TargetRange*definition.TargetRange) candidates.Add(actor);
            }
            if (candidates.Count == 0) return false;
            Actor selected = null;
            if (definition.TargetMode == SkillTargetMode.Entity && input.Target.IsValid)
            {
                foreach (var candidate in candidates) if (candidate.Id == input.Target) { selected = candidate; break; }
                if (selected == null) return false;
            }
            else if (definition.TargetMode == SkillTargetMode.Random)
                selected = candidates[Math.Min(candidates.Count-1,(int)(caster.World.Random.Next01()*candidates.Count))];
            else
            {
                var reference = definition.TargetMode == SkillTargetMode.Entity && input.HasPoint ? input.Point : origin;
                float best = float.MaxValue;
                foreach (var candidate in candidates)
                {
                    float distance = DistanceSquared(candidate.GetComp<TransformComp>().Position,reference);
                    if(distance<best){best=distance;selected=candidate;}
                }
            }
            var point = selected.GetComp<TransformComp>().Position;
            aim = new SkillAim(selected.Id,point,LocomotionComp.YawFromStick(new SimVec3(point.X-origin.X,0,point.Z-origin.Z)));
            return true;
        }
        static float DistanceSquared(SimVec3 a,SimVec3 b) => (a.X-b.X)*(a.X-b.X)+(a.Z-b.Z)*(a.Z-b.Z);
    }
}
