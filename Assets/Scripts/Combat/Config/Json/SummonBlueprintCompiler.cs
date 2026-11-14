using System.Collections.Generic;
using Combat.Core;

namespace Combat.Config
{
    internal static class SummonBlueprintCompiler
    {
        public static void Compile(SummonBlueprintDocument[] documents,Dictionary<string,SkillNodeId> skills,SummonBlueprintCatalog catalog)
        {
            if(documents==null||documents.Length>128)throw new ContentValidationException("$.summonBlueprints","Expected up to128 blueprints.");
            var ids=new HashSet<string>();
            for(int i=0;i<documents.Length;i++)
            {
                var d=documents[i];string path="$.summonBlueprints["+i+"]";
                if(d==null)throw new ContentValidationException(path,"Blueprint is required.");
                if(string.IsNullOrWhiteSpace(d.Id)||!ids.Add(d.Id))throw new ContentValidationException(path+".id","Expected unique nonempty identity.");
                if(d.Id=="caster"||d.Id=="target"||d.Id=="projectile"||d.Id=="aoe")throw new ContentValidationException(path+".id","Blueprint identity is reserved for runtime bodies.");
                if(d.SkillId==null||!skills.TryGetValue(d.SkillId,out var skill))throw new ContentValidationException(path+".skillId","Unknown attack skill: "+d.SkillId);
                var value=new SummonBlueprint
                {
                    Id=d.Id,Name=d.Name,SkillKey=d.SkillId,Skill=skill,
                    Hp=Number(d.Hp,path+".hp",.01f,10000000),Attack=Number(d.Attack,path+".attack",0,1000000),Defense=Number(d.Defense,path+".defense",0,1000000),
                    MoveSpeed=Number(d.MoveSpeed,path+".moveSpeed",0,100),CollisionRadius=Number(d.CollisionRadius,path+".collisionRadius",0,10),
                    Energy=Number(d.Energy,path+".energy",0,1000000),Recovery=Number(d.Recovery,path+".recovery",0,1000000),
                    AcquireRadius=Number(d.AcquireRadius,path+".acquireRadius",.01f,1000),
                    FollowRange=Number(d.FollowRange,path+".followRange",0,100),HoldDuration=Number(d.HoldDuration,path+".holdDuration",.02f,120)
                };
                value.InitialEnergy=Number(d.InitialEnergy??d.Energy,path+".initialEnergy",0,value.Energy);
                value.AttackRange=Number(d.AttackRange,path+".attackRange",.01f,value.AcquireRadius);
                catalog.Register(value);
            }
        }
        static float Number(float value,string path,float minimum,float maximum)
        {
            if(!SkillSceneValidation.Finite(value)||value<minimum||value>maximum)throw new ContentValidationException(path,"Expected finite value in ["+minimum+","+maximum+"].");
            return value;
        }
    }
}
