using System;
using System.Collections.Generic;
using Combat.Core;

namespace Combat.Config
{
    internal static class BuffCompiler
    {
        static readonly string[] Attributes={"maxHp","attack","defense","moveSpeed","critRate","damageDealt","damageTaken","actionSpeed"};
        static readonly AttrId[] Ids={AttrId.MaxHp,AttrId.Atk,AttrId.Def,AttrId.MoveSpeed,AttrId.CritRate,AttrId.DmgDealMul,AttrId.DmgTakenMul,AttrId.ActionSpeed};
        public static void Register(BuffDocument[] documents,CombatContentSnapshot content)
        {
            if(documents==null||documents.Length>128)Fail("$.buffs","Expected up to128 Buff definitions.");
            var keys=new HashSet<string>();var ids=new HashSet<int>();
            for(int i=0;i<documents.Length;i++)
            {
                string path="$.buffs["+i+"]";var d=documents[i];
                if(d==null)Fail(path,"Buff is required.");
                if(string.IsNullOrWhiteSpace(d.Id)||!keys.Add(d.Id))Fail(path+".id","Expected unique nonempty identity.");
                if(d.RuntimeId<=0||!ids.Add(d.RuntimeId))Fail(path+".runtimeId","Expected unique positive identity.");
                int stack=Array.IndexOf(new[]{"refresh","add","independent","reject"},d.Stack);
                if(stack<0)Fail(path+".stack","Unknown stacking policy.");
                if(d.MaxStacks<1||d.MaxStacks>128)Fail(path+".maxStacks","Expected1..128 stacks.");
                if(d.MutexGroup<0)Fail(path+".mutexGroup","Expected nonnegative group.");
                var spec=new DurationSpec {Key=d.Id,Name=d.Name,BuffId=d.RuntimeId,Duration=d.Permanent?0:Number(d.Duration,path+".duration",.001f,3600),
                    TickInterval=Number(d.Interval,path+".interval",0,3600),Stack=(StackPolicy)stack,MaxStacks=d.MaxStacks,MutexGroup=d.MutexGroup};
                if(spec.TickInterval>0&&spec.TickInterval<.001f)Fail(path+".interval","Minimum period is0.001s.");
                if(d.Tags==null||d.Tags.Length>32)Fail(path+".tags","Expected up to32 tags.");
                var tagIds=new HashSet<int>();spec.GrantedTags=new TagId[d.Tags.Length];
                for(int t=0;t<d.Tags.Length;t++)
                {if(d.Tags[t]<=0||!tagIds.Add(d.Tags[t]))Fail(path+".tags["+t+"]","Expected unique positive Tag identity.");spec.GrantedTags[t]=new TagId(d.Tags[t]);}
                if(d.Modifiers==null||d.Modifiers.Length>32)Fail(path+".modifiers","Expected up to32 modifiers.");
                spec.Modifiers=new Modifier[d.Modifiers.Length];
                for(int m=0;m<d.Modifiers.Length;m++)
                {
                    var modifier=d.Modifiers[m];string field=path+".modifiers["+m+"]";
                    if(modifier==null)Fail(field,"Modifier is required.");
                    int attribute=Array.IndexOf(Attributes,modifier.Attribute),operation=Array.IndexOf(new[]{"add","mul","override"},modifier.Operation);
                    if(attribute<0)Fail(field+".attribute","Unknown or non-modifiable attribute.");
                    if(operation<0)Fail(field+".operation","Unknown operation.");
                    spec.Modifiers[m]=new Modifier {Attr=Ids[attribute],Op=(ModOp)operation,Value=Number(modifier.Value,field+".value",operation==0?-1000000:0,1000000),Priority=modifier.Priority};
                }
                content.Buffs.Register(spec);content.Dependencies.Register(ContentDependencies.Buff(d.Id));
            }
        }
        public static void Link(BuffDocument document,string path,CombatContentSnapshot content,HashSet<int> projectiles,HashSet<int> areas,HashSet<int> summons)
        {
            var graph=new EffectGraphIndex(ContentDependencies.Buff(document.Id),document.Nodes,document.Edges,path,true);
            var root=graph.Root("buff-events");new NodeParameters(root,graph.NodePath(root));
            var compiler=new SkillEffectCompiler(graph,content,projectiles,areas,summons);
            content.Buffs.TryGet(document.Id,out var spec);
            spec.OnApply=compiler.Outputs(root,"apply");spec.OnStack=compiler.Outputs(root,"stack");spec.OnPeriod=compiler.Outputs(root,"period");
            spec.OnExpire=compiler.Outputs(root,"expire");spec.OnHurted=compiler.Outputs(root,"hurt");spec.OnOwnerCast=compiler.Outputs(root,"cast");spec.OnRemoved=compiler.Outputs(root,"removed");
            if(spec.OnPeriod.Length>0&&spec.TickInterval<=0)Fail(path+".interval","Periodic effects need a positive interval.");
            graph.CheckComplete();
        }
        static float Number(float value,string path,float min,float max)
        {if(!SkillSceneValidation.Finite(value)||value<min||value>max)Fail(path,"Expected finite value in ["+min+","+max+"].");return value;}
        static void Fail(string path,string message)=>throw new ContentValidationException(path,message);
    }
}
