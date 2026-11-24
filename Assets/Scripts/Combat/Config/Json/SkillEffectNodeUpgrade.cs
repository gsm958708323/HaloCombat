using System;
using System.Collections.Generic;
using System.Linq;
using Combat.Core;
using Newtonsoft.Json.Linq;

namespace Combat.Config
{
    // Format-1 portable files are a public contract. Normalize them once at the
    // protocol boundary; the compiler and Core execute only effect nodes.
    public static class SkillEffectNodeUpgrade
    {
        internal static SkillProjectDocument ForCompilation(SkillProjectDocument project)
        {
            // Compilation never rewrites a caller's draft or previously recorded input.
            if(project==null)return null;
            return Apply(SkillJsonCodec.Read<SkillProjectDocument>(SkillJsonCodec.Write(project)));
        }
        public static SkillProjectDocument Apply(SkillProjectDocument project)
        {
            if(project==null)return null;
            foreach(var graph in project.Skills??Array.Empty<SkillGraphDocument>())
                (graph.Nodes,graph.Edges)=Graph(graph.Nodes,graph.Edges,graph.Timeline);
            foreach(var graph in project.Timelines??Array.Empty<SharedTimelineDocument>())
                (graph.Nodes,graph.Edges)=Graph(graph.Nodes,graph.Edges,graph.Timeline);
            foreach(var graph in project.Projectiles??Array.Empty<SharedProjectileDocument>())
                (graph.Nodes,graph.Edges)=Graph(graph.Nodes,graph.Edges,null);
            foreach(var graph in project.Areas??Array.Empty<SharedAreaDocument>())
                (graph.Nodes,graph.Edges)=Graph(graph.Nodes,graph.Edges,null);
            foreach(var graph in project.Buffs??Array.Empty<BuffDocument>())
                (graph.Nodes,graph.Edges)=Graph(graph.Nodes,graph.Edges,null);
            return project;
        }
        static int Integer(SkillNodeDocument source,string field,int fallback,int minimum,int maximum)
        {
            var token=source.Parameters?[field];if(token==null)return fallback;
            var node=new SkillNodeDocument{Id=source.Id,Parameters=new JObject{[field]=token.DeepClone()}};
            return new NodeParameters(node,"$.nodes["+source.Id+"]",field).Integer(field,fallback,minimum,maximum);
        }
        static (SkillNodeDocument[],SkillEdgeDocument[]) Graph(SkillNodeDocument[] source,SkillEdgeDocument[] links,TimelineDocument timeline)
        {
            if(source==null||links==null)return (source,links);
            if(source.Any(n=>n==null)||links.Any(e=>e==null))throw new ContentValidationException("$.nodes","Null graph entries are not allowed.");
            var nodes=source.ToList();var edges=links.ToList();
            string Identity(string wanted)
            {string id=wanted;int count=1;while(nodes.Any(n=>n?.Id==id))id=wanted+"-"+count++;return id;}
            void Connect(string from,string port,string to)
            {
                string id=from+"-"+port+"-"+to;int count=1;while(edges.Any(e=>e.Id==id))id=from+"-"+port+"-"+to+"-"+count++;
                int order=edges.Where(e=>e.Source==from&&e.SourcePort==port).Select(e=>e.Order).DefaultIfEmpty(-1).Max()+1;
                edges.Add(new SkillEdgeDocument{Id=id,Source=from,SourcePort=port,Target=to,TargetPort="execute",Order=order});
            }
            SkillNodeDocument Add(SkillNodeDocument owner,string kind,JObject values)
            {
                var node=new SkillNodeDocument{Id=Identity(owner.Id+"-"+kind),Kind=kind,Enabled=owner.Enabled,
                    Parameters=values,Position=new NodePosition{X=owner.Position?.X??0,Y=(owner.Position?.Y??0)+260}};
                nodes.Add(node);return node;
            }
            foreach(var old in source.Where(n=>n?.Kind=="projectile-behavior"))
            {
                var policy=new NodeParameters(old,"$.nodes["+old.Id+"]","mode","maxHits","unlimited","chainCount","chainRadius","chainMode");
                bool chain=policy.Choice("mode","pierce","pierce","chain")=="chain";
                int maximum=policy.Integer("maxHits",2,1,1000000),chains=policy.Integer("chainCount",3,1,64);
                float radius=policy.Number("chainRadius",6,.01f,1000);string selection=policy.Choice("chainMode","nearest","nearest","random");
                bool unlimited=policy.Boolean("unlimited",false);
                var outputs=edges.Where(e=>e.Source==old.Id&&e.SourcePort=="behavior").ToArray();
                if(outputs.Length==0||edges.Any(e=>e.Source==old.Id&&e.SourcePort!="behavior"))throw new ContentValidationException("$.nodes["+old.Id+"]","Legacy behavior needs a projectile behavior output.");
                if(edges.Any(e=>e.Target==old.Id))throw new ContentValidationException("$.nodes["+old.Id+"]","Legacy behavior cannot have an execution input or form a cycle.");
                foreach(var output in outputs)
                {
                    if(edges.Count(e=>e.Target==output.Target&&e.TargetPort=="behavior")>1)throw new ContentValidationException("$.nodes["+output.Target+"]","Only one legacy behavior is allowed.");
                    var owner=source.FirstOrDefault(n=>n.Id==output.Target);
                    if(owner?.Kind!="projectile"||output.TargetPort!="behavior")throw new ContentValidationException("$.nodes["+old.Id+"]","Legacy behavior only targets a projectile behavior port.");
                    foreach(string field in new[]{"maxHits","chain","chainCount","chainRadius","chainMode"})
                        if(old.Enabled&&owner?.Parameters?[field]!=null)throw new ContentValidationException("$.nodes["+owner.Id+"].parameters."+field,"Legacy behavior and inline policy cannot coexist.");
                }
                var values=chain?new JObject{["count"]=chains,["radius"]=radius,["selection"]=selection}:
                    new JObject{["maxHits"]=unlimited?0:maximum};
                old.Kind=chain?"chain":"pierce";old.Parameters=values;
                foreach(var edge in outputs){edges.Remove(edge);Connect(edge.Target,"hit",old.Id);}
            }
            foreach(var body in source.Where(n=>n?.Kind=="projectile"&&n.Parameters!=null))
            {
                var p=body.Parameters;
                var legacy=new SkillNodeDocument{Id=body.Id,Parameters=new JObject(p.Properties().Where(v=>new[]{"maxHits","chain","chainCount","chainRadius","chainMode"}.Contains(v.Name)).Select(v=>new JProperty(v.Name,v.Value.DeepClone())))};
                var policy=new NodeParameters(legacy,"$.nodes["+body.Id+"]","maxHits","chain","chainCount","chainRadius","chainMode");
                int hits=policy.Integer("maxHits",1,0,1000000);bool chain=policy.Boolean("chain",false);
                int count=policy.Integer("chainCount",2,1,64);float radius=policy.Number("chainRadius",6,.01f,1000);string selection=policy.Choice("chainMode","nearest","nearest","random");
                if(legacy.Parameters.HasValues&&edges.Any(e=>e.Source==body.Id&&e.SourcePort=="hit"&&nodes.Any(n=>n.Id==e.Target&&n.Enabled&&(n.Kind=="chain"||n.Kind=="pierce"))))
                    throw new ContentValidationException("$.nodes["+body.Id+"].parameters."+legacy.Parameters.Properties().First().Name,"Legacy contact parameters cannot coexist with effect nodes.");
                if(chain&&hits==0)throw new ContentValidationException("$.nodes["+body.Id+"].parameters.maxHits","Chain needs a finite segment hit budget.");
                if(hits!=1)Connect(body.Id,"hit",Add(body,"pierce",new JObject{["maxHits"]=hits}).Id);
                if(chain)Connect(body.Id,"hit",Add(body,"chain",new JObject{["count"]=count,["radius"]=radius,["selection"]=selection}).Id);
                foreach(string field in new[]{"maxHits","chain","chainCount","chainRadius","chainMode"})p.Remove(field);
            }
            foreach(var ray in source.Where(n=>n?.Kind=="delivery"&&(string)n.Parameters?["mode"]=="ray"))
            {
                if((bool?)ray.Parameters["pierce"]==true)
                    Connect(ray.Id,"apply",Add(ray,"pierce",new JObject{["maxHits"]=Integer(ray,"maxTargets",0,0,128)}).Id);
                ray.Parameters.Remove("pierce");
            }
            foreach(var damage in source.Where(n=>n?.Kind=="damage"&&n.Parameters?["scope"]!=null))
            {
                var scope=damage.Parameters["scope"];
                if(scope.Type!=JTokenType.String||!new[]{"target","area"}.Contains((string)scope))throw new ContentValidationException("$.nodes["+damage.Id+"].parameters.scope","Expected target or area.");
                if((string)scope=="target")
                {foreach(string field in new[]{"scope","center","radius","maxTargets"})damage.Parameters.Remove(field);continue;}
                var p=damage.Parameters;var area=Add(damage,"delivery",new JObject{["mode"]="area",["center"]=p["center"]??"point",["radius"]=p["radius"]??3,["maxTargets"]=p["maxTargets"]??0});
                foreach(var edge in edges.Where(e=>e.Target==damage.Id).ToArray())edge.Target=area.Id;
                if(timeline!=null)
                {
                    foreach(var clip in timeline.Clips)clip.Effects=clip.Effects.Select(id=>id==damage.Id?area.Id:id).ToArray();
                    foreach(var payload in timeline.Payloads)payload.Effects=payload.Effects.Select(id=>id==damage.Id?area.Id:id).ToArray();
                }
                Connect(area.Id,"apply",damage.Id);
                foreach(string field in new[]{"scope","center","radius","maxTargets"})p.Remove(field);
            }
            return (nodes.ToArray(),edges.ToArray());
        }
    }
}
