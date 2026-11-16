import type {Project,RuntimeCombo} from './model';
import {ComboProperties} from './ComboProperties';
import {rulePurpose} from './authoring/comboRules';

export function ComboBindings({project,skillId,edit,remove,add}:{project:Project;skillId:string;edit:(row:RuntimeCombo)=>void;remove:(id:string)=>void;add:()=>void}){
  const rows=project.runtime?.combos.filter(r=>r.preSkills.length===0&&r.toSkill===skillId)??[];
  const others=project.runtime?.combos.filter(r=>r.preSkills.length&&r.toSkill===skillId)??[];
  const skill=project.skills.find(s=>s.id===skillId),count=new Set(others.flatMap(r=>r.preSkills)).size;
  return <section className="combo-task-content" aria-label="技能起手绑定"><h3>{skill?.name??'选择技能'} · 起手操作</h3><p className="combo-note">从空闲状态收到输入时启动技能；不表示动作播放中的接续。</p>
    <ComboProperties project={project} rows={rows} edit={edit} remove={remove}/><button disabled={!skill} onClick={add}>添加起手绑定</button>
    <details className="combo-incoming"><summary>其他接入规则 · {others.length}条 · {count}个前置技能</summary>
      <p className="combo-note">这些来源独立判断，不属于本次起手路径。{others.some(r=>rulePurpose(r)==='unassigned')?'部分旧规则尚未标注用途，编辑时可以明确归类。':''}</p>
      <ComboProperties project={project} rows={others} edit={edit} remove={remove} showPurpose/></details>
  </section>;
}
