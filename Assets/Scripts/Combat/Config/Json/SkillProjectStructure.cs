using System;
using System.Collections.Generic;
using System.Linq;

namespace Combat.Config
{
    // A persistable draft may be disconnected or empty. Compilation remains a separate gate.
    public static class SkillProjectStructure
    {
        public static void Validate(SkillProjectDocument project)=>Validate(project,true);
        internal static void ValidateRuntimeStructure(SkillProjectDocument project)=>Validate(project,false);
        static void Validate(SkillProjectDocument project,bool checkRetainedReferences)
        {
            if(project==null||project.FormatVersion!=1)Fail("$.formatVersion","Only version 1 is supported.");
            Identity(project.Id,"$.id");
            SkillSceneValidation.Validate(project.Preview);
            var skills=Identities(project.Skills,s=>s.Id,"$.skills",512);
            var buffs=Identities(project.Buffs,b=>b.Id,"$.buffs",512);
            var blueprints=Identities(project.SummonBlueprints,b=>b.Id,"$.summonBlueprints",256);
            var timelines=Identities(project.Timelines,t=>t.Id,"$.timelines",512);
            var projectiles=Identities(project.Projectiles,p=>p.Id,"$.projectiles",512);
            var skillNumbers=new HashSet<int>();var buffNumbers=new HashSet<int>();
            for(int i=0;i<project.Skills.Length;i++)
            {
                var skill=project.Skills[i];string path="$.skills["+i+"]";
                Number(skill.RuntimeId,skillNumbers,path+".runtimeId");
                Graph(skill.Nodes,skill.Edges,path,false,buffs,blueprints,projectiles,checkRetainedReferences);
                foreach(var node in skill.Nodes)if(node.Kind=="cast")foreach(string field in new[]{"fallbackSkill","recastSkill"})Reference(node,field,skills,path+".nodes["+node.Id+"]");
                TimelineReferences(skill.Timeline,skill.Nodes,path+".timeline");
                if(skill.TimelineRef!=null)
                {
                    if(skill.Timeline!=null)Fail(path+".timelineRef","Inline timeline and shared reference are mutually exclusive.");
                    if(!timelines.Contains(skill.TimelineRef))Fail(path+".timelineRef","Unknown shared timeline reference.");
                }
            }
            for(int i=0;i<project.Buffs.Length;i++)
            {
                var buff=project.Buffs[i];string path="$.buffs["+i+"]";
                Number(buff.RuntimeId,buffNumbers,path+".runtimeId");
                Graph(buff.Nodes,buff.Edges,path,true,buffs,blueprints,projectiles,checkRetainedReferences);
            }
            for(int i=0;i<project.SummonBlueprints.Length;i++)
                if(!skills.Contains(project.SummonBlueprints[i].SkillId??""))Fail("$.summonBlueprints["+i+"].skillId","Unknown skill reference.");
            foreach(var timeline in project.Timelines)
            {
                string path="$.timelines["+timeline.Id+"]";
                if(timeline.Timeline==null)Fail(path+".timeline","Timeline is required.");
                Graph(timeline.Nodes,timeline.Edges,path,true,buffs,blueprints,projectiles,checkRetainedReferences);
                if(timeline.Nodes.Any(n=>n.Kind=="buff-events"))Fail(path+".nodes","Shared timeline graph has no lifecycle root.");
                TimelineReferences(timeline.Timeline,timeline.Nodes,path+".timeline");
            }
            foreach(var projectile in project.Projectiles)
            {
                string path="$.projectiles["+projectile.Id+"]";
                Graph(projectile.Nodes,projectile.Edges,path,true,buffs,blueprints,projectiles,checkRetainedReferences);
                var root=projectile.Nodes.SingleOrDefault(n=>n.Id==projectile.Root);
                if(root==null||root.Kind!="projectile"||!root.Enabled)Fail(path+".root","Expected an enabled projectile definition node.");
                if(projectile.Nodes.Any(n=>n.Kind=="buff-events"))Fail(path+".nodes","Projectile definition has no lifecycle root.");
                foreach(string field in new[]{"multi","count","spread"})if(root.Parameters[field]!=null)Fail(path+".nodes["+root.Id+"].parameters."+field,"Emission parameters belong to projectile-ref.");
            }
            Editor(project.Editor,skills,buffs,timelines,projectiles);
            RuntimeReferences(project.Runtime,skills);
        }
        static void TimelineReferences(TimelineDocument timeline,SkillNodeDocument[] nodes,string path)
        {
            if(timeline==null)return;
            if(timeline.Clips==null||timeline.Payloads==null)Fail(path,"Timeline arrays are required.");
            var ids=new HashSet<string>();foreach(var node in nodes)ids.Add(node.Id);
            Action<string[],string> check=(entries,p)=>{if(entries==null)Fail(p,"Effect entry array is required.");foreach(string id in entries)if(!ids.Contains(id??""))Fail(p,"Unknown effect entry: "+id);};
            foreach(var clip in timeline.Clips){if(clip==null)Fail(path+".clips","Null clip.");check(clip.Effects,path+".clips["+clip.Id+"].effects");}
            foreach(var payload in timeline.Payloads){if(payload==null)Fail(path+".payloads","Null payload.");check(payload.Effects,path+".payloads["+payload.Id+"].effects");}
        }
        static void RuntimeReferences(RuntimeContentDocument runtime,HashSet<string> skills)
        {
            if(runtime==null)return;
            Identities(runtime.Actors,a=>a.Id,"$.runtime.actors",256);Identities(runtime.Combos,c=>c.Id,"$.runtime.combos",1024);
            Action<string,string> check=(id,path)=>{if(!string.IsNullOrEmpty(id)&&!skills.Contains(id))Fail(path,"Unknown skill reference: "+id);};
            foreach(var row in runtime.Combos){check(row.ToSkill,"$.runtime.combos["+row.Id+"].toSkill");if(row.PreSkills==null)Fail("$.runtime.combos","PreSkills array is required.");foreach(string id in row.PreSkills)check(id,"$.runtime.combos["+row.Id+"].preSkills");}
            foreach(var actor in runtime.Actors){string p="$.runtime.actors["+actor.Id+"]";check(actor.SkillId,p+".skillId");check(actor.DodgeSkill,p+".dodgeSkill");if(actor.Slots==null)Fail(p+".slots","Slot array is required.");foreach(string id in actor.Slots)check(id,p+".slots");}
            if(runtime.Arena!=null)
            {
                var actors=new HashSet<string>(runtime.Actors.Select(a=>a.Id));
                foreach(string id in new[]{runtime.Arena.Player,runtime.Arena.Enemy,runtime.Arena.TrainingTarget})if(!actors.Contains(id??""))Fail("$.runtime.arena","Unknown actor reference: "+id);
            }
        }

