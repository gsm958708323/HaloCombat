import type {ComboPurpose,Project,RuntimeCombo} from '../model';

export type RulePurpose=ComboPurpose|'unassigned';
export const purposeLabel:Record<RulePurpose,string>={entry:'起手绑定',transition:'动作接续',cancel:'公共取消',unassigned:'未标用途的接续'};
export function rulePurpose(row:RuntimeCombo):RulePurpose{return row.purpose??(row.preSkills.length?'unassigned':'entry');}
export function predecessorOverlap(a:RuntimeCombo,b:RuntimeCombo){
  return !a.preSkills.length||!b.preSkills.length?!a.preSkills.length&&!b.preSkills.length:a.preSkills.some(id=>b.preSkills.includes(id));
}
export function overlappingRules(project:Project,candidate:RuntimeCombo){
  // Positive tags can coexist. Distinct tag sets do not prove mutual exclusion.
  return (project.runtime?.combos??[]).filter(r=>r.id!==candidate.id&&r.input===candidate.input&&predecessorOverlap(r,candidate));
}
export function changedResolution(before:RuntimeCombo|undefined,after:RuntimeCombo){
  return !before||before.input!==after.input||before.toSkill!==after.toSkill||before.priority!==after.priority||
    JSON.stringify([...before.preSkills].sort())!==JSON.stringify([...after.preSkills].sort())||
    JSON.stringify([...before.requiredTags].sort((a,b)=>a-b))!==JSON.stringify([...after.requiredTags].sort((a,b)=>a-b));
}
export function validateRule(project:Project,row:RuntimeCombo){
  if(!row.id.trim()||!row.input.trim()||row.input.length>128)throw new Error('规则身份与输入不能为空，输入最多128字符');
  if(!Number.isInteger(row.priority)||row.priority < -2147483648||row.priority>2147483647)throw new Error('优先级必须是32位整数');
  const skills=new Set(project.skills.map(s=>s.id));
  if(!skills.has(row.toSkill)||row.preSkills.some(id=>!skills.has(id)))throw new Error('规则只能引用已有技能');
  if(row.requiredTags.some(t=>!Number.isInteger(t)||t<=0||t>2147483647))throw new Error('状态Tag必须是正整数');
  if(row.purpose==='entry'&&row.preSkills.length||row.purpose&&row.purpose!=='entry'&&!row.preSkills.length)throw new Error('起手要求空闲前置；动作接续与公共取消必须明确前置技能');
  if(overlappingRules(project,row).some(r=>r.priority===row.priority))throw new Error('相同输入与重叠前置不能使用相同优先级');
}
export function inputLabel(project:Project,input:string){
  const actions=project.runtime?.arena?.bindings.filter(b=>b.input===input).map(b=>b.action)??[];
  return actions.length?actions.join(' / ')+' · '+input:input;
}
