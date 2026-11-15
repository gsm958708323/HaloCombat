using System;
using Combat.Core;

namespace Combat.Config
{
    internal static class ProjectileBehaviorCompiler
    {
        internal static readonly string[] Fields={"maxHits","chain","chainCount","chainRadius","chainMode"};
        public static (int Hits,ProjectileChainDefinition Chain) Compile(EffectGraphIndex graph,SkillNodeDocument projectile,NodeParameters inline,string path)
        {
            var node=graph.ProjectileBehavior(projectile);
            if(node==null)return ReadInline(inline,path);
            foreach(string field in Fields)if(projectile.Parameters[field]!=null)
                throw new ContentValidationException(path+".parameters."+field,"Connected behavior owns this field; remove the inline value or extract it into the behavior node.");
            var p=new NodeParameters(node,graph.NodePath(node),"mode","maxHits","unlimited","chainCount","chainRadius","chainMode");
            string mode=p.Choice("mode","pierce","pierce","chain");
            int hits=p.Integer("maxHits",2,1,1000000);bool unlimited=p.Boolean("unlimited",false);
            int count=p.Integer("chainCount",3,1,64);float radius=p.Number("chainRadius",6,.01f,1000);
            string strategy=p.Choice("chainMode","nearest","nearest","random");
            return mode=="pierce"?(unlimited?0:hits,null):(1,new ProjectileChainDefinition{ExtraChains=count,Radius=radius,Mode=strategy=="random"?ChainTargetMode.Random:ChainTargetMode.Nearest});
        }
        static (int,ProjectileChainDefinition) ReadInline(NodeParameters p,string path)
        {
            int hits=p.Integer("maxHits",1,0,1000000),count=p.Integer("chainCount",2,1,64);float radius=p.Number("chainRadius",6,.01f,1000);
            string mode=p.Choice("chainMode","nearest","nearest","random");
            var chain=p.Boolean("chain",false)?new ProjectileChainDefinition{ExtraChains=count,Radius=radius,Mode=mode=="random"?ChainTargetMode.Random:ChainTargetMode.Nearest}:null;
            if(chain!=null&&hits==0)throw new ContentValidationException(path+".parameters.maxHits","Chain needs a finite per-segment hit budget; unlimited piercing has no segment endpoint.");
            return (hits,chain);
        }
    }
}