        static HashSet<string> Identities<T>(T[] values,Func<T,string> key,string path,int limit) where T:class
        {
            if(values==null||values.Length>limit)Fail(path,"Expected an array with at most "+limit+" entries.");
            var keys=new HashSet<string>();
            foreach(var value in values)
            {
                if(value==null)Fail(path,"Null entries are not allowed.");
                string id=key(value);Identity(id,path+".id");
                if(!keys.Add(id))Fail(path+"["+id+"].id","Duplicate identity.");
            }
            return keys;
        }

        static void Graph(SkillNodeDocument[] nodes,SkillEdgeDocument[] edges,string path,bool buff,HashSet<string> buffs,HashSet<string> blueprints,HashSet<string> projectiles,bool checkRetainedReferences)
        {
            var nodeIds=Identities(nodes,n=>n.Id,path+".nodes",256);
            Identities(edges,e=>e.Id,path+".edges",1024);
            foreach(var node in nodes)
            {
                string p=path+".nodes["+node.Id+"]";
                if(!EffectGraphIndex.KnownKind(node.Kind))Fail(p+".kind","Unknown node kind: "+node.Kind);
                if(buff?(node.Kind=="target"||node.Kind=="cast"||node.Kind=="skill-events"):node.Kind=="buff-events")Fail(p+".kind","Root kind is invalid for this graph.");
                if(node.Position==null||!Finite(node.Position.X)||!Finite(node.Position.Y))Fail(p+".position","Expected finite coordinates.");
                if(node.Parameters==null)Fail(p+".parameters","Expected a parameter object.");
                // Runtime compilation checks only active fields in SkillEffectCompiler. Its
                // publication closure intentionally excludes definitions used by disabled nodes.
                if(checkRetainedReferences)
                {
                    if(node.Kind=="buff"||node.Kind=="dispel")Reference(node,"buff",buffs,p);
                    if(node.Kind=="delivery")Reference(node,"blueprint",blueprints,p);
                    if(node.Kind=="projectile-ref")Reference(node,"projectile",projectiles,p);
                }
            }
            foreach(var edge in edges)
            {
                string p=path+".edges["+edge.Id+"]";
                if(!nodeIds.Contains(edge.Source??"")||!nodeIds.Contains(edge.Target??""))Fail(p,"Unknown edge endpoint.");
                Identity(edge.SourcePort,p+".sourcePort");Identity(edge.TargetPort,p+".targetPort");
                if(edge.Order<0)Fail(p+".order","Order cannot be negative.");
            }
        }

