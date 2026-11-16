import type {Project,RuntimeCombo} from './model';
import {ComboProperties} from './ComboProperties';
import {rulePurpose} from './authoring/comboRules';

export function ComboCancellation({project,ids,edit,remove,add}:{project:Project;ids:string[];edit:(row:RuntimeCombo)=>void;remove:(id:string)=>void;add:()=>void}){
  const rows=project.runtime?.combos.filter(r=>rulePurpose(r)==='cancel')??[],affected=rows.filter(r=>r.preSkills.some(id=>ids.includes(id)));
  return <section className="combo-task-content" aria-label="公共取消规则"><h3>公共取消 · 显式适用范围</h3>
    <p className="combo-note">统一配置一组动作允许接入的目标，例如闪避。集合成员满足一个即可；动作播放中仍要求各自接续窗口开启。</p>
    <div className="combo-impact">当前编排受 {affected.length} 条公共规则影响。修改一条规则会影响其全部适用技能；没有列出的技能不会自动加入。</div>
    <ComboProperties project={project} rows={rows} edit={edit} remove={remove}/><button disabled={!project.skills.length} onClick={add}>添加公共取消</button>
  </section>;
}
