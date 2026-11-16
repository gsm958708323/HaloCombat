import type {Project,RuntimeCombo} from '../model';

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
export function connectCombo(project:Project,source:string,target:string,id=crypto.randomUUID()):Project{
  if(source===target&&source===comboStart||target===comboStart||!project.skills.some(s=>s.id===target)||source!==comboStart&&!project.skills.some(s=>s.id===source))throw new Error('连招端点必须引用现有技能，起手不能作为接续目标');
  const p=withComboRuntime(project),rows=p.runtime!.combos,preSkills=source===comboStart?[]:[source];
  if(rows.some(c=>c.input==='Attack'&&c.requiredTags.length===0&&c.toSkill===target&&c.preSkills.length===preSkills.length&&c.preSkills.every(s=>preSkills.includes(s))))throw new Error('此接续已存在，可编辑它的输入或条件，或添加条件分支');
  const priority=Math.max(-10,...rows.filter(c=>c.input==='Attack'&&(preSkills.length===0?c.preSkills.length===0:c.preSkills.some(s=>preSkills.includes(s)))).map(c=>c.priority))+10;
  const edge:RuntimeCombo={id,input:'Attack',preSkills,toSkill:target,priority,requiredTags:[]};
  return {...p,runtime:{...p.runtime!,combos:[...rows,edge]}};
}
export function patchCombo(project:Project,id:string,patch:Partial<RuntimeCombo>):Project{
  if(!project.runtime)throw new Error('连招配置不存在');
  return {...project,runtime:{...project.runtime,combos:project.runtime.combos.map(c=>c.id===id?{...c,...patch}:c)}};
}
export function removeCombo(project:Project,id:string):Project{
  return project.runtime?{...project,runtime:{...project.runtime,combos:project.runtime.combos.filter(c=>c.id!==id)}}:project;
}
export function comboSkills(project:Project):string[]{
  return [...new Set([...(project.editor?.comboPositions?.map(v=>v.skillId)??[]),...(project.runtime?.combos.flatMap(c=>[...c.preSkills,c.toSkill])??[])])].filter(id=>id!==comboStart&&project.skills.some(s=>s.id===id));
}
export function moveComboNode(project:Project,skillId:string,position:{x:number;y:number}):Project{
  return {...project,editor:{...project.editor,comboPositions:[...(project.editor?.comboPositions??[]).filter(v=>v.skillId!==skillId),{skillId,...position}]}};
}
export function removeComboSkill(project:Project,skillId:string):Project{
  if(skillId===comboStart)return project;
  return {...project,editor:{...project.editor,comboPositions:project.editor?.comboPositions?.filter(v=>v.skillId!==skillId)},
    runtime:project.runtime?{...project.runtime,combos:project.runtime.combos.filter(c=>c.toSkill!==skillId&&!c.preSkills.includes(skillId))}:undefined};
}
