import { clone } from '../model';
import type { AuthoredTimeline,BuffDefinition, GraphEdge, GraphNode, GraphViewport, Project, Skill } from '../model';

export type ContentKind = 'skill' | 'buff' | 'blueprint';
export interface GraphKey { kind:'skill'|'buff';id:string }
export type EditableGraph = Pick<Skill,'id'|'name'|'nodes'|'edges'|'timeline'>;
export interface ContentReference { kind:ContentKind|'actor'|'combo';id:string;name:string;nodeId?:string;field:string }
export const contentLabel = { skill:'技能',buff:'Buff',blueprint:'召唤蓝图',actor:'角色',combo:'连招' };
export const contentItems = (p:Project,kind:ContentKind) => kind==='skill'?p.skills:kind==='buff'?(p.buffs??[]):(p.summonBlueprints??[]);
export const graphOf = (p:Project,key:GraphKey):EditableGraph|undefined => key.kind==='skill'?p.skills.find(s=>s.id===key.id):p.buffs?.find(b=>b.id===key.id);
export const allGraphs = (p:Project) => [...p.skills.map(graph=>({kind:'skill' as const,graph})),...(p.buffs??[]).map(graph=>({kind:'buff' as const,graph}))];

export function updateGraph(p:Project,key:GraphKey,change:(graph:EditableGraph)=>EditableGraph):Project {
  return key.kind==='skill'?{...p,skills:p.skills.map(s=>s.id===key.id?{...s,...change(s)}:s)}:
    {...p,buffs:p.buffs?.map(b=>b.id===key.id?{...b,...change(b)}:b)};
}

