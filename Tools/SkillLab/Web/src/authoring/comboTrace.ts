import type {ComboDecision,PreviewState,RunEvent} from '../model';

const rejectionLabels:Record<string,string>={'Insufficient energy.':'能量不足','Insufficient ammunition.':'弹药不足','Cooldown.':'技能冷却中',
  'Cancel window is closed.':'接续窗口未开启','Casting is blocked by actor state.':'角色状态禁止施放','No valid target.':'没有有效目标',
  'Actor is in hitstop.':'角色正在顿帧','A held cast is already active.':'引导或蓄力尚未结束','Required projectile is unavailable.':'缺少所需追踪弹体'};
export function comboReason(reason:string|null){return reason?rejectionLabels[reason]??reason:'';}
export function tagLabel(tag:number){return ({1001:'接续窗口',1017:'本次命中确认'} as Record<number,string>)[tag]??'Tag '+tag;}
export function comboTrace(state:PreviewState|null){
  const caster=state?.actors.find(a=>a.kind==='caster');
  const events=(state?.comboDecisions??[]).filter(e=>e.comboDecision&&e.sourceId===caster?.id).slice(-64);
  const relevant=events.filter(e=>e.comboDecision?.inputSequence===caster?.comboInputSequence);
  const newest=relevant.at(-1)?.comboDecision??(caster?.comboInputSequence?null:caster?.comboDecision)??null;
  const group=newest?relevant.filter(e=>e.comboDecision?.inputSequence===newest.inputSequence):[];
  // Preserve a resource rejection for this press even if confirmation is consumed on lookup.
  const outcome=[...group].reverse().find(e=>['cast','rejected'].includes(e.comboDecision!.phase))?.comboDecision;
  const decision=outcome??newest;
  const successes=events.filter(e=>e.comboDecision?.phase==='cast').slice(-24);
  let root=-1;successes.forEach((e,index)=>{if(!e.comboDecision!.fromSkill)root=index;});
  const chain=successes.slice(root<0?0:root);
  const steps:{skill:string;event:number;castId:number}[]=[];
  if(chain.length){const first=chain[0].comboDecision!;if(first.fromSkill)steps.push({skill:first.fromSkill,event:0,castId:0});
    for(const event of chain){const d=event.comboDecision!;if(d.actualSkill)steps.push({skill:d.actualSkill,event:event.sequence,castId:d.castId});}}
  return {caster,decision,events,chain,steps,hasIdleStart:chain.length>0&&!chain[0].comboDecision!.fromSkill,
    executedEdges:new Set(chain.filter(e=>e.comboDecision!.actualSkill===e.comboDecision!.toSkill).map(e=>e.comboDecision!.rule+'::'+(e.comboDecision!.fromSkill??'$start'))),
    executedRules:new Set(chain.filter(e=>e.comboDecision!.actualSkill===e.comboDecision!.toSkill).map(e=>e.comboDecision!.rule!))};
}
export function decisionTitle(decision:ComboDecision|null){
  if(!decision)return '尚未收到连招输入';
  return ({blocked:'角色状态阻止接招',waiting:'输入等待接续窗口',matched:'已选择规则',unmatched:'没有匹配规则',cast:'技能已成功施放',rejected:'规则已选中，施放失败'} as const)[decision.phase];
}
export function successfulComboEvents(events:RunEvent[]){return events.filter(e=>e.comboDecision?.phase==='cast');}
