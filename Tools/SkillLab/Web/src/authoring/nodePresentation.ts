import {castRoutingFields,nodeFields,nodeOutputs} from '../model';
import type {Field,GraphNode} from '../model';

const extra:Record<string,Set<string>>={
  cast:new Set(['execution','hitFeedback','duration','cancelStart','cancelEnd',...castRoutingFields]),
  projectile:new Set(['view','passInvulnerable','trackingKey','spawnForward','lifetime','removeOnObstacle','wallBounces','homingRetarget','homingRadius']),
  damage:new Set(['feedbackHitstop','canCrit','critFromAttribute','snapshotAttack','ignoreDefense','scaleByStacks','notifyHurt','hitstopFrames','sourceHitstopFrames','targetHitstopFrames','feedback','maxTargets']),
  delivery:new Set(['view','maxTargets','hostileMask','cueId','pulseOnSpawn','forwardOffset']),
};
export function nodeFieldGroups(node:GraphNode,options:{definitionRoot?:boolean;authoredTimeline?:boolean}={}) {
  const fields=nodeFields(node,options),main:Field[]=[],advanced:Field[]=[];
  for(const field of fields){
    const isExtra=extra[node.kind]?.has(field.key)||node.kind==='projectile'&&field.key==='homingRate'&&!(Number(node.parameters.homingRate)>0);
    if(isExtra&&!(node.kind==='cast'&&node.parameters.execution==='operation'&&field.key==='execution'))advanced.push(field);else main.push(field);
  }
  return {main,advanced};
}
export function nodePortGroups(node:GraphNode,counts:Record<string,number>={}) {
  const ports=nodeOutputs(node);
  if(node.kind!=='projectile')return {main:ports,extra:[] as [string,string][]};
  const visible=new Set(['hit','wall','range','expire']);if(node.parameters.flightTrigger&&node.parameters.flightTrigger!=='none')visible.add('flight');
  if(node.parameters.trajectory==='return')visible.add('returned');
  if(['bounce','phase-bounce'].includes(String(node.parameters.trajectory))){visible.add('bounce');visible.add('ground');}
  if(Number(node.parameters.wallBounces)>0)visible.add('bounce');
  // A connected endpoint always stays visible; folding never hides a live wire.
  return {main:ports.filter(([key])=>visible.has(key)||counts[key]>0),extra:ports.filter(([key])=>!visible.has(key)&&!counts[key])};
}
export function targetTimingDescription(node:GraphNode):string {
  return node.parameters.mode==='direction'?'每次实际触发独立抽取方向':node.parameters.mode==='random'?'每次实际触发从当前合法目标中重新选择':'';
}
