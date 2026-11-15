import type {GraphNode} from '../model';

const timing=new Set(['delay','duration','count','interval','costPerPulse','energyPerSecond','sustained','channelDuration','chargeMin','chargeMax','powerMin','powerMax','autoRelease']);
export function setCastExecution(node:GraphNode,value:string):GraphNode {
  if(value!=='timeline'&&value!=='operation')throw new Error('未知技能执行方式');
  return {...node,parameters:value==='operation'
    ?{...Object.fromEntries(Object.entries(node.parameters).filter(([key])=>!timing.has(key))),mode:'instant',execution:value}
    :{...node.parameters,execution:value}};
}
