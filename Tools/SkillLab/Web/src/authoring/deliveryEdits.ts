import {kinds,nodeFields,nodeOutputs,parameterValue} from '../model';
import type {GraphNode,Project,Parameters} from '../model';
import {definitionAllocator,graphOf,updateGraph} from './contentEdits';
import type {EditableGraph,GraphKey} from './contentEdits';

export const deliveryModes:[string,string][]=[['target','目标作用'],['area','区域作用'],['projectile','投射物'],['melee','近战判定'],['displacement','位移攻击'],['ray','射线判定'],['summon','召唤分身']];
export const deliveryMode=(node:GraphNode)=>node.kind==='projectile'?'projectile':String(node.parameters.mode??'target');

function roots(graph:EditableGraph):string[] {
  return [...graph.nodes.filter(n=>['target','cast','skill-events','buff-events'].includes(n.kind)).map(n=>n.id),
    ...(graph.root?[graph.root]:[]),...(graph.timeline?[...graph.timeline.payloads,...graph.timeline.clips].flatMap(v=>v.effects??[]):[])];
}
function reachable(graph:EditableGraph):Set<string> {
  const visited=new Set<string>(),pending=roots(graph);
  while(pending.length){
    const id=pending.pop()!,node=graph.nodes.find(n=>n.id===id);if(!node||node.enabled===false||visited.has(id))continue;
    visited.add(id);for(const edge of graph.edges){if(edge.source===id)pending.push(edge.target);if(node.kind==='projectile'&&edge.target===id&&edge.targetPort==='behavior')pending.push(edge.source);}
  }
  return visited;
}
function unboundSplits(graph:EditableGraph):Set<string> {
  const invalid=new Set<string>(),visited=new Set<string>();
  function visit(id:string,hit:boolean){
    const node=graph.nodes.find(n=>n.id===id),key=id+':'+hit;if(!node||node.enabled===false||visited.has(key))return;visited.add(key);
    if(node.kind==='split'&&!hit)invalid.add(id);
    for(const edge of graph.edges.filter(e=>e.source===id&&e.targetPort==='execute')){
      let next=hit;if(node.kind==='projectile')next=edge.sourcePort==='hit';
      if(node.kind==='construct'||node.kind==='delivery'&&(!['target','area'].includes(String(node.parameters.mode??'target'))||node.parameters.persistent===true))next=false;
      visit(edge.target,next);
    }
  }
  for(const id of roots(graph))visit(id,false);return invalid;
}
function parametersFor(node:GraphNode,mode:string):Parameters {
  const kind=mode==='projectile'?'projectile':'delivery';
  const parameters:Parameters=Object.fromEntries(kinds[kind].fields.map(f=>[f.key,f.initial]));if(kind==='delivery')parameters.mode=mode;
  const previous=nodeFields(node),allowed=new Set(previous.map(f=>f.key));
  // A projectile collision radius and an effect area radius are different quantities.
  const unrelated=new Set(node.kind===kind?[]:['radius','count','view','removeOnObstacle']);
  for(const field of kinds[kind].fields)if(field.key!=='mode'&&allowed.has(field.key)&&!unrelated.has(field.key)){
    const old=previous.find(f=>f.key===field.key)!;parameters[field.key]=parameterValue(old,node.parameters);
  }
  const next={...node,kind,parameters};return Object.fromEntries(nodeFields(next).map(f=>[f.key,parameterValue(f,parameters)]));
}
function disableDetached(before:EditableGraph,after:EditableGraph) {
  const oldReach=reachable(before),newReach=reachable(after);let disabledNodes=0;
  const nodes=after.nodes.map(n=>{if(oldReach.has(n.id)&&!newReach.has(n.id)&&n.enabled!==false){disabledNodes++;return {...n,enabled:false};}return n;});
  return {graph:{...after,nodes},disabledNodes};
}
export function reconcileNodeOutputs(graph:EditableGraph,nodes:GraphNode[],id:string):EditableGraph {
  const node=nodes.find(n=>n.id===id);if(!node)return {...graph,nodes};
  const available=new Set(nodeOutputs(node).map(([port])=>port));
  const edges=graph.edges.filter(e=>e.source!==id||available.has(e.sourcePort));
  return disableDetached(graph,{...graph,nodes,edges}).graph;
}
export function switchDelivery(project:Project,key:GraphKey,id:string,mode:string) {
  const graph=graphOf(project,key),node=graph?.nodes.find(n=>n.id===id);
  if(!graph||!node||!['projectile','delivery'].includes(node.kind))throw new Error('请选择普通效果传递节点');
  if(graph.root===id)throw new Error('共享定义根类型固定，请在调用技能中更换传递方式');
  if(!deliveryModes.some(([value])=>value===mode))throw new Error('未知效果传递类型');
  if(deliveryMode(node)===mode)return {project,removedEdges:0,disabledNodes:0};
  const kind=mode==='projectile'?'projectile':'delivery';
  const changed={...node,kind,runtimeId:node.runtimeId&&node.runtimeId>0?node.runtimeId:definitionAllocator(project)(),parameters:parametersFor(node,mode)};
  if(mode==='summon'&&!changed.parameters.blueprint){const blueprint=project.summonBlueprints?.find(b=>key.kind!=='skill'||b.skillId!==key.id);if(blueprint)changed.parameters.blueprint=blueprint.id;}
  const primary=node.kind==='projectile'?'hit':'apply',destination=kind==='projectile'?'hit':'apply';
  const available=new Set(nodeOutputs(changed).map(([port])=>port));
  const edges=graph.edges.filter(e=>!(e.target===id&&e.targetPort==='behavior'&&kind!=='projectile'))
    .map(e=>e.source===id&&e.sourcePort===primary?{...e,sourcePort:destination}:e)
    .filter(e=>e.source!==id||available.has(e.sourcePort));
  const next={...graph,nodes:graph.nodes.map(n=>n.id===id?changed:n),edges};
  const oldInvalid=unboundSplits(graph),invalid=[...unboundSplits(next)].find(id=>!oldInvalid.has(id));
  if(invalid)throw new Error('节点 '+invalid+' 需要投射物命中上下文；请先断开其分支，再切换传递方式');
  const result=disableDetached(graph,next);
  return {project:updateGraph(project,key,()=>result.graph),removedEdges:graph.edges.length-edges.length,disabledNodes:result.disabledNodes};
}
