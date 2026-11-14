namespace Combat.Core
{
    internal static class SkillCastRouting
    {
        public static bool TryResolve(Actor actor,SkillCatalog catalog,SkillNodeId requested,out SkillDefinition selected,out string error)
        {
            selected=null;error=null;
            for(int depth=0;depth<9;depth++)
            {
                if(!catalog.TryGet(requested,out selected)){error="Unknown skill.";return false;}
                if(selected.RecastSkill.IsValid&&actor.TryGetComp<TrackedProjectilesComp>(out var tracker)&&tracker.TryFind(selected.RecastProjectile,out _))
                {requested=selected.RecastSkill;continue;}
                if(selected.AmmoCost>0&&(!actor.TryGetComp<AmmoComp>(out var ammo)||!ammo.CanSpend(selected.AmmoCost))&&selected.AmmoFallback.IsValid)
                {requested=selected.AmmoFallback;continue;}
                return true;
            }
            error="Skill routing exceeds the supported depth.";selected=null;return false;
        }
    }
}
