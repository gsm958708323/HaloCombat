using System;
using System.Collections.Generic;

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
            var skillNumbers=new HashSet<int>();var buffNumbers=new HashSet<int>();
            for(int i=0;i<project.Skills.Length;i++)
            {
                var skill=project.Skills[i];string path="$.skills["+i+"]";
                Number(skill.RuntimeId,skillNumbers,path+".runtimeId");
                Graph(skill.Nodes,skill.Edges,path,false,buffs,blueprints,checkRetainedReferences);
            }
            for(int i=0;i<project.Buffs.Length;i++)
            {
                var buff=project.Buffs[i];string path="$.buffs["+i+"]";
                Number(buff.RuntimeId,buffNumbers,path+".runtimeId");
                Graph(buff.Nodes,buff.Edges,path,true,buffs,blueprints,checkRetainedReferences);
            }
            for(int i=0;i<project.SummonBlueprints.Length;i++)
                if(!skills.Contains(project.SummonBlueprints[i].SkillId??""))Fail("$.summonBlueprints["+i+"].skillId","Unknown skill reference.");
            Editor(project.Editor,skills,buffs);
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

        static void Graph(SkillNodeDocument[] nodes,SkillEdgeDocument[] edges,string path,bool buff,HashSet<string> buffs,HashSet<string> blueprints,bool checkRetainedReferences)
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

        static void Editor(SkillEditorDocument editor,HashSet<string> skills,HashSet<string> buffs)
        {
            if(editor==null)return;
            if(!string.IsNullOrEmpty(editor.SelectedSkill)&&!skills.Contains(editor.SelectedSkill))Fail("$.editor.selectedSkill","Unknown skill.");
            if(!string.IsNullOrEmpty(editor.SelectedBuff)&&!buffs.Contains(editor.SelectedBuff))Fail("$.editor.selectedBuff","Unknown Buff.");
            if(editor.Viewports==null||editor.Viewports.Length>1024)Fail("$.editor.viewports","Invalid viewport array.");
            var views=new HashSet<string>();
            foreach(var view in editor.Viewports)
            {
                if(view==null)Fail("$.editor.viewports","Null viewport.");
                string path="$.editor.viewports["+view.Kind+":"+view.Id+"]";
                var ids=view.Kind=="skill"?skills:view.Kind=="buff"?buffs:null;
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
