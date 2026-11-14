using System;
using Combat.Core;

namespace Combat.Config
{
    internal static class ConstructNodeCompiler
    {
        public static IEffect Compile(SkillNodeDocument node,string path,CombatContentSnapshot content,Func<string,IEffect[]> outputs)
        {
            var p=new NodeParameters(node,path,"hp","radius","damageCap","decayPeriod","decayDamage","group","view","team","center","forwardOffset","count","spacing","maxAlive","cleanupWithOwner");
            if(node.RuntimeId<=0||content.Constructs.TryGet(node.RuntimeId,out _))throw new ContentValidationException(path+".runtimeId","Expected unique positive construct definition id.");
            var definition=new ConstructDefinition{SpecId=node.RuntimeId,Hp=p.Number("hp",5,.001f,10000000),Radius=p.Number("radius",.25f,0,10),
                DamageCap=p.Number("damageCap",1,0,10000000),DecayPeriod=p.Number("decayPeriod",5,0,3600),DecayDamage=p.Number("decayDamage",1,0,10000000),
                Group=p.Text("group","barrel"),View=p.Choice("view","barrel","barrel","crate"),Team=p.Integer("team",0,0,30),ForwardOffset=p.Number("forwardOffset",.55f,0,100),
                Count=p.Integer("count",1,1,16),Spacing=p.Number("spacing",1,0,20),MaxAlive=p.Integer("maxAlive",64,1,128),CleanupWithOwner=p.Boolean("cleanupWithOwner",false)};
            if(definition.DecayPeriod>0&&definition.DecayPeriod<.02f)throw new ContentValidationException(path+".parameters.decayPeriod","Decay period must be zero or at least0.02 seconds.");
            content.Constructs.Register(definition);
            definition.OnSpawn=outputs("apply");definition.OnDeath=outputs("death");definition.OnRemoved=outputs("removed");
            return new SpawnConstructEffect(node.RuntimeId,(EffectCenter)Array.IndexOf(new[]{"point","target","source","auto"},p.Choice("center","source","point","target","source","auto")));
        }
    }
}
