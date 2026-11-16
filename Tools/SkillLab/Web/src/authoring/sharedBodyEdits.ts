import { clone } from '../model';
import type { Parameters,Project } from '../model';
import { graphOf,selectGraph,updateGraph } from './contentEdits';
import type { GraphKey } from './contentEdits';

// A shared body owns its outgoing lifecycle graph; placement/emission stays at its caller.
export function extractBody(project:Project,key:GraphKey,nodeId:string,kind:'projectile'|'area',newId:()=>string) {
  const graph=graphOf(project,key),root=graph?.nodes.find(n=>n.id===nodeId);
  const matches=kind==='projectile'?root?.kind==='projectile':root?.kind==='delivery'&&root.parameters.mode==='area'&&root.parameters.persistent===true;
  if(!graph||!root||!matches||graph.root===nodeId)throw new Error('请选择可提取的'+(kind==='area'?'持续区域':'普通弹体')+'节点');
  const ids=new Set([nodeId]),pending=[nodeId];
  while(pending.length){const id=pending.pop()!;for(const edge of graph.edges)if(edge.source===id&&!ids.has(edge.target)){ids.add(edge.target);pending.push(edge.target);}}
  if(graph.edges.some(e=>ids.has(e.target)&&e.target!==nodeId&&!ids.has(e.source)))throw new Error('效果分支还被其他节点使用，请先拆开引用');
  if(graph.timeline&&[...graph.timeline.clips,...graph.timeline.payloads].some(c=>c.effects?.some(id=>id!==nodeId&&ids.has(id))))throw new Error('分支仍被时间轴直接调度，请先拆开入口');
  const id=newId(),omitted=kind==='area'?['center','forwardOffset']:['multi','count','spread'];
  const definition={id,name:graph.name+(kind==='area'?' 区域':' 弹体'),root:nodeId,
    nodes:clone(graph.nodes.filter(n=>ids.has(n.id))).map(n=>n.id!==nodeId?n:{...n,parameters:Object.fromEntries(Object.entries(n.parameters).filter(([k])=>!omitted.includes(k)))}),
    edges:clone(graph.edges.filter(e=>ids.has(e.source)&&ids.has(e.target)))};
  const parameters:Parameters=kind==='area'?{area:id,center:root.parameters.center??'point',forwardOffset:root.parameters.forwardOffset??0}:
    {projectile:id,count:root.parameters.multi===true?Number(root.parameters.count??3):1,spread:root.parameters.multi===true?Number(root.parameters.spread??45):0};
  const withDefinition=kind==='area'?{...project,areas:[...(project.areas??[]),definition]}:{...project,projectiles:[...(project.projectiles??[]),definition]};
  const replaced=updateGraph(withDefinition,key,g=>({...g,
    nodes:g.nodes.filter(n=>n.id===nodeId||!ids.has(n.id)).map(n=>n.id!==nodeId?n:{...n,kind:kind+'-ref',runtimeId:0,parameters}),
    edges:g.edges.filter(e=>!ids.has(e.source)&&(!ids.has(e.target)||e.target===nodeId))}));
  return {id,project:selectGraph(replaced,project.editor?.selectedSkill??project.skills[0]?.id??'','','',kind==='projectile'?id:'',kind==='area'?id:'')};
}
