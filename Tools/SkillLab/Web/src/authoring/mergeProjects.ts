import { clone } from '../model';
import type { GraphNode, Project } from '../model';
import { actionNumbers,allGraphs, copyGraph, definitionAllocator, numberAllocator,remapTimeline } from './contentEdits';

// Source references always stay within the imported set, including when a shared ID conflicts.
export function mergeProjects(destination:Project,source:Project,newId:()=>string=()=>crypto.randomUUID()):Project {
  return mergeWithIdentities(destination,source,newId).project;
}

export function mergeWithIdentities(destination:Project,source:Project,newId:()=>string=()=>crypto.randomUUID()) {
  function identityMap(imported:{id:string}[],existing:{id:string}[]) {
    const used=new Set(existing.map(item=>item.id));const result=new Map<string,string>();
    for(const item of imported){let id=item.id;while(used.has(id))id=newId();used.add(id);result.set(item.id,id);}return result;
  }
  const skillIds=identityMap(source.skills,destination.skills),buffIds=identityMap(source.buffs??[],destination.buffs??[]),blueprintIds=identityMap(source.summonBlueprints??[],destination.summonBlueprints??[]);
  const timelineIds=identityMap(source.timelines??[],destination.timelines??[]);
  const projectileIds=identityMap(source.projectiles??[],destination.projectiles??[]),areaIds=identityMap(source.areas??[],destination.areas??[]);
  const viewIds=identityMap(source.runtime?.views??[],destination.runtime?.views??[]);
  const comboIds=identityMap(source.runtime?.combos??[],destination.runtime?.combos??[]);
  const comboViewIds=identityMap(source.editor?.comboViews??[],destination.editor?.comboViews??[]);
  const skillNumber=numberAllocator(actionNumbers(destination)),buffNumber=numberAllocator((destination.buffs??[]).map(b=>b.runtimeId),8000);
  const definitionNumber=definitionAllocator(destination);
  const references=(nodes:GraphNode[])=>nodes.map(original=>{
    const builtIn=original.kind==='construct'&&['barrel','crate'].includes(String(original.parameters.view));
    const node=!builtIn&&typeof original.parameters.view==='string'&&viewIds.has(original.parameters.view)?{...original,parameters:{...original.parameters,view:viewIds.get(original.parameters.view)!}}:original;
    if(node.kind==='area-ref'&&typeof node.parameters.area==='string')return {...node,parameters:{...node.parameters,area:areaIds.get(node.parameters.area)??node.parameters.area}};
    if(node.kind==='projectile-ref'&&typeof node.parameters.projectile==='string')return {...node,parameters:{...node.parameters,projectile:projectileIds.get(node.parameters.projectile)??node.parameters.projectile}};
    if(node.kind==='cast')return {...node,parameters:{...node.parameters,...Object.fromEntries(['fallbackSkill','recastSkill'].filter(field=>typeof node.parameters[field]==='string'&&skillIds.has(String(node.parameters[field]))).map(field=>[field,skillIds.get(String(node.parameters[field]))!]))}};
    if((node.kind==='buff'||node.kind==='dispel')&&typeof node.parameters.buff==='string'&&buffIds.has(node.parameters.buff))return {...node,parameters:{...node.parameters,buff:buffIds.get(node.parameters.buff)!}};
    if(node.kind==='delivery'&&typeof node.parameters.blueprint==='string'&&blueprintIds.has(node.parameters.blueprint))return {...node,parameters:{...node.parameters,blueprint:blueprintIds.get(node.parameters.blueprint)!}};
    return node;
  });
  const skills=source.skills.map(s=>{const graph=copyGraph(references(s.nodes),s.edges,definitionNumber,newId),runtimeId=skillNumber();
    return {...clone(s),...graph,id:skillIds.get(s.id)!,runtimeId,timelineRef:s.timelineRef?timelineIds.get(s.timelineRef):undefined,timeline:s.timeline?remapTimeline(s.timeline,new Map(s.nodes.map((n,i)=>[n.id,graph.nodes[i].id])),runtimeId):undefined};});
  const timelines=(source.timelines??[]).map(t=>{const graph=copyGraph(references(t.nodes),t.edges,definitionNumber,newId);return {...clone(t),...graph,id:timelineIds.get(t.id)!,timeline:remapTimeline(t.timeline,new Map(t.nodes.map((n,i)=>[n.id,graph.nodes[i].id])),skillNumber())};});
  const projectiles=(source.projectiles??[]).map(t=>{const graph=copyGraph(references(t.nodes),t.edges,definitionNumber,newId);return {...clone(t),...graph,id:projectileIds.get(t.id)!,root:graph.nodes[t.nodes.findIndex(n=>n.id===t.root)].id};});
  const areas=(source.areas??[]).map(t=>{const graph=copyGraph(references(t.nodes),t.edges,definitionNumber,newId);return {...clone(t),...graph,id:areaIds.get(t.id)!,root:graph.nodes[t.nodes.findIndex(n=>n.id===t.root)].id};});
  const buffs=(source.buffs??[]).map(b=>({...clone(b),...copyGraph(references(b.nodes),b.edges,definitionNumber,newId),id:buffIds.get(b.id)!,runtimeId:buffNumber()}));
  const blueprints=(source.summonBlueprints??[]).map(b=>({...clone(b),id:blueprintIds.get(b.id)!,skillId:skillIds.get(b.skillId)!}));
  const viewports=source.editor?.viewports?.map(v=>({...v,id:(v.kind==='skill'?skillIds:v.kind==='timeline'?timelineIds:v.kind==='projectile'?projectileIds:v.kind==='area'?areaIds:buffIds).get(v.id)!}))??[];
  const runtime=source.runtime?mergeRuntime(destination,source,skillIds,newId,viewIds,comboIds):destination.runtime;
  const comboPositions=(source.editor?.comboPositions??[]).filter(v=>v.skillId!=='$start').map(v=>({...v,skillId:skillIds.get(v.skillId)!}));
  const comboViews=(source.editor?.comboViews??[]).map(v=>({...clone(v),id:comboViewIds.get(v.id)!,rootSkill:skillIds.get(v.rootSkill)!,ruleIds:v.ruleIds.map(id=>comboIds.get(id)!),positions:v.positions.map(p=>({...p,skillId:p.skillId==='$start'?p.skillId:skillIds.get(p.skillId)!}))}));
  const result={...destination,runtime,skills:[...destination.skills,...skills],timelines:[...(destination.timelines??[]),...timelines],projectiles:[...(destination.projectiles??[]),...projectiles],areas:[...(destination.areas??[]),...areas],buffs:[...(destination.buffs??[]),...buffs],summonBlueprints:[...(destination.summonBlueprints??[]),...blueprints],editor:{...destination.editor,viewports:[...(destination.editor?.viewports??[]),...viewports],comboPositions:[...(destination.editor?.comboPositions??[]),...comboPositions],comboViews:[...(destination.editor?.comboViews??[]),...comboViews]}};
  if(allGraphs(result).some(({graph})=>graph.nodes.length>256))throw new Error('图节点数超过上限');
  return {project:result,identities:{skills:skillIds,buffs:buffIds,summonBlueprints:blueprintIds,timelines:timelineIds,projectiles:projectileIds,areas:areaIds,views:viewIds}};
}
function mergeRuntime(destination:Project,source:Project,skills:Map<string,string>,newId:()=>string,viewIds:Map<string,string>,comboIds:Map<string,string>) {
  const incoming=source.runtime!;const current=destination.runtime;
  if(current&&JSON.stringify(current.motor)!==JSON.stringify(incoming.motor))throw new Error('两个工程的 Motor 设置不同，请分别发布或先统一设置后合并');
  const ids=(existing:{id:string}[])=>{const used=new Set(existing.map(v=>v.id));return (id:string)=>{while(used.has(id))id=newId();used.add(id);return id;};};
  const actorId=ids(current?.actors??[]);
  const actorIds=new Map(incoming.actors.map(a=>[a.id,actorId(a.id)]));
  const views=[...(current?.views??[]),...(incoming.views??[]).map(v=>({...clone(v),id:viewIds.get(v.id)!}))];
  const inputId=ids((current?.combos??[]).map(c=>({id:c.input}))),inputIds=new Map([...new Set(incoming.combos.map(c=>c.input))].map(input=>[input,inputId(input)]));
  const arena=current?.arena??(incoming.arena?{...clone(incoming.arena),player:actorIds.get(incoming.arena.player)!,enemy:actorIds.get(incoming.arena.enemy)!,trainingTarget:actorIds.get(incoming.arena.trainingTarget)!,bindings:incoming.arena.bindings.map(b=>({...b,input:inputIds.get(b.input)!}))}:undefined);
  const cues=[...(current?.cues??[])];
  for(const cue of incoming.cues){const old=cues.find(c=>c.id===cue.id);if(old&&JSON.stringify(old)!==JSON.stringify(cue))throw new Error('Cue数字身份冲突：'+cue.id+'，请先修改冲突定义');if(!old)cues.push(clone(cue));}
  return {...clone(incoming),arena,cues,views:views.length?views:undefined,actors:[...(current?.actors??[]),...incoming.actors.map(a=>({...clone(a),id:actorIds.get(a.id)!,view:viewIds.get(a.view)??a.view,skillId:a.skillId?skills.get(a.skillId):undefined,dodgeSkill:a.dodgeSkill?skills.get(a.dodgeSkill):undefined,slots:a.slots?.map(id=>skills.get(id)!)}))],
    combos:[...(current?.combos??[]),...incoming.combos.map(c=>({...clone(c),id:comboIds.get(c.id)!,input:inputIds.get(c.input)!,toSkill:skills.get(c.toSkill)!,preSkills:c.preSkills.map(id=>skills.get(id)!)}))]};
}
