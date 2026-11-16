import type {Project,RuntimeCombo} from '../model';
import {validateRule} from './comboRules';
import {maintainComboViews} from './comboViews';

export const comboStart='$start';
export function withComboRuntime(project:Project):Project{
  if(project.runtime)return project;
  const cues=new Map<number,{id:number;key:string;kind:string;lifetime:number}>();
  const graphs=[...project.skills,...(project.buffs??[]),...(project.timelines??[]),...(project.projectiles??[]),...(project.areas??[])];
  for(const graph of graphs)for(const node of graph.nodes){
    const id=Number(node.kind==='cue'?node.parameters.id:node.parameters.cueId);
    if(Number.isInteger(id)&&id>0)cues.set(id,{id,key:'cue-'+id,kind:'cube',lifetime:.45});
  }
  return {...project,runtime:{motor:{gravity:-20,jumpSpeed:6,airSteer:.35,groundY:0,stickDeadzone:.25},actors:[],combos:[],cues:[...cues.values()]}};
}
export function connectCombo(project:Project,source:string,target:string,id=crypto.randomUUID(),input='Attack'):Project{
  if(source===target&&source===comboStart||target===comboStart||!project.skills.some(s=>s.id===target)||source!==comboStart&&!project.skills.some(s=>s.id===source))throw new Error('连招端点必须引用现有技能，起手不能作为接续目标');
  const p=withComboRuntime(project),rows=p.runtime!.combos,preSkills=source===comboStart?[]:[source];
  if(rows.some(c=>c.input===input&&c.requiredTags.length===0&&c.toSkill===target&&c.preSkills.length===preSkills.length&&c.preSkills.every(s=>preSkills.includes(s))))throw new Error('此接续已存在，可编辑它的输入或条件，或添加条件分支');
  const edge:RuntimeCombo={id,input,preSkills,toSkill:target,priority:0,requiredTags:[],purpose:source===comboStart?'entry':'transition'};
  return applyComboRule(p,edge);
}
export function applyComboRule(project:Project,row:RuntimeCombo,viewId?:string):Project{
  const p=withComboRuntime(project);validateRule(p,row);
  const exists=p.runtime!.combos.some(r=>r.id===row.id);
  const next={...p,runtime:{...p.runtime!,combos:exists?p.runtime!.combos.map(r=>r.id===row.id?row:r):[...p.runtime!.combos,row]}};
  if(viewId&&row.purpose!=='cancel'&&row.preSkills.length)next.editor={...next.editor,comboViews:next.editor?.comboViews?.map(v=>v.id===viewId?{...v,ruleIds:[...new Set([...v.ruleIds,row.id])]}:v)};
  return maintainComboViews(next);
}
export function patchCombo(project:Project,id:string,patch:Partial<RuntimeCombo>):Project{
  if(!project.runtime)throw new Error('连招配置不存在');
  const row=project.runtime.combos.find(c=>c.id===id);if(!row)throw new Error('规则已不存在');
  return applyComboRule(project,{...row,...patch});
}
export function removeCombo(project:Project,id:string):Project{
  return project.runtime?maintainComboViews({...project,runtime:{...project.runtime,combos:project.runtime.combos.filter(c=>c.id!==id)}}):project;
}
export function comboSkills(project:Project):string[]{
  return [...new Set([...(project.editor?.comboPositions?.map(v=>v.skillId)??[]),...(project.runtime?.combos.flatMap(c=>[...c.preSkills,c.toSkill])??[])])].filter(id=>id!==comboStart&&project.skills.some(s=>s.id===id));
}
export function moveComboNode(project:Project,skillId:string,position:{x:number;y:number},viewId?:string):Project{
  if(viewId&&project.editor?.comboViews?.some(v=>v.id===viewId))return {...project,editor:{...project.editor,comboViews:project.editor.comboViews.map(v=>v.id===viewId?{...v,positions:[...v.positions.filter(p=>p.skillId!==skillId),{skillId,...position}]}:v)}};
  return {...project,editor:{...project.editor,comboPositions:[...(project.editor?.comboPositions??[]).filter(v=>v.skillId!==skillId),{skillId,...position}]}};
}
export function removeComboSkill(project:Project,skillId:string,input?:string):Project{
  if(skillId===comboStart)return project;
  const combos=project.runtime?.combos.filter(c=>input!==undefined&&c.input!==input||c.toSkill!==skillId&&!c.preSkills.includes(skillId));
  const retained=combos?.some(c=>c.toSkill===skillId||c.preSkills.includes(skillId));
  return maintainComboViews({...project,editor:{...project.editor,comboPositions:project.editor?.comboPositions?.filter(v=>v.skillId!==skillId)},
    ...(retained?{editor:project.editor}:{}),runtime:project.runtime?{...project.runtime,combos:combos!}:undefined});
}
