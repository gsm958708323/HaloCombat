using System.Collections.Generic;
using Combat.Core;

namespace Combat.Config
{
    internal static class SkillCastRoutingCompiler
    {
        public static SkillDefinition Apply(SkillDefinition definition,string key,NodeParameters p,string path,Dictionary<string,SkillNodeId> skills,CombatContentSnapshot content)
        {
            SkillNodeId Reference(string field)
            {
                string id=p.OptionalIdentity(field);if(id.Length==0)return SkillNodeId.None;
                if(!skills.TryGetValue(id,out var number))throw new ContentValidationException(path+".parameters."+field,"Unknown skill reference: "+id);
                content.Dependencies.Add(ContentDependencies.Skill(key),ContentDependencies.Skill(id));return number;
            }
            definition.AmmoCost=p.Integer("ammoCost",0,0,1000000);
            definition.HitFeedback=(HitFeedbackLevel)System.Array.IndexOf(new[]{"none","light","confirm","heavy","finisher"},p.Choice("hitFeedback","none","none","light","confirm","heavy","finisher"));
            definition.AmmoFallback=Reference("fallbackSkill");definition.RecastSkill=Reference("recastSkill");
            definition.RecastProjectile=p.OptionalIdentity("recastProjectile");definition.RequiredProjectile=p.OptionalIdentity("requiredProjectile");
            if(definition.RecastSkill.IsValid!=(definition.RecastProjectile.Length>0))throw new ContentValidationException(path+".parameters.recastSkill","Recast skill and projectile key must be configured together.");
            if(definition.AmmoFallback.IsValid&&definition.AmmoCost==0)throw new ContentValidationException(path+".parameters.fallbackSkill","Ammo fallback requires a positive ammunition cost.");
            string buffId = p.OptionalIdentity("requiredBuff");
            definition.BuffStackCost = p.Integer("buffStackCost", 0, 1, 1000000);
            if (buffId.Length > 0)
            {
                if (!content.Buffs.TryGet(buffId, out var buff))
                {
                    content.MissingDependency?.Invoke("buffs", buffId);
                    throw new ContentValidationException(path + ".parameters.requiredBuff", "Unknown Buff identity: " + buffId);
                }
                definition.RequiredBuffId = buff.BuffId;
                content.Dependencies.Add(ContentDependencies.Skill(key), ContentDependencies.Buff(buffId));
            }
            else if (definition.BuffStackCost > 0)
                throw new ContentValidationException(path + ".parameters.buffStackCost", "A buff stack cost requires requiredBuff.");
            definition.ReturnWindow = p.Number("returnWindow", 0, 0, 3600);
            if (definition.ReturnWindow > 0 && (definition.Mode != SkillCastMode.Instant || definition.Cooldown <= 0))
                throw new ContentValidationException(path + ".parameters.returnWindow", "An origin return requires an instant cast and positive cooldown.");
            return definition;
        }
    }
}
