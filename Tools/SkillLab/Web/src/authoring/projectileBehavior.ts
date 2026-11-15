import type {GraphNode} from '../model';
import type {EditableGraph} from './contentEdits';

export const inlineBehaviorFields=['maxHits','chain','chainCount','chainRadius','chainMode'];
export function withoutInlineBehavior(node:GraphNode):GraphNode {
  return {...node,parameters:Object.fromEntries(Object.entries(node.parameters).filter(([field])=>!inlineBehaviorFields.includes(field)))};
}
export function extractProjectileBehavior(graph:EditableGraph,id:string,newId:()=>string=()=>crypto.randomUUID()):EditableGraph {
  const node=graph.nodes.find(n=>n.id===id);if(node?.kind!=='projectile')throw new Error('请选择普通弹体定义');
  if(graph.edges.some(e=>e.target===id&&e.targetPort==='behavior'))throw new Error('弹体已有行为连接');
  const behaviorId=newId(),p=node.parameters;
  const behavior:GraphNode={id:behaviorId,kind:'projectile-behavior',position:{x:node.position.x-290,y:node.position.y+240},parameters:p.chain===true
    ?{mode:'chain',chainCount:p.chainCount??2,chainRadius:p.chainRadius??6,chainMode:p.chainMode??'nearest'}
    :{mode:'pierce',maxHits:Number(p.maxHits??1)>0?Number(p.maxHits??1):2,unlimited:p.maxHits===0}};
  return {...graph,nodes:[...graph.nodes.map(n=>n.id===id?withoutInlineBehavior(n):n),behavior],edges:[...graph.edges,{id:newId(),source:behaviorId,sourcePort:'behavior',target:id,targetPort:'behavior',order:0}]};
}
