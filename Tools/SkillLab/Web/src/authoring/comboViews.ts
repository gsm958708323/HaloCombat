import type {ComboView,Project,RuntimeCombo} from '../model';
import {rulePurpose} from './comboRules';

export const automaticComboView='$automatic';
export function transitionRules(project:Project){return (project.runtime?.combos??[]).filter(r=>r.preSkills.length&&rulePurpose(r)!=='cancel');}
export function comboScope(project:Project,root:string,viewId=automaticComboView,expandedSources=false,extraIds:string[]=[]){
  const all=project.runtime?.combos??[],view=project.editor?.comboViews?.find(v=>v.id===viewId);
  const start=view?.rootSkill??root,transitions=transitionRules(project);
  const pool=view?transitions.filter(r=>view.ruleIds.includes(r.id)):transitions;
  const ids=new Set<string>([start,...extraIds].filter(id=>project.skills.some(s=>s.id===id)));
  const rows:RuntimeCombo[]=[];const used=new Set<string>();
  // Reachability is a display scope, never a new execution order or rule filter.
  let changed=true;
  while(changed){changed=false;for(const row of pool)if(!used.has(row.id)&&row.preSkills.some(id=>ids.has(id))){
    rows.push(row);used.add(row.id);if(!ids.has(row.toSkill)){ids.add(row.toSkill);changed=true;}
  }}
  if(view)for(const row of pool){
    if(!used.has(row.id)){rows.push(row);used.add(row.id);}
    row.preSkills.forEach(id=>ids.add(id));ids.add(row.toSkill);
  }
  const external=view?transitions.filter(r=>!used.has(r.id)&&r.preSkills.some(id=>ids.has(id))):[];
  const incoming=transitions.filter(r=>!used.has(r.id)&&ids.has(r.toSkill));
  if(expandedSources)for(const row of incoming){rows.push(row);used.add(row.id);row.preSkills.forEach(id=>ids.add(id));}
  const entries=all.filter(r=>!r.preSkills.length&&r.toSkill===start);
  const cancels=all.filter(r=>rulePurpose(r)==='cancel'&&r.preSkills.some(id=>ids.has(id)));
  return {view,root:start,ids:[...ids],rows,entries,incoming,cancels,external,
    unassigned:rows.filter(r=>rulePurpose(r)==='unassigned')};
}
export function saveComboView(project:Project,view:ComboView):Project{
  if(!view.name.trim())throw new Error('请输入编排视图名称');
  return {...project,editor:{...project.editor,comboViews:[...(project.editor?.comboViews??[]).filter(v=>v.id!==view.id),{...view,name:view.name.trim()}]}};
}
export function deleteComboView(project:Project,id:string):Project{
  return {...project,editor:{...project.editor,comboViews:project.editor?.comboViews?.filter(v=>v.id!==id)}};
}
export function maintainComboViews(project:Project):Project{
  if(!project.editor?.comboViews)return project;
  const skills=new Set(project.skills.map(s=>s.id)),rules=new Set(transitionRules(project).map(r=>r.id));
  return {...project,editor:{...project.editor,comboViews:project.editor.comboViews.filter(v=>skills.has(v.rootSkill)).map(v=>({...v,
    ruleIds:v.ruleIds.filter(id=>rules.has(id)),positions:v.positions.filter(p=>p.skillId==='$start'||skills.has(p.skillId))}))}};
}
