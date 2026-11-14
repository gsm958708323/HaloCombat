import { clone } from '../model';
import type { Project,SharedProjectile } from '../model';
import { definitionAllocator,graphOf,selectGraph,updateGraph } from './contentEdits';
import type { GraphKey } from './contentEdits';

export function extractProjectile(project:Project,key:GraphKey,nodeId:string,newId:()=>string=()=>crypto.randomUUID()) {
  const graph=graphOf(project,key),root=graph?.nodes.find(n=>n.id===nodeId);
  if(!graph||root?.kind!=='projectile'||graph.root===nodeId)throw new Error('请选择可提取的普通弹体节点');
  const ids=new Set([nodeId]),pending=[nodeId];
  while(pending.length){const id=pending.pop()!;for(const edge of graph.edges)if(edge.source===id&&!ids.has(edge.target)){ids.add(edge.target);pending.push(edge.target);}}
  if(graph.edges.some(e=>ids.has(e.target)&&e.target!==nodeId&&!ids.has(e.source)))throw new Error('命中分支还被图中其他节点使用，请先拆开引用');
  if(graph.timeline&&[...graph.timeline.clips,...graph.timeline.payloads].some(c=>c.effects?.some(id=>id!==nodeId&&ids.has(id))))throw new Error('分支仍被时间轴直接调度，请先拆开入口');
  const id=newId(),count=root.parameters.multi===true?Number(root.parameters.count??3):1,spread=root.parameters.multi===true?Number(root.parameters.spread??45):0;
  const shared:SharedProjectile={id,name:graph.name+' 弹体',root:nodeId,nodes:clone(graph.nodes.filter(n=>ids.has(n.id))).map(n=>n.id!==nodeId?n:{...n,parameters:Object.fromEntries(Object.entries(n.parameters).filter(([k])=>!['multi','count','spread'].includes(k)))}),edges:clone(graph.edges.filter(e=>ids.has(e.source)&&ids.has(e.target)))};
  const replaced=updateGraph({...project,projectiles:[...(project.projectiles??[]),shared]},key,g=>({...g,
    nodes:g.nodes.filter(n=>n.id===nodeId||!ids.has(n.id)).map(n=>n.id!==nodeId?n:{...n,kind:'projectile-ref',runtimeId:0,parameters:{projectile:id,count,spread}}),
    edges:g.edges.filter(e=>!ids.has(e.source)&&(!ids.has(e.target)||e.target===nodeId))}));
  return {id,project:selectGraph(replaced,project.editor?.selectedSkill??project.skills[0]?.id??'','','',id)};
}
export function newProjectile(project:Project,skillId:string,newId:()=>string=()=>crypto.randomUUID()) {
  const id=newId(),root=newId();const value:SharedProjectile={id,name:'新共享弹体',root,nodes:[{id:root,kind:'projectile',runtimeId:definitionAllocator(project)(),position:{x:0,y:80},parameters:{speed:12,range:24,radius:.18,maxHits:1}}],edges:[]};
  return {id,project:selectGraph({...project,projectiles:[...(project.projectiles??[]),value]},skillId,'','',id)};
}
