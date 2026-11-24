import type {GraphNode,Project,Skill} from '../model';

const timing=new Set(['cancelStart','cancelEnd','delay','duration','burst','count','interval','costPerPulse','energyPerSecond','sustained','channelDuration','chargeMin','chargeMax','powerMin','powerMax','autoRelease']);
export function setCastExecution(node:GraphNode,value:string):GraphNode {
  if(value!=='timeline'&&value!=='operation')throw new Error('未知技能执行方式');
  return {...node,parameters:value==='operation'
    ?{...Object.fromEntries(Object.entries(node.parameters).filter(([key])=>!timing.has(key))),mode:'instant',execution:value}
    :{...node.parameters,execution:value}};
}
export function setBurst(node:GraphNode,enabled:boolean):GraphNode {
  const count=enabled&&Number(node.parameters.count??1)<=1?3:node.parameters.count;
  return {...node,parameters:{...node.parameters,burst:enabled,...(count===undefined?{}:{count})}};
}
export function defaultCastMovement(project:Project,graph:Pick<Skill,'nodes'|'timeline'|'timelineRef'>):boolean {
  const timeline=graph.timeline??project.timelines?.find(t=>t.id===graph.timelineRef)?.timeline;
  return timeline?!timeline.clips.some(c=>['hitbox','melee','dash'].includes(c.kind)):
    !graph.nodes.some(n=>n.enabled!==false&&n.kind==='delivery'&&['melee','displacement'].includes(String(n.parameters.mode)));
}
