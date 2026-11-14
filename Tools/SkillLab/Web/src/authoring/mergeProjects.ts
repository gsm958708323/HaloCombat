import { clone } from '../model';
import type { GraphNode, Project } from '../model';
import { allGraphs, copyGraph, definitionAllocator, numberAllocator } from './contentEdits';

// Source references always stay within the imported set, including when a shared ID conflicts.
export function mergeProjects(destination:Project,source:Project,newId:()=>string=()=>crypto.randomUUID()):Project {
  function identityMap(imported:{id:string}[],existing:{id:string}[]) {
    const used=new Set(existing.map(item=>item.id));const result=new Map<string,string>();
    for(const item of imported){let id=item.id;while(used.has(id))id=newId();used.add(id);result.set(item.id,id);}return result;
  }
  const skillIds=identityMap(source.skills,destination.skills),buffIds=identityMap(source.buffs??[],destination.buffs??[]),blueprintIds=identityMap(source.summonBlueprints??[],destination.summonBlueprints??[]);
  const skillNumber=numberAllocator(destination.skills.map(s=>s.runtimeId)),buffNumber=numberAllocator((destination.buffs??[]).map(b=>b.runtimeId),8000);
  const definitionNumber=definitionAllocator(destination);
  const references=(nodes:GraphNode[])=>nodes.map(node=>{
    if((node.kind==='buff'||node.kind==='dispel')&&typeof node.parameters.buff==='string'&&buffIds.has(node.parameters.buff))return {...node,parameters:{...node.parameters,buff:buffIds.get(node.parameters.buff)!}};
    if(node.kind==='delivery'&&typeof node.parameters.blueprint==='string'&&blueprintIds.has(node.parameters.blueprint))return {...node,parameters:{...node.parameters,blueprint:blueprintIds.get(node.parameters.blueprint)!}};
    return node;
  });
  const skills=source.skills.map(s=>({...clone(s),...copyGraph(references(s.nodes),s.edges,definitionNumber,newId),id:skillIds.get(s.id)!,runtimeId:skillNumber()}));
  const buffs=(source.buffs??[]).map(b=>({...clone(b),...copyGraph(references(b.nodes),b.edges,definitionNumber,newId),id:buffIds.get(b.id)!,runtimeId:buffNumber()}));
  const blueprints=(source.summonBlueprints??[]).map(b=>({...clone(b),id:blueprintIds.get(b.id)!,skillId:skillIds.get(b.skillId)!}));
  const viewports=source.editor?.viewports?.map(v=>({...v,id:(v.kind==='skill'?skillIds:buffIds).get(v.id)!}))??[];
  const result={...destination,skills:[...destination.skills,...skills],buffs:[...(destination.buffs??[]),...buffs],summonBlueprints:[...(destination.summonBlueprints??[]),...blueprints],editor:{...destination.editor,viewports:[...(destination.editor?.viewports??[]),...viewports]}};
  if(allGraphs(result).some(({graph})=>graph.nodes.length>256))throw new Error('图节点数超过上限');
  return result;
}
