import { clone, kinds, nodeOutputs } from '../model';
import type { GraphEdge, GraphNode, Project } from '../model';
import { copyGraph, definitionAllocator, graphOf, updateGraph } from './contentEdits';
import type { EditableGraph, GraphKey } from './contentEdits';
import {withoutInlineBehavior} from './projectileBehavior';

export interface GraphClipboard { nodes:GraphNode[];edges:GraphEdge[] }
export interface Link { source:string;sourceHandle?:string|null;target:string;targetHandle?:string|null }
export function copySelection(graph:EditableGraph,ids:string[]):GraphClipboard {
  const included=new Set(ids);return clone({nodes:graph.nodes.filter(n=>included.has(n.id)),edges:graph.edges.filter(e=>included.has(e.source)&&included.has(e.target))});
}
export function pasteSelection(p:Project,key:GraphKey,clipboard:GraphClipboard,point:{x:number;y:number},newId:()=>string=()=>crypto.randomUUID()) {
  const graph=graphOf(p,key);if(!graph)throw new Error('目标图已不存在');
  const roots=new Set(['target','cast','buff-events','skill-events']);
  const includedRoots=new Set(graph.nodes.filter(n=>roots.has(n.kind)).map(n=>n.kind));
  const nodes=clipboard.nodes.filter(n=>{
    if((key.kind==='timeline'||key.kind==='projectile'||key.kind==='area')&&roots.has(n.kind))return false;
    if(key.kind==='buff'&&['target','cast','skill-events'].includes(n.kind)||key.kind==='skill'&&n.kind==='buff-events')return false;
    if(!roots.has(n.kind))return true;if(includedRoots.has(n.kind))return false;includedRoots.add(n.kind);return true;
  });
  if(!nodes.length)throw new Error('选中的根节点无法粘贴到此图');
  if(graph.nodes.length+nodes.length>256)throw new Error('粘贴后节点数超过256个');
  for(const node of nodes){
    if(node.kind==='cast'&&['fallbackSkill','recastSkill'].some(field=>node.parameters[field]&&!p.skills.some(s=>s.id===node.parameters[field])))throw new Error('待粘贴施放节点引用了工程中不存在的技能');
    if((node.kind==='buff'||node.kind==='dispel')&&node.parameters.buff&&!p.buffs?.some(b=>b.id===node.parameters.buff))throw new Error('待粘贴节点引用了工程中不存在的 Buff');
    if(node.kind==='delivery'&&node.parameters.blueprint&&!p.summonBlueprints?.some(b=>b.id===node.parameters.blueprint))throw new Error('待粘贴节点引用了工程中不存在的召唤蓝图');
    if(node.kind==='area-ref'&&!p.areas?.some(v=>v.id===node.parameters.area))throw new Error('待粘贴节点引用了工程中不存在的区域定义');
    if(node.kind==='projectile-ref'&&!p.projectiles?.some(v=>v.id===node.parameters.projectile))throw new Error('待粘贴节点引用了工程中不存在的弹体定义');
  }
  const copied=copyGraph(nodes,clipboard.edges,definitionAllocator(p),newId);
  const origin={x:Math.min(...nodes.map(n=>n.position.x)),y:Math.min(...nodes.map(n=>n.position.y))};
  copied.nodes=copied.nodes.map(n=>({...n,position:{x:n.position.x-origin.x+point.x,y:n.position.y-origin.y+point.y}}));
  return {project:updateGraph(p,key,g=>({...g,nodes:[...g.nodes,...copied.nodes],edges:[...g.edges,...copied.edges]})),
    ids:copied.nodes.map(n=>n.id),skipped:clipboard.nodes.length-nodes.length};
}
export function deleteSelection(p:Project,key:GraphKey,nodeIds:string[],edgeIds:string[]):Project {
  return updateGraph(p,key,g=>{
    const removed=new Set(nodeIds.filter(id=>id!==g.root&&g.nodes.find(n=>n.id===id)?.kind!=='buff-events'));
    return {...g,nodes:g.nodes.filter(n=>!removed.has(n.id)),edges:g.edges.filter(e=>!removed.has(e.source)&&!removed.has(e.target)&&!edgeIds.includes(e.id)),timeline:g.timeline?{...g.timeline,
      clips:g.timeline.clips.map(c=>({...c,effects:c.effects?.filter(id=>!removed.has(id))})),payloads:g.timeline.payloads.map(p=>({...p,effects:p.effects.filter(id=>!removed.has(id))}))}:undefined};
  });
}
export function connectGraph(graph:EditableGraph,link:Link,edgeId:string=crypto.randomUUID(),replacing=false):EditableGraph {
  const source=graph.nodes.find(n=>n.id===link.source),target=graph.nodes.find(n=>n.id===link.target);
  const sourcePort=link.sourceHandle,targetPort=link.targetHandle;
  if(!source||!target||!sourcePort||!targetPort||!nodeOutputs(source).some(([p])=>p===sourcePort)||!kinds[target.kind]?.inputs.includes(targetPort))throw new Error('端口不存在或类型不匹配');
  if((sourcePort==='target')!==(targetPort==='target')||targetPort==='target'&&target.kind!=='cast')throw new Error('目标数据只能连接施放的目标端口');
  if((sourcePort==='behavior')!==(targetPort==='behavior')||sourcePort==='behavior'&&(source.kind!=='projectile-behavior'||target.kind!=='projectile'))throw new Error('行为数据只能连接投射物的行为端口');
  const previous=graph.edges.find(e=>e.id===edgeId);const edges=replacing?graph.edges.filter(e=>e.id!==edgeId):graph.edges;
  if(targetPort==='target'&&edges.some(e=>e.target===target.id&&e.targetPort==='target'))throw new Error('此施放已有目标数据，请先断开原连线');
  if(targetPort==='behavior'&&edges.some(e=>e.target===target.id&&e.targetPort==='behavior'))throw new Error('此弹体已有行为，请先断开原连线');
  const pending=[target.id];const visited=new Set<string>();
  while(pending.length){const id=pending.pop()!;if(id===source.id)throw new Error('连接会形成循环；请使用有限的行为参数');if(visited.has(id))continue;visited.add(id);for(const edge of edges)if(edge.source===id)pending.push(edge.target);}
  const order=previous&&previous.source===source.id&&previous.sourcePort===sourcePort?previous.order:
    Math.max(-1,...edges.filter(e=>e.source===source.id&&e.sourcePort===sourcePort).map(e=>e.order))+1;
  return {...graph,nodes:targetPort==='behavior'?graph.nodes.map(n=>n.id===target.id?withoutInlineBehavior(n):n):graph.nodes,edges:[...edges,{id:edgeId,source:source.id,sourcePort,target:target.id,targetPort,order}]};
}
export function reorderEdge(graph:EditableGraph,id:string,direction:-1|1):EditableGraph {
  const edge=graph.edges.find(e=>e.id===id);if(!edge)return graph;
  const siblings=graph.edges.filter(e=>e.source===edge.source&&e.sourcePort===edge.sourcePort).sort((a,b)=>a.order-b.order);
  const other=siblings[siblings.findIndex(e=>e.id===id)+direction];if(!other)return graph;
  return {...graph,edges:graph.edges.map(e=>e.id===edge.id?{...e,order:other.order}:e.id===other.id?{...e,order:edge.order}:e)};
}