function referenceField(node:GraphNode,kind:ContentKind) {
  return kind==='buff'&&(node.kind==='buff'||node.kind==='dispel')?'buff':kind==='blueprint'&&node.kind==='delivery'?'blueprint':null;
}
export function referencesTo(p:Project,kind:ContentKind,id:string):ContentReference[] {
  if(kind==='skill')return [
    ...(p.summonBlueprints??[]).filter(b=>b.skillId===id).map(b=>({kind:'blueprint' as const,id:b.id,name:b.name,field:'skillId'})),
    ...(p.runtime?.actors??[]).flatMap(a=>[...(['skillId','dodgeSkill'] as const).filter(k=>a[k]===id).map(field=>({kind:'actor' as const,id:a.id,name:a.id,field})),...(a.slots??[]).flatMap((v,i)=>v===id?[{kind:'actor' as const,id:a.id,name:a.id,field:'slots['+i+']'}]:[])]),
    ...(p.runtime?.combos??[]).flatMap(c=>[...(c.toSkill===id?[{kind:'combo' as const,id:c.id,name:c.id,field:'toSkill'}]:[]),...c.preSkills.flatMap((v,i)=>v===id?[{kind:'combo' as const,id:c.id,name:c.id,field:'preSkills['+i+']'}]:[])])];
  return allGraphs(p).flatMap(({kind:owner,graph})=>graph.nodes.flatMap(node=>{
    const field=referenceField(node,kind);
    return field&&node.parameters[field]===id?[{kind:owner,id:graph.id,name:graph.name,nodeId:node.id,field}]:[];
  }));
}
function rewriteReferences(p:Project,kind:ContentKind,from:string,to:string):Project {
  if(kind==='skill')return {...p,summonBlueprints:p.summonBlueprints?.map(b=>b.skillId===from?{...b,skillId:to}:b),runtime:p.runtime?{...p.runtime,
    actors:p.runtime.actors.map(a=>({...a,skillId:a.skillId===from?to:a.skillId,dodgeSkill:a.dodgeSkill===from?to:a.dodgeSkill,slots:a.slots?.map(id=>id===from?to:id)})),
    combos:p.runtime.combos.map(c=>({...c,toSkill:c.toSkill===from?to:c.toSkill,preSkills:c.preSkills.map(id=>id===from?to:id)}))}:undefined};
  const nodes=(items:GraphNode[])=>items.map(node=>{const field=referenceField(node,kind);return field&&node.parameters[field]===from?{...node,parameters:{...node.parameters,[field]:to}}:node;});
  return {...p,skills:p.skills.map(s=>({...s,nodes:nodes(s.nodes)})),buffs:p.buffs?.map(b=>({...b,nodes:nodes(b.nodes)}))};
}
function mapContent(p:Project,kind:ContentKind,from:string,to:string):Project {
  return kind==='skill'?{...p,skills:p.skills.map(s=>s.id===from?{...s,id:to}:s)}:
    kind==='buff'?{...p,buffs:p.buffs?.map(b=>b.id===from?{...b,id:to}:b)}:
    {...p,summonBlueprints:p.summonBlueprints?.map(b=>b.id===from?{...b,id:to}:b)};
}
export function renameContent(p:Project,kind:ContentKind,id:string,input:string):Project {
  const next=input.trim();const items=contentItems(p,kind);
  if(!next)throw new Error('ID 不能为空');
  if(!items.some(item=>item.id===id))throw new Error('内容已不存在');
  if(next===id)return p;
  if(items.some(item=>item.id===next))throw new Error(contentLabel[kind]+' ID 已存在：'+next);
  let result=mapContent(rewriteReferences(p,kind,id,next),kind,id,next);
  if(kind!=='blueprint'&&p.editor)result={...result,editor:{...p.editor,
    selectedSkill:kind==='skill'&&p.editor.selectedSkill===id?next:p.editor.selectedSkill,
    selectedBuff:kind==='buff'&&p.editor.selectedBuff===id?next:p.editor.selectedBuff,
    viewports:p.editor.viewports?.map(v=>v.kind===kind&&v.id===id?{...v,id:next}:v)}};
  return result;
}
export function deleteContent(p:Project,kind:ContentKind,id:string,replacement=''):Project {
  const items=contentItems(p,kind);
  if(!items.some(item=>item.id===id))throw new Error('内容已不存在');
  const incoming=referencesTo(p,kind,id).filter(r=>r.kind!==kind||r.id!==id);
  if(incoming.length&&(!replacement||replacement===id||!items.some(item=>item.id===replacement)))throw new Error('请先选择替代引用，或修改仍在使用此内容的项');
  let result=replacement?rewriteReferences(p,kind,id,replacement):p;
  result=kind==='skill'?{...result,skills:result.skills.filter(s=>s.id!==id)}:
    kind==='buff'?{...result,buffs:result.buffs?.filter(b=>b.id!==id)}:
    {...result,summonBlueprints:result.summonBlueprints?.filter(b=>b.id!==id)};
  if(p.editor)result={...result,editor:{...p.editor,
    selectedSkill:kind==='skill'&&p.editor.selectedSkill===id?(result.skills[0]?.id??''):p.editor.selectedSkill,
    selectedBuff:kind==='buff'&&p.editor.selectedBuff===id?'':p.editor.selectedBuff,
    viewports:p.editor.viewports?.filter(v=>v.kind!==kind||v.id!==id)}};
  return result;
}
export function numberAllocator(values:number[],floor=0) {
  let value=Math.max(floor,...values);
  return ()=>{if(value>=2147483647)throw new Error('运行时数字身份已耗尽');return ++value;};
}
export const definitionAllocator=(p:Project)=>numberAllocator(allGraphs(p).flatMap(({graph})=>graph.nodes.map(n=>n.runtimeId??0)),4000);
export function copyGraph(nodes:GraphNode[],edges:GraphEdge[],definitionId:()=>number,newId:()=>string=()=>crypto.randomUUID()) {
  const ids=new Map(nodes.map(n=>[n.id,newId()]));
  return {nodes:nodes.map(n=>({...clone(n),id:ids.get(n.id)!,runtimeId:n.kind==='projectile'||n.kind==='delivery'?definitionId():n.runtimeId})),
    edges:edges.filter(e=>ids.has(e.source)&&ids.has(e.target)).map(e=>({...e,id:newId(),source:ids.get(e.source)!,target:ids.get(e.target)!}))};
}
export function duplicateContent(p:Project,kind:ContentKind,id:string,name?:string,newId:()=>string=()=>crypto.randomUUID()) {
  const source=contentItems(p,kind).find(s=>s.id===id);if(!source)throw new Error('内容已不存在');
  const nextId=newId();const title=name??source.name+' 副本';
  if(kind==='blueprint')return {id:nextId,project:{...p,summonBlueprints:[...(p.summonBlueprints??[]),{...clone(p.summonBlueprints!.find(b=>b.id===id)!),id:nextId,name:title}]}};
  const graph=graphOf(p,{kind,id})!;const copied=copyGraph(graph.nodes,graph.edges,definitionAllocator(p),newId);
  if(kind==='skill'){
    const number=numberAllocator(p.skills.flatMap(s=>[s.runtimeId,s.timeline?.runtimeId??0]))();
    const source=p.skills.find(s=>s.id===id)!;const nodeIds=new Map(source.nodes.map((n,i)=>[n.id,copied.nodes[i].id]));
    const value={...clone(source),...copied,id:nextId,name:title,runtimeId:number,timeline:source.timeline?remapTimeline(source.timeline,nodeIds,number):undefined};
    return {id:nextId,project:{...p,skills:[...p.skills,value]}};
  }
  const value:BuffDefinition={...clone(p.buffs!.find(b=>b.id===id)!),...copied,id:nextId,name:title,runtimeId:numberAllocator((p.buffs??[]).map(b=>b.runtimeId),8000)()};
  return {id:nextId,project:{...p,buffs:[...(p.buffs??[]),value]}};
}
export function remapTimeline(source:AuthoredTimeline,ids:Map<string,string>,runtimeId:number):AuthoredTimeline {
  const map=(values:string[]|undefined)=>values?.map(id=>{const next=ids.get(id);if(!next)throw new Error('时间轴效果入口不存在：'+id);return next;})??[];
  return {...clone(source),runtimeId,clips:source.clips.map(c=>({...clone(c),effects:map(c.effects)})),payloads:source.payloads.map(p=>({...clone(p),effects:map(p.effects)}))};
}
export function selectGraph(p:Project,skillId:string,buffId=''):Project {
  return {...p,editor:{...p.editor,selectedSkill:p.skills.some(s=>s.id===skillId)?skillId:(p.skills[0]?.id??''),selectedBuff:p.buffs?.some(b=>b.id===buffId)?buffId:''}};
}
export function saveViewport(p:Project,key:GraphKey,viewport:{x:number;y:number;zoom:number}):Project {
  const value:GraphViewport={...key,...viewport};
  return {...p,editor:{...p.editor,viewports:[...(p.editor?.viewports??[]).filter(v=>v.kind!==key.kind||v.id!==key.id),value]}};
}
export function emptyProject(name='新工程'):Project {
  return {formatVersion:1,id:crypto.randomUUID(),name,skills:[],buffs:[],summonBlueprints:[],preview:{seed:1,energy:100,recovery:10,
    caster:{id:'caster',x:-6,z:0,hp:1000,atk:80,def:0},targets:[0,3,6].map((x,i)=>({id:'target-'+(i+1),x,z:0,hp:1000,atk:0,def:0}))}};
}
