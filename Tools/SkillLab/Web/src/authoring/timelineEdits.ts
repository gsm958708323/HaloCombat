import { clone } from '../model';
import type { Project } from '../model';
import { actionNumbers,numberAllocator,selectGraph } from './contentEdits';

const roots=new Set(['target','cast']);
const rules=new Set(['mode','cost','cooldown','ammoCost','fallbackSkill','recastSkill','recastProjectile','requiredProjectile','hitFeedback']);
export function bindTimeline(project:Project,skillId:string,timelineId:string):Project {
  if(timelineId&&!project.timelines?.some(t=>t.id===timelineId))throw new Error('共享时间轴不存在');
  return {...project,skills:project.skills.map(s=>{
    if(s.id!==skillId)return s;
    const kept=new Set(s.nodes.filter(n=>roots.has(n.kind)||n.kind==='skill-events').map(n=>n.id));
    const pending=s.nodes.filter(n=>n.kind==='skill-events').map(n=>n.id);
    while(pending.length){const id=pending.pop()!;for(const e of s.edges)if(e.source===id&&!kept.has(e.target)){kept.add(e.target);pending.push(e.target);}}
    return {...s,timeline:undefined,timelineRef:timelineId||undefined,
      nodes:s.nodes.filter(n=>kept.has(n.id)).map(n=>n.kind!=='cast'?n:{...n,parameters:Object.fromEntries(Object.entries(n.parameters).filter(([key])=>rules.has(key)))}),
      edges:s.edges.filter(e=>kept.has(e.source)&&kept.has(e.target)&&!s.nodes.some(n=>n.id===e.source&&n.kind==='cast'))};
  })};
}
export function extractTimeline(project:Project,skillId:string,newId:()=>string=()=>crypto.randomUUID()) {
  const skill=project.skills.find(s=>s.id===skillId);
  if(!skill?.timeline||skill.timelineRef)throw new Error('当前技能没有可提取的内联时间轴');
  if(skill.nodes.some(n=>n.kind==='skill-events')||skill.edges.some(e=>e.sourcePort==='cast'))throw new Error('请先将技能事件与施放直连效果拆开，再提取动作');
  const id=newId();const nodes=skill.nodes.filter(n=>!roots.has(n.kind));const ids=new Set(nodes.map(n=>n.id));
  const shared={id,name:skill.name+' 动作',nodes:clone(nodes),edges:clone(skill.edges.filter(e=>ids.has(e.source)&&ids.has(e.target))),timeline:clone(skill.timeline)};
  const next=bindTimeline({...project,timelines:[...(project.timelines??[]),shared]},skillId,id);
  return {id,project:selectGraph(next,skillId,'',id)};
}
export function newTimeline(project:Project,skillId:string,newId:()=>string=()=>crypto.randomUUID()) {
  const id=newId();const timeline={id,name:'新共享时间轴',nodes:[],edges:[],timeline:{runtimeId:numberAllocator(actionNumbers(project))(),duration:.5,clips:[],payloads:[]}};
  return {id,project:selectGraph({...project,timelines:[...(project.timelines??[]),timeline]},skillId,'',id)};
}