        static void Reference(SkillNodeDocument node,string field,HashSet<string> values,string path)
        {
            var token=node.Parameters[field];if(token==null)return;
            if(token.Type!=Newtonsoft.Json.Linq.JTokenType.String)Fail(path+".parameters."+field,"Expected an identity string.");
            string id=(string)token;
            if(!string.IsNullOrEmpty(id)&&!values.Contains(id))Fail(path+".parameters."+field,"Unknown shared reference: "+id);
        }

        static void Editor(SkillEditorDocument editor,HashSet<string> skills,HashSet<string> buffs,HashSet<string> timelines,HashSet<string> projectiles)
        {
            if(editor==null)return;
            if(!string.IsNullOrEmpty(editor.SelectedSkill)&&!skills.Contains(editor.SelectedSkill))Fail("$.editor.selectedSkill","Unknown skill.");
            if(!string.IsNullOrEmpty(editor.SelectedBuff)&&!buffs.Contains(editor.SelectedBuff))Fail("$.editor.selectedBuff","Unknown Buff.");
            if(!string.IsNullOrEmpty(editor.SelectedTimeline)&&!timelines.Contains(editor.SelectedTimeline))Fail("$.editor.selectedTimeline","Unknown timeline.");
            if(!string.IsNullOrEmpty(editor.SelectedProjectile)&&!projectiles.Contains(editor.SelectedProjectile))Fail("$.editor.selectedProjectile","Unknown projectile.");
            if(editor.Viewports==null||editor.Viewports.Length>1024)Fail("$.editor.viewports","Invalid viewport array.");
            var views=new HashSet<string>();
            foreach(var view in editor.Viewports)
            {
                if(view==null)Fail("$.editor.viewports","Null viewport.");
                string path="$.editor.viewports["+view.Kind+":"+view.Id+"]";
                var ids=view.Kind=="skill"?skills:view.Kind=="buff"?buffs:view.Kind=="timeline"?timelines:view.Kind=="projectile"?projectiles:null;
                if(ids==null||!ids.Contains(view.Id??""))Fail(path,"Unknown graph identity.");
                if(!views.Add(view.Kind+":"+view.Id))Fail(path,"Duplicate graph viewport.");
                if(!Finite(view.X)||!Finite(view.Y)||!Finite(view.Zoom)||view.Zoom<.1f||view.Zoom>4)Fail(path,"Invalid viewport coordinates or zoom.");
            }
        }

        static void Identity(string value,string path){if(string.IsNullOrWhiteSpace(value))Fail(path,"Identity is required.");}
        static void Number(int value,HashSet<int> values,string path){if(value<=0||!values.Add(value))Fail(path,"Expected unique positive runtime identity.");}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static void Fail(string path,string message)=>throw new ContentValidationException(path,message);
    }
}
